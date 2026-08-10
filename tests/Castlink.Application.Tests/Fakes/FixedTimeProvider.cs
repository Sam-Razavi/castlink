namespace Castlink.Application.Tests.Fakes;

/// <summary>Deterministic <see cref="TimeProvider"/> double — avoids pulling in an extra test package.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
