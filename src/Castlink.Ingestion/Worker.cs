namespace Castlink.Ingestion;

/// <summary>
/// Placeholder host for the TMDB ingestion pipeline (docs/PLAN.md Phase 1):
/// daily-export download, throttled fetch/merge into Postgres, and the
/// /movie/changes incremental sync. Runs once and exits until that lands —
/// deliberately not an infinite loop, since there's no work to do yet.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Castlink.Ingestion started with no pipeline wired up yet (Phase 0). " +
            "See docs/PLAN.md Phase 1 for the ingestion pipeline this will host.");
        return Task.CompletedTask;
    }
}
