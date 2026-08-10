namespace Castlink.Domain;

/// <summary>
/// An actor node in the film graph. Rows are upserted from the cast array of each film's
/// TMDB credits response — there is no separate person-detail ingestion pass.
/// </summary>
public sealed class Person
{
    /// <summary>TMDB person id — used directly as the primary key (see docs/PLAN.md section 2).</summary>
    public int Id { get; set; }

    public required string Name { get; set; }

    public double Popularity { get; set; }

    public string? ProfilePath { get; set; }

    public string? KnownForDepartment { get; set; }

    /// <summary>Denormalised count of <see cref="Credit"/> rows for this person, recomputed on upsert.</summary>
    public int CreditCount { get; set; }

    public DateTimeOffset LastSyncedAt { get; set; }
}
