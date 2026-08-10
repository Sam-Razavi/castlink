namespace Castlink.Infrastructure.Tmdb.Dto;

/// <summary>Response shape for `/discover/movie`. Only the id is needed — full detail comes from
/// a follow-up `/movie/{id}` call — so unmapped fields (vote_count, title, etc.) are just ignored
/// by the deserializer.</summary>
internal sealed record TmdbDiscoverResponse(
    int Page,
    List<TmdbDiscoverResult> Results,
    int TotalPages,
    int TotalResults);

internal sealed record TmdbDiscoverResult(int Id);
