namespace Castlink.Application.Graph;

/// <summary>
/// The actual CSR structure, plus its builder. Construction is a single pass over the edge
/// source — dense indices are assigned the first time a person/film id is seen, into growable
/// per-node buckets, which are flattened into flat CSR arrays once the pass completes. At ~1M
/// credits that's roughly 4 MB of <c>int[]</c> per direction (see docs/PLAN.md Phase 2).
/// </summary>
public sealed class InMemoryGraphSnapshot : IGraphSnapshot
{
    public static readonly InMemoryGraphSnapshot Empty = Build([]);

    private readonly int[] _personIds;
    private readonly int[] _filmIds;
    private readonly int[] _filmVoteCounts;
    private readonly Dictionary<int, int> _personIndexById;
    private readonly Dictionary<int, int> _filmIndexById;
    private readonly int[] _personOffsets;
    private readonly int[] _personToFilms;
    private readonly int[] _filmOffsets;
    private readonly int[] _filmToPeople;

    private InMemoryGraphSnapshot(
        int[] personIds,
        int[] filmIds,
        int[] filmVoteCounts,
        Dictionary<int, int> personIndexById,
        Dictionary<int, int> filmIndexById,
        int[] personOffsets,
        int[] personToFilms,
        int[] filmOffsets,
        int[] filmToPeople)
    {
        _personIds = personIds;
        _filmIds = filmIds;
        _filmVoteCounts = filmVoteCounts;
        _personIndexById = personIndexById;
        _filmIndexById = filmIndexById;
        _personOffsets = personOffsets;
        _personToFilms = personToFilms;
        _filmOffsets = filmOffsets;
        _filmToPeople = filmToPeople;
    }

    public int PersonCount => _personIds.Length;

    public int FilmCount => _filmIds.Length;

    public bool TryGetPersonIndex(int personId, out int personIndex) =>
        _personIndexById.TryGetValue(personId, out personIndex);

    public bool TryGetFilmIndex(int filmId, out int filmIndex) =>
        _filmIndexById.TryGetValue(filmId, out filmIndex);

    public int PersonIdAt(int personIndex) => _personIds[personIndex];

    public int FilmIdAt(int filmIndex) => _filmIds[filmIndex];

    public int VoteCountAt(int filmIndex) => _filmVoteCounts[filmIndex];

    public ReadOnlySpan<int> FilmIndexesForPerson(int personIndex) =>
        Slice(_personToFilms, _personOffsets, personIndex);

    public ReadOnlySpan<int> PersonIndexesForFilm(int filmIndex) =>
        Slice(_filmToPeople, _filmOffsets, filmIndex);

    private static ReadOnlySpan<int> Slice(int[] flat, int[] offsets, int index)
    {
        var start = offsets[index];
        var end = offsets[index + 1];
        return flat.AsSpan(start, end - start);
    }

    /// <summary>Synchronous convenience overload — what the fixture-graph unit tests use.</summary>
    public static InMemoryGraphSnapshot Build(IEnumerable<GraphEdgeRecord> edges)
    {
        var state = new BuilderState();
        foreach (var edge in edges)
        {
            state.AddEdge(edge);
        }

        return state.Finish();
    }

    /// <summary>What the real Postgres-backed loader uses — consumes an EF Core streaming query.</summary>
    public static async Task<InMemoryGraphSnapshot> BuildAsync(
        IAsyncEnumerable<GraphEdgeRecord> edges, CancellationToken cancellationToken = default)
    {
        var state = new BuilderState();
        await foreach (var edge in edges.WithCancellation(cancellationToken))
        {
            state.AddEdge(edge);
        }

        return state.Finish();
    }

    /// <summary>Mutable accumulator shared by the sync and async builder entry points.</summary>
    private sealed class BuilderState
    {
        private readonly Dictionary<int, int> _personIndexById = [];
        private readonly Dictionary<int, int> _filmIndexById = [];
        private readonly List<int> _personIds = [];
        private readonly List<int> _filmIds = [];
        private readonly List<int> _filmVoteCounts = [];
        private readonly List<List<int>> _personToFilmsBuckets = [];
        private readonly List<List<int>> _filmToPeopleBuckets = [];

        public void AddEdge(GraphEdgeRecord edge)
        {
            var personIndex = GetOrAddPerson(edge.PersonId);
            var filmIndex = GetOrAddFilm(edge.FilmId, edge.FilmVoteCount);

            _personToFilmsBuckets[personIndex].Add(filmIndex);
            _filmToPeopleBuckets[filmIndex].Add(personIndex);
        }

        public InMemoryGraphSnapshot Finish()
        {
            var (personOffsets, personToFilms) = Flatten(_personToFilmsBuckets);
            var (filmOffsets, filmToPeople) = Flatten(_filmToPeopleBuckets);

            return new InMemoryGraphSnapshot(
                [.. _personIds],
                [.. _filmIds],
                [.. _filmVoteCounts],
                _personIndexById,
                _filmIndexById,
                personOffsets,
                personToFilms,
                filmOffsets,
                filmToPeople);
        }

        private int GetOrAddPerson(int personId)
        {
            if (_personIndexById.TryGetValue(personId, out var index))
            {
                return index;
            }

            index = _personIds.Count;
            _personIndexById[personId] = index;
            _personIds.Add(personId);
            _personToFilmsBuckets.Add([]);
            return index;
        }

        private int GetOrAddFilm(int filmId, int voteCount)
        {
            if (_filmIndexById.TryGetValue(filmId, out var index))
            {
                return index;
            }

            index = _filmIds.Count;
            _filmIndexById[filmId] = index;
            _filmIds.Add(filmId);
            _filmVoteCounts.Add(voteCount);
            _filmToPeopleBuckets.Add([]);
            return index;
        }

        private static (int[] Offsets, int[] Flat) Flatten(List<List<int>> buckets)
        {
            var offsets = new int[buckets.Count + 1];
            var total = 0;
            for (var i = 0; i < buckets.Count; i++)
            {
                offsets[i] = total;
                total += buckets[i].Count;
            }

            offsets[buckets.Count] = total;

            var flat = new int[total];
            for (var i = 0; i < buckets.Count; i++)
            {
                buckets[i].CopyTo(flat, offsets[i]);
            }

            return (offsets, flat);
        }
    }
}
