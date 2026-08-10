namespace Castlink.Application.Graph;

/// <summary>
/// Bidirectional BFS over an <see cref="IGraphSnapshot"/> — deliberately stateless (the graph is a
/// parameter, not a constructor dependency) so tests build a tiny fixture graph and call this
/// directly, and production never risks a singleton holding a stale graph reference.
///
/// Unidirectional BFS isn't viable here: a popular actor has thousands of co-stars, so a depth-3
/// frontier explodes into the millions. Meeting in the middle keeps each side's frontier far
/// smaller. See docs/PLAN.md Phase 2 for the full rationale, including the tie-break rules below.
/// </summary>
public sealed class BidirectionalPathFinder : IPathFinder
{
    public PathResult FindPath(IGraphSnapshot graph, int fromPersonId, int toPersonId, int maxDepth = 6)
    {
        if (!graph.TryGetPersonIndex(fromPersonId, out var fromIndex))
        {
            return PathResult.NotFound(PathOutcome.UnknownFromPerson);
        }

        if (!graph.TryGetPersonIndex(toPersonId, out var toIndex))
        {
            return PathResult.NotFound(PathOutcome.UnknownToPerson);
        }

        if (fromIndex == toIndex)
        {
            return PathResult.Found([]);
        }

        var visitedForward = new Dictionary<int, VisitEntry> { [fromIndex] = new VisitEntry(0, -1, -1) };
        var visitedBackward = new Dictionary<int, VisitEntry> { [toIndex] = new VisitEntry(0, -1, -1) };
        var frontierForward = new List<int> { fromIndex };
        var frontierBackward = new List<int> { toIndex };
        var forwardDepth = 0;
        var backwardDepth = 0;

        while (true)
        {
            var canExpandForward = frontierForward.Count > 0 && forwardDepth < maxDepth;
            var canExpandBackward = frontierBackward.Count > 0 && backwardDepth < maxDepth;

            if (!canExpandForward && !canExpandBackward)
            {
                return PathResult.NotFound(PathOutcome.NoConnectionWithinDepth);
            }

            // Always expand whichever side is currently shallower (ties favour forward,
            // arbitrarily but consistently) — never let one side race ahead of the other. This
            // "balanced" invariant is what makes "stop at the first meeting found" correct: it
            // guarantees the first layer expansion to produce any meeting candidate is producing
            // the globally shortest one, not just a locally-first one. A naive strict-alternation
            // schedule, or a termination check based on the *sum* of rounds run so far, can both
            // under- and over-shoot the true answer on asymmetric graphs — see the plan for the
            // traced-through counterexamples that ruled those out.
            var expandForward = canExpandForward && (!canExpandBackward || forwardDepth <= backwardDepth);
            var meetingCandidates = new List<int>();

            if (expandForward)
            {
                forwardDepth++;
                frontierForward = ExpandLayer(frontierForward, graph, visitedForward, visitedBackward, forwardDepth, meetingCandidates);
            }
            else
            {
                backwardDepth++;
                frontierBackward = ExpandLayer(frontierBackward, graph, visitedBackward, visitedForward, backwardDepth, meetingCandidates);
            }

            if (meetingCandidates.Count == 0)
            {
                continue;
            }

            var best = SelectBestMeeting(meetingCandidates, graph, visitedForward, visitedBackward, expandForward);
            var combinedDistance = visitedForward[best].Depth + visitedBackward[best].Depth;

            // The side just expanded reached its cap independently of the other side, so a
            // meeting found here can still exceed maxDepth in total (e.g. 4+4=8 against a cap of
            // 6) — that's the true shortest path, and it's simply too long; report accordingly
            // rather than an over-cap "Found".
            return combinedDistance <= maxDepth
                ? ReconstructPath(best, graph, visitedForward, visitedBackward)
                : PathResult.NotFound(PathOutcome.NoConnectionWithinDepth);
        }
    }

    private static List<int> ExpandLayer(
        List<int> frontier,
        IGraphSnapshot graph,
        Dictionary<int, VisitEntry> visitedThisSide,
        Dictionary<int, VisitEntry> visitedOtherSide,
        int newDepth,
        List<int> meetingCandidates)
    {
        // A target person can be reachable via more than one film from this layer — keep only the
        // best (tie-break: highest vote_count, then lowest film id) as that person's parent edge,
        // so the result is deterministic regardless of enumeration order.
        var bestFilmForNewPerson = new Dictionary<int, int>();
        var parentOfNewPerson = new Dictionary<int, int>();

        foreach (var personIndex in frontier)
        {
            foreach (var filmIndex in graph.FilmIndexesForPerson(personIndex))
            {
                foreach (var neighborIndex in graph.PersonIndexesForFilm(filmIndex))
                {
                    if (visitedThisSide.ContainsKey(neighborIndex))
                    {
                        continue;
                    }

                    if (!bestFilmForNewPerson.TryGetValue(neighborIndex, out var currentBestFilm)
                        || IsBetterFilm(graph, filmIndex, currentBestFilm))
                    {
                        bestFilmForNewPerson[neighborIndex] = filmIndex;
                        parentOfNewPerson[neighborIndex] = personIndex;
                    }
                }
            }
        }

        var nextFrontier = new List<int>(bestFilmForNewPerson.Count);
        foreach (var (newPersonIndex, filmIndex) in bestFilmForNewPerson)
        {
            visitedThisSide[newPersonIndex] = new VisitEntry(newDepth, filmIndex, parentOfNewPerson[newPersonIndex]);
            nextFrontier.Add(newPersonIndex);

            if (visitedOtherSide.ContainsKey(newPersonIndex))
            {
                meetingCandidates.Add(newPersonIndex);
            }
        }

        return nextFrontier;
    }

    private static bool IsBetterFilm(IGraphSnapshot graph, int candidateFilmIndex, int currentBestFilmIndex)
    {
        var candidateVotes = graph.VoteCountAt(candidateFilmIndex);
        var currentVotes = graph.VoteCountAt(currentBestFilmIndex);
        return candidateVotes != currentVotes
            ? candidateVotes > currentVotes
            : graph.FilmIdAt(candidateFilmIndex) < graph.FilmIdAt(currentBestFilmIndex);
    }

    /// <summary>
    /// All candidates here were just added at the same depth on the side that was expanded this
    /// round, so that side's contribution to the combined distance is fixed — minimising the
    /// combined distance is equivalent to minimising the *other* side's recorded depth.
    /// docs/PLAN.md doesn't specify a rule for multiple equal-length paths through different
    /// people (only for choosing a film) — lowest person id is the extension that makes this
    /// fully deterministic, flagged in the plan.
    /// </summary>
    private static int SelectBestMeeting(
        List<int> candidates,
        IGraphSnapshot graph,
        Dictionary<int, VisitEntry> visitedForward,
        Dictionary<int, VisitEntry> visitedBackward,
        bool expandedForwardThisRound)
    {
        return candidates
            .OrderBy(p => expandedForwardThisRound ? visitedBackward[p].Depth : visitedForward[p].Depth)
            .ThenBy(graph.PersonIdAt)
            .First();
    }

    private static PathResult ReconstructPath(
        int meetingPersonIndex,
        IGraphSnapshot graph,
        Dictionary<int, VisitEntry> visitedForward,
        Dictionary<int, VisitEntry> visitedBackward)
    {
        // Forward side: walking parent pointers from the meeting point leads back toward `from`,
        // so links come out meeting-first — reverse to read from -> ... -> meeting.
        var forwardLinks = new List<PathLink>();
        var cursor = meetingPersonIndex;
        while (visitedForward[cursor].ParentPersonIndex != -1)
        {
            var entry = visitedForward[cursor];
            forwardLinks.Add(new PathLink(
                FromPersonId: graph.PersonIdAt(entry.ParentPersonIndex),
                FilmId: graph.FilmIdAt(entry.ViaFilmIndex),
                ToPersonId: graph.PersonIdAt(cursor)));
            cursor = entry.ParentPersonIndex;
        }

        forwardLinks.Reverse();

        // Backward side: walking parent pointers from the meeting point leads toward `to`, which
        // is already the right reading order (meeting -> ... -> to) — no reverse needed.
        var backwardLinks = new List<PathLink>();
        cursor = meetingPersonIndex;
        while (visitedBackward[cursor].ParentPersonIndex != -1)
        {
            var entry = visitedBackward[cursor];
            backwardLinks.Add(new PathLink(
                FromPersonId: graph.PersonIdAt(cursor),
                FilmId: graph.FilmIdAt(entry.ViaFilmIndex),
                ToPersonId: graph.PersonIdAt(entry.ParentPersonIndex)));
            cursor = entry.ParentPersonIndex;
        }

        var links = new List<PathLink>(forwardLinks.Count + backwardLinks.Count);
        links.AddRange(forwardLinks);
        links.AddRange(backwardLinks);

        return PathResult.Found(links);
    }

    private readonly record struct VisitEntry(int Depth, int ViaFilmIndex, int ParentPersonIndex);
}
