namespace Castlink.Application.Ingestion;

public sealed record IngestionWriteResult(int FilmsWritten, int PeopleWritten);

/// <summary>
/// Port for persisting a batch of fetched films. Implementations must be transactional and
/// idempotent — re-running the same batch converges to the same rows (upsert, not insert-only),
/// with no partial writes on failure. See <c>PostgresIngestionWriter</c> in Infrastructure.
/// </summary>
public interface IIngestionWriter
{
    Task<IngestionWriteResult> UpsertBatchAsync(IReadOnlyList<MovieIngestionRecord> batch, CancellationToken cancellationToken);
}
