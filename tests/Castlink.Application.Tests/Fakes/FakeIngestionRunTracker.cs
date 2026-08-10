using Castlink.Application.Ingestion;

namespace Castlink.Application.Tests.Fakes;

internal sealed class FakeIngestionRunTracker : IIngestionRunTracker
{
    public int StartCalls { get; private set; }

    public (int Films, int People)? Completed { get; private set; }

    public string? FailedError { get; private set; }

    public Task<Guid> StartAsync(CancellationToken cancellationToken)
    {
        StartCalls++;
        return Task.FromResult(Guid.NewGuid());
    }

    public Task CompleteAsync(Guid runId, int filmsProcessed, int peopleProcessed, CancellationToken cancellationToken)
    {
        Completed = (filmsProcessed, peopleProcessed);
        return Task.CompletedTask;
    }

    public Task FailAsync(Guid runId, string error, CancellationToken cancellationToken)
    {
        FailedError = error;
        return Task.CompletedTask;
    }
}
