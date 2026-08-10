namespace Castlink.Application.Graph;

public interface IPathFinder
{
    /// <param name="maxDepth">Maximum number of films/links allowed in the returned path — the
    /// "depth cap" from docs/PLAN.md Phase 2. Defaults to 6.</param>
    PathResult FindPath(IGraphSnapshot graph, int fromPersonId, int toPersonId, int maxDepth = 6);
}
