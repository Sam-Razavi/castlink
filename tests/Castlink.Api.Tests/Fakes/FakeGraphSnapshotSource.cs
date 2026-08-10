using System.Runtime.CompilerServices;
using Castlink.Application.Graph;

namespace Castlink.Api.Tests.Fakes;

/// <summary>
/// Feeds fixture edges through the *real* startup graph-loading path in <c>Program.cs</c>, instead
/// of hitting Postgres — the app still builds its <see cref="InMemoryGraphSnapshot"/> for real,
/// just from in-memory data.
/// </summary>
internal sealed class FakeGraphSnapshotSource : IGraphSnapshotSource
{
    private readonly IReadOnlyList<GraphEdgeRecord> _edges;

    public FakeGraphSnapshotSource(IReadOnlyList<GraphEdgeRecord> edges)
    {
        _edges = edges;
    }

    public async IAsyncEnumerable<GraphEdgeRecord> StreamEdgesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var edge in _edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return edge;
        }

        await Task.CompletedTask;
    }
}
