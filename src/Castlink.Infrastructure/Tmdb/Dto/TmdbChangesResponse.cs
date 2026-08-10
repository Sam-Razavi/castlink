namespace Castlink.Infrastructure.Tmdb.Dto;

/// <summary>Response shape for `/movie/changes`.</summary>
internal sealed record TmdbChangesResponse(
    List<TmdbChangeEntry> Results,
    int Page,
    int TotalPages,
    int TotalResults);

internal sealed record TmdbChangeEntry(int Id, bool Adult);
