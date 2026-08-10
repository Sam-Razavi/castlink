namespace Castlink.Domain;

/// <summary>
/// The bipartite edge between a <see cref="Film"/> and a <see cref="Person"/> — a cast credit.
/// Composite key (<see cref="FilmId"/>, <see cref="PersonId"/>); indexed on both columns
/// individually since the graph traversal walks both directions.
/// </summary>
public sealed class Credit
{
    public int FilmId { get; set; }

    public int PersonId { get; set; }

    /// <summary>TMDB cast billing order — lower is more prominent. Used for tie-breaking in path search.</summary>
    public int BillingOrder { get; set; }

    public string? Character { get; set; }
}
