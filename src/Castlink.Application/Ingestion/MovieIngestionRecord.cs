namespace Castlink.Application.Ingestion;

/// <summary>A film plus its full cast, ready to be upserted by an <see cref="IIngestionWriter"/>.</summary>
public sealed record MovieIngestionRecord(
    int FilmId,
    string Title,
    int? ReleaseYear,
    double Popularity,
    int VoteCount,
    string? PosterPath,
    IReadOnlyList<CastCreditRecord> Cast);
