namespace Castlink.Shared;

public sealed record PathLinkDto(
    int FromPersonId,
    string FromPersonName,
    string? FromProfilePath,
    int FilmId,
    string FilmTitle,
    string? FilmPosterPath,
    int ToPersonId,
    string ToPersonName,
    string? ToProfilePath);
