namespace Castlink.Domain;

/// <summary>A film node in the graph.</summary>
public sealed class Film
{
    /// <summary>TMDB movie id — used directly as the primary key.</summary>
    public int Id { get; set; }

    public required string Title { get; set; }

    public int? ReleaseYear { get; set; }

    public double Popularity { get; set; }

    public int VoteCount { get; set; }

    public string? PosterPath { get; set; }

    public DateTimeOffset LastSyncedAt { get; set; }
}
