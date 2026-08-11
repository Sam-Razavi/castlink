namespace Castlink.Client.Support;

/// <summary>
/// TMDB's image CDN is public and needs no API key (see docs/PLAN.md section 4) — the base URL is
/// a stable, documented constant, not something that needs to be configurable.
/// </summary>
public static class TmdbImageUrls
{
    private const string BaseUrl = "https://image.tmdb.org/t/p/";

    /// <summary>Actor headshot, sized for the search dropdown and path result rows.</summary>
    public static string? Profile(string? profilePath) =>
        string.IsNullOrEmpty(profilePath) ? null : $"{BaseUrl}w185{profilePath}";

    /// <summary>Film poster, sized for the path result rows.</summary>
    public static string? Poster(string? posterPath) =>
        string.IsNullOrEmpty(posterPath) ? null : $"{BaseUrl}w342{posterPath}";
}
