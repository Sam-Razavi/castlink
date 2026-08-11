using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Castlink.Application.Daily;
using Castlink.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Castlink.Infrastructure.Daily;

internal sealed record PlayerTokenPayload(Guid PlayerId, string DisplayName, DateTimeOffset IssuedAt);

/// <summary>
/// Token shape: <c>{base64url(payload json)}.{base64url(HMAC-SHA256(key, payload bytes))}</c>. No
/// expiry — an anonymous identity is meant to be sticky across sessions in localStorage.
/// Validation recomputes the HMAC and compares with <see cref="CryptographicOperations.FixedTimeEquals"/>
/// — constant-time, standard practice even though the stakes here are low.
/// </summary>
internal sealed class HmacPlayerTokenService : IPlayerTokenService
{
    /// <summary>Documented local-dev fallback, never a secret — mirrors the Postgres/Redis
    /// connection-string fallbacks in <c>ServiceCollectionExtensions</c>. Unlike those, this one
    /// also logs a warning outside Development, since a signing key defaulting silently in a real
    /// deployment is worth flagging even though the actual risk is low.</summary>
    private const string LocalDevSigningKeyFallback = "castlink-local-dev-signing-key-not-for-production";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly byte[] _signingKey;

    public HmacPlayerTokenService(IOptions<PlayerTokenOptions> options, IHostEnvironment environment, ILogger<HmacPlayerTokenService> logger)
    {
        var configuredKey = options.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            if (!environment.IsDevelopment())
            {
                logger.LogWarning(
                    "PlayerTokens:SigningKey is not configured outside Development — falling back to " +
                    "the documented local-dev key. Set PlayerTokens__SigningKey before relying on " +
                    "player tokens for anything beyond local testing.");
            }

            configuredKey = LocalDevSigningKeyFallback;
        }

        _signingKey = Encoding.UTF8.GetBytes(configuredKey);
    }

    public string Issue(Guid playerId, string displayName)
    {
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(new PlayerTokenPayload(playerId, displayName, DateTimeOffset.UtcNow), SerializerOptions);
        var signature = Sign(payloadBytes);
        return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
    }

    public bool TryValidate(string token, out Guid playerId, out string displayName)
    {
        playerId = default;
        displayName = string.Empty;

        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        try
        {
            var payloadBytes = Base64UrlDecode(parts[0]);
            var providedSignature = Base64UrlDecode(parts[1]);

            if (!CryptographicOperations.FixedTimeEquals(providedSignature, Sign(payloadBytes)))
            {
                return false;
            }

            var payload = JsonSerializer.Deserialize<PlayerTokenPayload>(payloadBytes, SerializerOptions);
            if (payload is null)
            {
                return false;
            }

            playerId = payload.PlayerId;
            displayName = payload.DisplayName;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private byte[] Sign(byte[] payloadBytes) => HMACSHA256.HashData(_signingKey, payloadBytes);

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        var paddingNeeded = (4 - (padded.Length % 4)) % 4;
        return Convert.FromBase64String(padded + new string('=', paddingNeeded));
    }
}
