namespace Castlink.Infrastructure.Configuration;

/// <summary>
/// Binds the "PlayerTokens" configuration section — signs the anonymous player identity token (see
/// docs/PLAN.md Phase 4). Unlike <see cref="TmdbOptions"/>, an unset key doesn't block `dotnet run`:
/// stakes are low (forging this only lets someone claim a different anonymous identity on a
/// leaderboard), so <c>HmacPlayerTokenService</c> falls back to a documented local-dev key and logs
/// a warning outside Development, rather than failing to start.
/// </summary>
public sealed class PlayerTokenOptions
{
    public const string SectionName = "PlayerTokens";

    /// <summary>
    /// Left empty in appsettings.json by design — set via `dotnet user-secrets set
    /// PlayerTokens:SigningKey &lt;key&gt;` locally, or the `PlayerTokens__SigningKey` app setting in
    /// Azure, for anything beyond local testing.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;
}
