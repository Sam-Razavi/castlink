namespace Castlink.Application.Graph;

/// <summary>
/// Mutable holder for the loaded graph — registered once as a DI singleton, populated exactly
/// once at API startup (see docs/PLAN.md Phase 2: "load at startup"), read per-request by
/// <c>PathController</c>. Kept separate from <see cref="IPathFinder"/> so the finder itself stays
/// stateless and never risks capturing a stale graph reference.
/// </summary>
public sealed class GraphSnapshotProvider
{
    private volatile IGraphSnapshot _current = InMemoryGraphSnapshot.Empty;

    public IGraphSnapshot Current => _current;

    public void SetSnapshot(IGraphSnapshot snapshot) => _current = snapshot;
}
