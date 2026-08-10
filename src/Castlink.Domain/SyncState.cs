namespace Castlink.Domain;

/// <summary>
/// Generic key/value watermark store. Ingestion uses this to remember where a multi-hour full
/// seed left off (per-year progress) and where the last `/movie/changes` incremental sync ended,
/// so a crashed or re-run job resumes instead of restarting from scratch.
/// </summary>
public sealed class SyncState
{
    public required string Key { get; set; }

    public required string Value { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
