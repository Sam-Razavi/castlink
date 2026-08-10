using Castlink.Application.Graph;
using Xunit;

namespace Castlink.Application.Tests.Graph;

public sealed class InMemoryGraphSnapshotTests
{
    [Fact]
    public void Build_assigns_dense_zero_based_indices_and_preserves_reverse_lookup()
    {
        var snapshot = InMemoryGraphSnapshot.Build(
        [
            new GraphEdgeRecord(FilmId: 100, PersonId: 1, FilmVoteCount: 500),
            new GraphEdgeRecord(FilmId: 100, PersonId: 2, FilmVoteCount: 500),
            new GraphEdgeRecord(FilmId: 200, PersonId: 2, FilmVoteCount: 300),
        ]);

        Assert.Equal(2, snapshot.PersonCount);
        Assert.Equal(2, snapshot.FilmCount);

        Assert.True(snapshot.TryGetPersonIndex(1, out var person1Index));
        Assert.True(snapshot.TryGetPersonIndex(2, out var person2Index));
        Assert.True(snapshot.TryGetFilmIndex(100, out var film100Index));
        Assert.True(snapshot.TryGetFilmIndex(200, out var film200Index));

        Assert.Equal(1, snapshot.PersonIdAt(person1Index));
        Assert.Equal(2, snapshot.PersonIdAt(person2Index));
        Assert.Equal(100, snapshot.FilmIdAt(film100Index));
        Assert.Equal(200, snapshot.FilmIdAt(film200Index));
        Assert.Equal(500, snapshot.VoteCountAt(film100Index));
        Assert.Equal(300, snapshot.VoteCountAt(film200Index));
    }

    [Fact]
    public void TryGetPersonIndex_and_TryGetFilmIndex_return_false_for_unknown_ids()
    {
        var snapshot = InMemoryGraphSnapshot.Build([new GraphEdgeRecord(1, 1, 100)]);

        Assert.False(snapshot.TryGetPersonIndex(999, out _));
        Assert.False(snapshot.TryGetFilmIndex(999, out _));
    }

    [Fact]
    public void FilmIndexesForPerson_and_PersonIndexesForFilm_are_mutually_consistent()
    {
        // A shared film (100) connecting two people (1, 2); person 2 also has a solo film (200).
        var snapshot = InMemoryGraphSnapshot.Build(
        [
            new GraphEdgeRecord(FilmId: 100, PersonId: 1, FilmVoteCount: 500),
            new GraphEdgeRecord(FilmId: 100, PersonId: 2, FilmVoteCount: 500),
            new GraphEdgeRecord(FilmId: 200, PersonId: 2, FilmVoteCount: 300),
        ]);

        snapshot.TryGetPersonIndex(1, out var person1);
        snapshot.TryGetPersonIndex(2, out var person2);
        snapshot.TryGetFilmIndex(100, out var film100);
        snapshot.TryGetFilmIndex(200, out var film200);

        Assert.Equal([film100], snapshot.FilmIndexesForPerson(person1).ToArray());
        Assert.Equal([film100, film200], snapshot.FilmIndexesForPerson(person2).ToArray());
        Assert.Equal([person1, person2], snapshot.PersonIndexesForFilm(film100).ToArray());
        Assert.Equal([person2], snapshot.PersonIndexesForFilm(film200).ToArray());
    }

    [Fact]
    public void Empty_snapshot_has_no_people_or_films()
    {
        Assert.Equal(0, InMemoryGraphSnapshot.Empty.PersonCount);
        Assert.Equal(0, InMemoryGraphSnapshot.Empty.FilmCount);
    }

    [Fact]
    public async Task BuildAsync_produces_the_same_result_as_the_synchronous_overload()
    {
        GraphEdgeRecord[] edges =
        [
            new GraphEdgeRecord(100, 1, 500),
            new GraphEdgeRecord(100, 2, 500),
            new GraphEdgeRecord(200, 2, 300),
        ];

        var syncSnapshot = InMemoryGraphSnapshot.Build(edges);
        var asyncSnapshot = await InMemoryGraphSnapshot.BuildAsync(ToAsyncEnumerable(edges));

        Assert.Equal(syncSnapshot.PersonCount, asyncSnapshot.PersonCount);
        Assert.Equal(syncSnapshot.FilmCount, asyncSnapshot.FilmCount);
    }

    private static async IAsyncEnumerable<GraphEdgeRecord> ToAsyncEnumerable(IEnumerable<GraphEdgeRecord> edges)
    {
        foreach (var edge in edges)
        {
            yield return edge;
        }

        await Task.Yield();
    }
}
