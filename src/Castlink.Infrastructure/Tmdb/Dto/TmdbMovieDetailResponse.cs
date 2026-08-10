namespace Castlink.Infrastructure.Tmdb.Dto;

// TMDB wire-format records for `/movie/{id}?append_to_response=credits`. Deliberately internal —
// these never leave Infrastructure; TmdbClientMapping converts them to Application-layer records.
// Property names map to TMDB's snake_case JSON via JsonNamingPolicy.SnakeCaseLower (see
// TmdbJsonOptions) rather than [JsonPropertyName] attributes on every field.

internal sealed record TmdbMovieDetailResponse(
    int Id,
    string Title,
    string? ReleaseDate,
    double Popularity,
    int VoteCount,
    string? PosterPath,
    TmdbCredits? Credits);

internal sealed record TmdbCredits(List<TmdbCastMember> Cast);

internal sealed record TmdbCastMember(
    int Id,
    string Name,
    double Popularity,
    string? ProfilePath,
    string? KnownForDepartment,
    int Order,
    string? Character);
