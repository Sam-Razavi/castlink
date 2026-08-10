namespace Castlink.Infrastructure.Configuration;

/// <summary>
/// Binds the "Tmdb" configuration section. The API key must never be committed —
/// it is supplied via `dotnet user-secrets` locally or an environment variable
/// (Tmdb__ApiKey) in Azure App Service. See docs/PLAN.md section 4 for the
/// rate-limit and licensing constraints this client must respect once it's built
/// out in Phase 1.
/// </summary>
public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    /// <summary>
    /// TMDB v3 API key. Left empty in appsettings.json by design — bind from
    /// user secrets / environment, never from source control.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Base URL for the TMDB REST API.</summary>
    public string BaseUrl { get; set; } = "https://api.themoviedb.org/3/";

    /// <summary>Conservative per-second request budget (TMDB's CDN limit is ~50 req/s per IP).</summary>
    public int RequestsPerSecond { get; set; } = 25;

    /// <summary>Maximum concurrent connections to TMDB.</summary>
    public int MaxConcurrentConnections { get; set; } = 8;
}
