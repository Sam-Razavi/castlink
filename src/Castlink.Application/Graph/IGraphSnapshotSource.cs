namespace Castlink.Application.Graph;

/// <summary>
/// Port for supplying the raw edges an <see cref="InMemoryGraphSnapshot"/> is built from. Keeps
/// every bit of graph-building logic in Application (pure, testable); an implementation just
/// hands over rows.
/// </summary>
public interface IGraphSnapshotSource
{
    IAsyncEnumerable<GraphEdgeRecord> StreamEdgesAsync(CancellationToken cancellationToken);
}
