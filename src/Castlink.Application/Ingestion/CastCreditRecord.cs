namespace Castlink.Application.Ingestion;

/// <summary>
/// One cast member on a film, as returned by TMDB's credits sub-resource. This is a transport
/// shape between <see cref="ITmdbClient"/> and <see cref="IIngestionWriter"/> — deliberately not
/// a <c>Castlink.Domain</c> entity and not TMDB's wire format, so neither side leaks into the other.
/// </summary>
public sealed record CastCreditRecord(
    int PersonId,
    string Name,
    double Popularity,
    string? ProfilePath,
    string? KnownForDepartment,
    int BillingOrder,
    string? Character);
