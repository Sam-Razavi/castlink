namespace Castlink.Application.Ingestion;

/// <summary>Port over the <c>ingestion_runs</c> operational log — one row per full-seed or sync run.</summary>
public interface IIngestionRunTracker
{
    Task<Guid> StartAsync(CancellationToken cancellationToken);

    Task CompleteAsync(Guid runId, int filmsProcessed, int peopleProcessed, CancellationToken cancellationToken);

    Task FailAsync(Guid runId, string error, CancellationToken cancellationToken);
}
