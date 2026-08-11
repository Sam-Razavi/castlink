using System.Security.Cryptography;
using System.Text;
using Castlink.Application.Graph;
using Microsoft.Extensions.Options;

namespace Castlink.Application.Daily;

/// <summary>Generation output, before it's mapped to the persisted <c>DailyChallenge</c> entity.</summary>
public sealed record GeneratedChallenge(
    int FromPersonId,
    int ToPersonId,
    int OptimalLength,
    IReadOnlyList<PathLink> CanonicalPath);

/// <summary>
/// Thrown when no eligible pair produced an acceptable-length path within
/// <see cref="DailyChallengeOptions.MaxGenerationAttempts"/>. Should be unreachable with a pool of
/// more than a handful of people, but a hard bound beats an infinite loop.
/// </summary>
public sealed class DailyChallengeGenerationException : Exception
{
    public DailyChallengeGenerationException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Deterministically picks a date's actor pair: <c>SHA256(date|salt|attempt)</c> seeds a PRNG that
/// indexes into the eligible pool, retrying with an advanced <paramref name="attempt"/> until the
/// resulting shortest path has an optimal length in
/// [<see cref="DailyChallengeOptions.MinOptimalLength"/>, <see cref="DailyChallengeOptions.MaxOptimalLength"/>]
/// — the "curated pair, not a coin flip" rule from docs/PLAN.md Phase 4.
///
/// Determinism holds within one generation call, which is all "same date -&gt; same challenge"
/// requires: <see cref="Random"/> seeded from a fixed integer is deterministic within a .NET major
/// version, and the caller persists the result immediately, so it is never regenerated for that
/// date afterwards.
/// </summary>
public sealed class DailyChallengeGenerator
{
    private readonly IDailyChallengePool _pool;
    private readonly IPathFinder _pathFinder;
    private readonly DailyChallengeOptions _options;

    public DailyChallengeGenerator(IDailyChallengePool pool, IPathFinder pathFinder, IOptions<DailyChallengeOptions> options)
    {
        _pool = pool;
        _pathFinder = pathFinder;
        _options = options.Value;
    }

    public async Task<GeneratedChallenge> GenerateAsync(DateOnly date, IGraphSnapshot graph, CancellationToken cancellationToken)
    {
        var eligiblePersonIds = await _pool.GetEligiblePersonIdsAsync(cancellationToken);
        if (eligiblePersonIds.Count < 2)
        {
            throw new DailyChallengeGenerationException(
                $"Daily challenge pool has only {eligiblePersonIds.Count} eligible people — need at least 2.");
        }

        for (var attempt = 0; attempt < _options.MaxGenerationAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var random = new Random(DeriveSeed(date, _options.Salt, attempt));
            var fromPersonId = eligiblePersonIds[random.Next(eligiblePersonIds.Count)];
            var toPersonId = eligiblePersonIds[random.Next(eligiblePersonIds.Count)];
            if (fromPersonId == toPersonId)
            {
                continue;
            }

            var result = _pathFinder.FindPath(graph, fromPersonId, toPersonId);
            if (result.Outcome == PathOutcome.Found
                && result.Degrees >= _options.MinOptimalLength
                && result.Degrees <= _options.MaxOptimalLength)
            {
                return new GeneratedChallenge(fromPersonId, toPersonId, result.Degrees, result.Links);
            }
        }

        throw new DailyChallengeGenerationException(
            $"No eligible pair produced a {_options.MinOptimalLength}-{_options.MaxOptimalLength} degree " +
            $"path within {_options.MaxGenerationAttempts} attempts for {date:yyyy-MM-dd}.");
    }

    private static int DeriveSeed(DateOnly date, string salt, int attempt)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{date:yyyy-MM-dd}|{salt}|{attempt}"));
        return BitConverter.ToInt32(hash, 0);
    }
}
