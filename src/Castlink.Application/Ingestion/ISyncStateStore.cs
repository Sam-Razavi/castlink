namespace Castlink.Application.Ingestion;

/// <summary>
/// Port over the <c>sync_state</c> key/value watermark table. Used both for the
/// `/movie/changes` incremental-sync cursor and for per-year progress during a full seed.
/// </summary>
public interface ISyncStateStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string value, CancellationToken cancellationToken);
}
