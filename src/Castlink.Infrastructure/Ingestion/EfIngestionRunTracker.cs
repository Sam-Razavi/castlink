using Castlink.Application.Ingestion;
using Castlink.Domain;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Castlink.Infrastructure.Ingestion;

internal sealed class EfIngestionRunTracker : IIngestionRunTracker
{
    private readonly CastlinkDbContext _dbContext;

    public EfIngestionRunTracker(CastlinkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> StartAsync(CancellationToken cancellationToken)
    {
        var run = new IngestionRun
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTimeOffset.UtcNow,
            Status = IngestionRunStatus.Running,
        };
        _dbContext.IngestionRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return run.Id;
    }

    public async Task CompleteAsync(Guid runId, int filmsProcessed, int peopleProcessed, CancellationToken cancellationToken)
    {
        var run = await RequireRunAsync(runId, cancellationToken);
        run.Status = IngestionRunStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.FilmsProcessed = filmsProcessed;
        run.PeopleProcessed = peopleProcessed;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid runId, string error, CancellationToken cancellationToken)
    {
        var run = await RequireRunAsync(runId, cancellationToken);
        run.Status = IngestionRunStatus.Failed;
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.Error = error;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IngestionRun> RequireRunAsync(Guid runId, CancellationToken cancellationToken) =>
        await _dbContext.IngestionRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken)
        ?? throw new InvalidOperationException($"No ingestion_runs row for {runId} — StartAsync must be called first.");
}
