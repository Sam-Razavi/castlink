using Castlink.Application.Ingestion;

namespace Castlink.Application.Tests.Fakes;

internal sealed class FakeIngestionWriter : IIngestionWriter
{
    public List<IReadOnlyList<MovieIngestionRecord>> Batches { get; } = [];

    public Func<IReadOnlyList<MovieIngestionRecord>, IngestionWriteResult>? OnUpsert { get; set; }

    public Task<IngestionWriteResult> UpsertBatchAsync(IReadOnlyList<MovieIngestionRecord> batch, CancellationToken cancellationToken)
    {
        Batches.Add(batch);

        if (OnUpsert is not null)
        {
            return Task.FromResult(OnUpsert(batch));
        }

        var distinctPeople = batch.SelectMany(f => f.Cast).Select(c => c.PersonId).Distinct().Count();
        return Task.FromResult(new IngestionWriteResult(batch.Count, distinctPeople));
    }
}
