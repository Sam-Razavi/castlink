namespace Castlink.Api.Tests.Fakes;

/// <summary>Deterministic <see cref="TimeProvider"/> double — avoids pulling in an extra test
/// package. Registering this in a test host's DI overrides <c>TimeProvider.System</c> for every
/// service with an optional <c>TimeProvider</c> constructor parameter (e.g. <c>DailyChallengeService</c>,
/// <c>DailySubmissionService</c>), since a registered type wins over an unresolved optional
/// parameter's default.</summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now)
    {
        _now = now;
    }

    public override DateTimeOffset GetUtcNow() => _now;
}
