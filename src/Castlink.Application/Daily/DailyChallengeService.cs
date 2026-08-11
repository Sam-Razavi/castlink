using Castlink.Application.Graph;
using Castlink.Domain;

namespace Castlink.Application.Daily;

/// <summary>
/// Orchestrates "give me today's challenge, generating it if this is the first request of the
/// day" — deliberately on-demand rather than a background scheduler (see docs/PLAN.md Phase 4):
/// this is a single-instance portfolio deployment with no uptime guarantee at midnight UTC, and
/// generation reuses the already-resident graph's microsecond-cost BFS, so there is nothing
/// expensive to precompute ahead of time.
/// </summary>
public sealed class DailyChallengeService
{
    private readonly IDailyChallengeRepository _repository;
    private readonly DailyChallengeGenerator _generator;
    private readonly GraphSnapshotProvider _graphSnapshotProvider;
    private readonly TimeProvider _timeProvider;

    public DailyChallengeService(
        IDailyChallengeRepository repository,
        DailyChallengeGenerator generator,
        GraphSnapshotProvider graphSnapshotProvider,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _generator = generator;
        _graphSnapshotProvider = graphSnapshotProvider;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Today's date, UTC — the same "date" every part of Phase 4 (generation, submission,
    /// leaderboard keys) agrees on.</summary>
    public DateOnly Today => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    public async Task<DailyChallenge> GetOrGenerateForTodayAsync(CancellationToken cancellationToken)
    {
        var today = Today;
        var existing = await _repository.FindAsync(today, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var generated = await _generator.GenerateAsync(today, _graphSnapshotProvider.Current, cancellationToken);
        var challenge = new DailyChallenge
        {
            Date = today,
            FromPersonId = generated.FromPersonId,
            ToPersonId = generated.ToPersonId,
            OptimalLength = generated.OptimalLength,
            CanonicalPath = generated.CanonicalPath
                .Select(link => new PathLinkRecord(link.FromPersonId, link.FilmId, link.ToPersonId))
                .ToList(),
            GeneratedAt = _timeProvider.GetUtcNow(),
        };

        return await _repository.InsertIfNotExistsAsync(challenge, cancellationToken);
    }
}
