using Castlink.Infrastructure.Configuration;
using Castlink.Infrastructure.Daily;
using Castlink.Infrastructure.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Castlink.Infrastructure.Tests.Daily;

public sealed class HmacPlayerTokenServiceTests
{
    private static HmacPlayerTokenService BuildService(string signingKey) =>
        new(
            Options.Create(new PlayerTokenOptions { SigningKey = signingKey }),
            new FakeHostEnvironment(),
            NullLogger<HmacPlayerTokenService>.Instance);

    [Fact]
    public void A_token_round_trips_back_to_the_same_player_id_and_display_name()
    {
        var service = BuildService("test-signing-key");
        var playerId = Guid.NewGuid();

        var token = service.Issue(playerId, "Alice");
        var valid = service.TryValidate(token, out var validatedPlayerId, out var validatedDisplayName);

        Assert.True(valid);
        Assert.Equal(playerId, validatedPlayerId);
        Assert.Equal("Alice", validatedDisplayName);
    }

    [Fact]
    public void A_token_with_a_tampered_payload_segment_is_rejected()
    {
        var service = BuildService("test-signing-key");
        var token = service.Issue(Guid.NewGuid(), "Alice");
        var parts = token.Split('.');

        var tampered = $"{parts[0]}AAAA.{parts[1]}";

        Assert.False(service.TryValidate(tampered, out _, out _));
    }

    [Fact]
    public void A_token_with_a_tampered_signature_segment_is_rejected()
    {
        var service = BuildService("test-signing-key");
        var token = service.Issue(Guid.NewGuid(), "Alice");
        var parts = token.Split('.');

        var tampered = $"{parts[0]}.{parts[1]}AAAA";

        Assert.False(service.TryValidate(tampered, out _, out _));
    }

    [Fact]
    public void A_token_issued_with_a_different_signing_key_is_rejected()
    {
        var issuer = BuildService("key-one");
        var validator = BuildService("key-two");
        var token = issuer.Issue(Guid.NewGuid(), "Alice");

        Assert.False(validator.TryValidate(token, out _, out _));
    }

    [Fact]
    public void A_malformed_token_without_the_expected_separator_is_rejected()
    {
        var service = BuildService("test-signing-key");

        Assert.False(service.TryValidate("not-a-real-token", out _, out _));
    }

    [Fact]
    public void An_unconfigured_signing_key_falls_back_to_the_local_dev_key_without_throwing()
    {
        var service = BuildService(signingKey: string.Empty);

        var token = service.Issue(Guid.NewGuid(), "Alice");

        Assert.True(service.TryValidate(token, out _, out _));
    }
}
