using Castlink.Domain;

namespace Castlink.Application.Daily;

/// <summary>
/// Orchestrates a daily-challenge submission end to end: rate-limit → load today's challenge →
/// validate the path server-side against real credits → score → persist → publish to the
/// leaderboard. See docs/PLAN.md Phase 4's two hard rules — neither is enforced anywhere else, so
/// this class is where they actually live: the canonical path is only ever attached to an
/// <see cref="DailySubmissionOutcome.Accepted"/> result, and a submission is never scored without
/// first passing <see cref="IDailyPathValidator"/>.
/// </summary>
public sealed class DailySubmissionService
{
    private readonly IDailySubmissionRateLimiter _rateLimiter;
    private readonly IDailyChallengeRepository _challengeRepository;
    private readonly ICreditLookupRepository _creditLookupRepository;
    private readonly IDailyPathValidator _validator;
    private readonly IDailySubmissionRepository _submissionRepository;
    private readonly ILeaderboardStore _leaderboardStore;
    private readonly TimeProvider _timeProvider;

    public DailySubmissionService(
        IDailySubmissionRateLimiter rateLimiter,
        IDailyChallengeRepository challengeRepository,
        ICreditLookupRepository creditLookupRepository,
        IDailyPathValidator validator,
        IDailySubmissionRepository submissionRepository,
        ILeaderboardStore leaderboardStore,
        TimeProvider? timeProvider = null)
    {
        _rateLimiter = rateLimiter;
        _challengeRepository = challengeRepository;
        _creditLookupRepository = creditLookupRepository;
        _validator = validator;
        _submissionRepository = submissionRepository;
        _leaderboardStore = leaderboardStore;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DailySubmissionResult> SubmitAsync(
        Guid playerId,
        IReadOnlyList<DailyPathStepRecord> submittedPath,
        int durationMs,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        if (!await _rateLimiter.TryAcquireAsync(today, playerId, cancellationToken))
        {
            return DailySubmissionResult.Rejected(DailySubmissionOutcome.RateLimited);
        }

        var challenge = await _challengeRepository.FindAsync(today, cancellationToken);
        if (challenge is null)
        {
            return DailySubmissionResult.Rejected(DailySubmissionOutcome.ChallengeNotFound);
        }

        var filmIds = submittedPath
            .Where(step => step.FilmId is not null)
            .Select(step => step.FilmId!.Value)
            .Distinct()
            .ToArray();
        var knownCredits = await _creditLookupRepository.GetCreditsForFilmsAsync(filmIds, cancellationToken);

        var validation = _validator.Validate(submittedPath, challenge.FromPersonId, challenge.ToPersonId, knownCredits);
        if (!validation.IsValid)
        {
            // Rejected, never persisted — a fabricated/malformed submission is a client bug, not a
            // player's real attempt, and shouldn't burn their one graded try for the day.
            return DailySubmissionResult.Rejected(DailySubmissionOutcome.InvalidPath);
        }

        var score = DailyScoring.Score(challenge.OptimalLength, validation.Length);
        var submission = new DailySubmission
        {
            Id = Guid.NewGuid(),
            Date = today,
            PlayerId = playerId,
            Path = validation.Links.ToList(),
            PathLength = validation.Length,
            Score = score,
            DurationMs = durationMs,
            SubmittedAt = _timeProvider.GetUtcNow(),
        };

        try
        {
            await _submissionRepository.InsertAsync(submission, cancellationToken);
        }
        catch (DuplicateSubmissionException)
        {
            return DailySubmissionResult.Rejected(DailySubmissionOutcome.AlreadySubmitted);
        }

        await _leaderboardStore.SubmitAsync(today, playerId, score, durationMs, cancellationToken);

        return DailySubmissionResult.Accepted(validation.Length, challenge.OptimalLength, score, challenge.CanonicalPath);
    }
}
