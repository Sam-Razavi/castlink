using Castlink.Application.Ingestion;
using Microsoft.Extensions.Configuration;

namespace Castlink.Ingestion;

/// <summary>
/// Run-to-completion ingestion batch job — not a long-lived loop. Runs one full seed or one
/// incremental sync (see <c>Ingestion:Mode</c> config), logs a summary, and stops the host so the
/// process exits cleanly whether run locally, from CI, or as a scheduled job.
/// </summary>
public sealed class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IConfiguration _configuration;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        IConfiguration configuration,
        ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ingestionService = scope.ServiceProvider.GetRequiredService<IngestionService>();

            var mode = _configuration["Ingestion:Mode"] ?? "Incremental";
            _logger.LogInformation("Starting ingestion run in {Mode} mode", mode);

            var summary = mode.Equals("FullSeed", StringComparison.OrdinalIgnoreCase)
                ? await RunFullSeedAsync(ingestionService, stoppingToken)
                : await ingestionService.RunIncrementalSyncAsync(stoppingToken);

            _logger.LogInformation(
                "Ingestion run complete: {FilmsWritten} films, {PeopleWritten} people",
                summary.FilmsWritten, summary.PeopleWritten);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Ingestion run cancelled");
        }
        catch (Exception ex)
        {
            // IngestionService has already recorded the failure in ingestion_runs — this is just
            // making sure the process exits non-zero so a scheduler/CI notices.
            _logger.LogCritical(ex, "Ingestion run failed");
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }

    private Task<IngestionSummary> RunFullSeedAsync(IngestionService ingestionService, CancellationToken cancellationToken)
    {
        var startYear = _configuration.GetValue<int?>("Ingestion:FullSeedStartYear") ?? 1970;
        return ingestionService.RunFullSeedAsync(startYear, cancellationToken);
    }
}
