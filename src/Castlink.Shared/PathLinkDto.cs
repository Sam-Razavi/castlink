namespace Castlink.Shared;

public sealed record PathLinkDto(
    int FromPersonId,
    string FromPersonName,
    int FilmId,
    string FilmTitle,
    int ToPersonId,
    string ToPersonName);
