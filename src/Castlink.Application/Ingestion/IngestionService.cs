using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Castlink.Application.Ingestion;

/// <summary>
/// Orchestrates ingestion against the <see cref="ITmdbClient"/>/<see cref="IIngestionWriter"/>
/// ports. Has no infrastructure dependency — everything here is unit-testable with fakes, no
/// network or database (see docs/PLAN.md Phase 1).
/// </summary>
public sealed class IngestionService
{
    /// <summary>Watermark key: last release year whose full-seed discovery+write completed.</summary>
    internal const string FullSeedWatermarkKey = "full_seed:last_completed_year";

    /// <summary>Watermark key: end date (exclusive) of the last completed `/movie/changes` window.</summary>
    internal const string IncrementalSyncWatermarkKey = "incremental_sync:last_synced_date";

    /// <summary>TMDB's `/movie/changes` accepts at most a 14-day window per call.</summary>
    private const int MaxChangesWindowDays = 14;

    private const int WriteBatchSize = 200;
    private const int MinVoteCount = 200;
    private const string DateFormat = "yyyy-MM-dd";

    private readonly ITmdbClient _tmdbClient;
    private readonly IIngestionWriter _writer;
    private readonly ISyncStateStore _syncState;
    private readonly IIngestionRunTracker _runTracker;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        ITmdbClient tmdbClient,
        IIngestionWriter writer,
        ISyncStateStore syncState,
        IIngestionRunTracker runTracker,
        ILogger<IngestionService> logger,
        TimeProvider? timeProvider = null)
    {
        _tmdbClient = tmdbClient;
        _writer = writer;
        _syncState = syncState;
        _runTracker = runTracker;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Discovers and writes every film with at least <c>vote_count &gt;= 200</c>, one release year
    /// at a time, from <paramref name="startYear"/> (or wherever a previous run's watermark left
    /// off) through the current year. Advances <see cref="FullSeedWatermarkKey"/> after each year
    /// so a crashed multi-hour run resumes at year granularity instead of restarting from scratch.
    /// </summary>
    public Task<IngestionSummary> RunFullSeedAsync(int startYear, CancellationToken cancellationToken) =>
        RunTrackedAsync(() => RunFullSeedCoreAsync(startYear, cancellationToken), cancellationToken);

    /// <summary>
    /// Fetches and writes films changed since the last sync (or the last day, if this is the
    /// first run), via `/movie/changes`, chunked into TMDB's 14-day-max windows and looped until
    /// caught up to today.
    /// </summary>
    public Task<IngestionSummary> RunIncrementalSyncAsync(CancellationToken cancellationToken) =>
        RunTrackedAsync(() => RunIncrementalSyncCoreAsync(cancellationToken), cancellationToken);

    private async Task<IngestionSummary> RunTrackedAsync(
        Func<Task<IngestionSummary>> run, CancellationToken cancellationToken)
    {
        var runId = await _runTracker.StartAsync(cancellationToken);
        try
        {
            var summary = await run();
            await _runTracker.CompleteAsync(runId, summary.FilmsWritten, summary.PeopleWritten, cancellationToken);
            return summary;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ingestion run {RunId} failed", runId);
            await _runTracker.FailAsync(runId, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task<IngestionSummary> RunFullSeedCoreAsync(int startYear, CancellationToken cancellationToken)
    {
        var totalFilms = 0;
        var totalPeople = 0;
        var currentYear = _timeProvider.GetUtcNow().Year;

        var watermark = await _syncState.GetAsync(FullSeedWatermarkKey, cancellationToken);
        var resumeFromYear = watermark is not null && int.TryParse(watermark, NumberStyles.Integer, CultureInfo.InvariantCulture, out var completedYear)
            ? completedYear + 1
            : startYear;

        for (var year = resumeFromYear; year <= currentYear; year++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var movieIds = await _tmdbClient.DiscoverMovieIdsAsync(year, MinVoteCount, cancellationToken);
            _logger.LogInformation("Discovered {Count} candidate films for {Year}", movieIds.Count, year);

            var (films, people) = await FetchAndWriteAsync(movieIds, cancellationToken);
            totalFilms += films;
            totalPeople += people;

            await _syncState.SetAsync(FullSeedWatermarkKey, year.ToString(CultureInfo.InvariantCulture), cancellationToken);
        }

        return new IngestionSummary(totalFilms, totalPeople);
    }

    private async Task<IngestionSummary> RunIncrementalSyncCoreAsync(CancellationToken cancellationToken)
    {
        var totalFilms = 0;
        var totalPeople = 0;
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        var watermark = await _syncState.GetAsync(IncrementalSyncWatermarkKey, cancellationToken);
        var windowStart = watermark is not null && DateOnly.TryParseExact(watermark, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : today.AddDays(-1);

        while (windowStart < today)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var daysRemaining = today.DayNumber - windowStart.DayNumber;
            var windowEnd = windowStart.AddDays(Math.Min(MaxChangesWindowDays, daysRemaining));

            var changedIds = await _tmdbClient.GetChangedMovieIdsAsync(windowStart, windowEnd, cancellationToken);
            _logger.LogInformation("{Count} films changed between {Start} and {End}", changedIds.Count, windowStart, windowEnd);

            var (films, people) = await FetchAndWriteAsync(changedIds, cancellationToken);
            totalFilms += films;
            totalPeople += people;

            windowStart = windowEnd;
            await _syncState.SetAsync(IncrementalSyncWatermarkKey, windowStart.ToString(DateFormat, CultureInfo.InvariantCulture), cancellationToken);
        }

        return new IngestionSummary(totalFilms, totalPeople);
    }

    private async Task<(int Films, int People)> FetchAndWriteAsync(IReadOnlyList<int> movieIds, CancellationToken cancellationToken)
    {
        var totalFilms = 0;
        var totalPeople = 0;
        var buffer = new List<MovieIngestionRecord>(WriteBatchSize);

        foreach (var movieId in movieIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = await _tmdbClient.GetMovieWithCreditsAsync(movieId, cancellationToken);
            if (record is null)
            {
                _logger.LogWarning("TMDB has no movie {MovieId} (skipped)", movieId);
                continue;
            }

            buffer.Add(record);
            if (buffer.Count >= WriteBatchSize)
            {
                var (films, people) = await FlushAsync(buffer, cancellationToken);
                totalFilms += films;
                totalPeople += people;
            }
        }

        if (buffer.Count > 0)
        {
            var (films, people) = await FlushAsync(buffer, cancellationToken);
            totalFilms += films;
            totalPeople += people;
        }

        return (totalFilms, totalPeople);
    }

    private async Task<(int Films, int People)> FlushAsync(List<MovieIngestionRecord> buffer, CancellationToken cancellationToken)
    {
        // Snapshot before clearing: the writer's IReadOnlyList<T> contract doesn't promise it
        // won't retain the reference (tests do, to assert against it), so reusing and clearing
        // `buffer` in place would silently truncate whatever the caller held onto.
        var batch = buffer.ToArray();
        buffer.Clear();
        var result = await _writer.UpsertBatchAsync(batch, cancellationToken);
        return (result.FilmsWritten, result.PeopleWritten);
    }
}
