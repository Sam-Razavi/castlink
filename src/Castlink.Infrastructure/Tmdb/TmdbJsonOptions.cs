using System.Text.Json;

namespace Castlink.Infrastructure.Tmdb;

internal static class TmdbJsonOptions
{
    /// <summary>
    /// TMDB's JSON is snake_case; the DTO records in <c>Tmdb/Dto</c> are plain PascalCase with no
    /// per-property [JsonPropertyName] attributes — this naming policy does the mapping instead.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}
