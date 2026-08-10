using Castlink.Application.Graph;
using Xunit;

namespace Castlink.Application.Tests.Graph;

public sealed class BidirectionalPathFinderTests
{
    private readonly BidirectionalPathFinder _finder = new();

    private static InMemoryGraphSnapshot BuildGraph(params (int FilmId, int VoteCount, int[] PersonIds)[] films) =>
        InMemoryGraphSnapshot.Build(
            films.SelectMany(f => f.PersonIds.Select(personId => new GraphEdgeRecord(f.FilmId, personId, f.VoteCount))));

    [Fact]
    public void Direct_costars_are_found_as_a_single_link()
    {
        var graph = BuildGraph((FilmId: 1, VoteCount: 100, PersonIds: [1, 2]));

        var result = _finder.FindPath(graph, 1, 2);

        Assert.Equal(PathOutcome.Found, result.Outcome);
        Assert.Equal(1, result.Degrees);
        Assert.Equal([new PathLink(1, 1, 2)], result.Links);
    }

    [Fact]
    public void A_two_hop_chain_is_found_with_the_correct_intermediate_link()
    {
        var graph = BuildGraph(
            (1, 100, [1, 2]),
            (2, 100, [2, 3]));

        var result = _finder.FindPath(graph, 1, 3);

        Assert.Equal(PathOutcome.Found, result.Outcome);
        Assert.Equal(2, result.Degrees);
        Assert.Equal([new PathLink(1, 1, 2), new PathLink(2, 2, 3)], result.Links);
    }

    [Fact]
    public void A_three_hop_chain_is_found()
    {
        var graph = BuildGraph(
            (1, 100, [1, 2]),
            (2, 100, [2, 3]),
            (3, 100, [3, 4]));

        var result = _finder.FindPath(graph, 1, 4);

        Assert.Equal(PathOutcome.Found, result.Outcome);
        Assert.Equal(3, result.Degrees);
    }

    [Fact]
    public void Disconnected_components_return_NoConnectionWithinDepth()
    {
        var graph = BuildGraph(
            (1, 100, [1, 2]),
            (2, 100, [3, 4]));

        var result = _finder.FindPath(graph, 1, 3);

        Assert.Equal(PathOutcome.NoConnectionWithinDepth, result.Outcome);
        Assert.Empty(result.Links);
    }

    [Fact]
    public void The_same_actor_on_both_ends_is_zero_degrees_not_an_error()
    {
        var graph = BuildGraph((1, 100, [1, 2]));

        var result = _finder.FindPath(graph, 1, 1);

        Assert.Equal(PathOutcome.Found, result.Outcome);
        Assert.Equal(0, result.Degrees);
        Assert.Empty(result.Links);
    }

    [Fact]
    public void A_path_at_exactly_the_default_depth_cap_is_found()
    {
        // A 6-hop chain: 1-2-3-4-5-6-7 (six films, six links) — exactly at the default cap.
        var graph = BuildGraph(
            (1, 100, [1, 2]), (2, 100, [2, 3]), (3, 100, [3, 4]),
            (4, 100, [4, 5]), (5, 100, [5, 6]), (6, 100, [6, 7]));

        var result = _finder.FindPath(graph, 1, 7);

        Assert.Equal(PathOutcome.Found, result.Outcome);
        Assert.Equal(6, result.Degrees);
    }

    [Fact]
    public void A_path_one_hop_beyond_the_depth_cap_is_rejected_even_though_it_exists()
    {
        // Same shape as above plus one more hop: true distance is 7, one more than the default cap.
        var graph = BuildGraph(
            (1, 100, [1, 2]), (2, 100, [2, 3]), (3, 100, [3, 4]), (4, 100, [4, 5]),
            (5, 100, [5, 6]), (6, 100, [6, 7]), (7, 100, [7, 8]));

        var result = _finder.FindPath(graph, 1, 8);

        Assert.Equal(PathOutcome.NoConnectionWithinDepth, result.Outcome);
    }

    [Fact]
    public void A_shorter_path_is_preferred_over_a_longer_one_that_also_exists()
    {
        // Direct 2-hop route (1-2-99) and an unrelated 4-hop detour (1-5-6-7-99) both connect
        // the same pair — the shorter one must win, not whichever the algorithm happens to touch.
        var graph = BuildGraph(
            (1, 100, [1, 2]), (2, 100, [2, 99]),
            (3, 100, [1, 5]), (4, 100, [5, 6]), (5, 100, [6, 7]), (6, 100, [7, 99]));

        var result = _finder.FindPath(graph, 1, 99);

        Assert.Equal(PathOutcome.Found, result.Outcome);
        Assert.Equal(2, result.Degrees);
    }

    [Fact]
    public void Film_tiebreak_prefers_higher_vote_count_over_a_lower_film_id()
    {
        // Two films both directly connect 1 and 2 — the higher-vote_count one must win even
        // though it has the higher film id.
        var graph = BuildGraph(
            (FilmId: 50, VoteCount: 100, PersonIds: [1, 2]),
            (FilmId: 10, VoteCount: 200, PersonIds: [1, 2]));

        var result = _finder.FindPath(graph, 1, 2);

        Assert.Equal([new PathLink(1, 10, 2)], result.Links);
    }

    [Fact]
    public void Film_tiebreak_falls_back_to_lowest_film_id_when_vote_counts_are_equal()
    {
        var graph = BuildGraph(
            (FilmId: 99, VoteCount: 100, PersonIds: [1, 2]),
            (FilmId: 5, VoteCount: 100, PersonIds: [1, 2]));

        var result = _finder.FindPath(graph, 1, 2);

        Assert.Equal([new PathLink(1, 5, 2)], result.Links);
    }

    [Fact]
    public void Meeting_point_tiebreak_prefers_the_lowest_person_id_among_equal_length_paths()
    {
        // Two disjoint 2-hop routes from 1 to 6, through person 2 and through person 3
        // respectively — both equally short; person 2 must win as the intermediate.
        var graph = BuildGraph(
            (101, 100, [1, 2]), (102, 100, [2, 6]),
            (201, 100, [1, 3]), (202, 100, [3, 6]));

        var result = _finder.FindPath(graph, 1, 6);

        Assert.Equal(PathOutcome.Found, result.Outcome);
        Assert.Equal(2, result.Degrees);
        Assert.Equal([new PathLink(1, 101, 2), new PathLink(2, 102, 6)], result.Links);
    }

    [Fact]
    public void An_unknown_from_person_id_is_reported_distinctly()
    {
        var graph = BuildGraph((1, 100, [1, 2]));

        var result = _finder.FindPath(graph, 999, 2);

        Assert.Equal(PathOutcome.UnknownFromPerson, result.Outcome);
    }

    [Fact]
    public void An_unknown_to_person_id_is_reported_distinctly()
    {
        var graph = BuildGraph((1, 100, [1, 2]));

        var result = _finder.FindPath(graph, 1, 999);

        Assert.Equal(PathOutcome.UnknownToPerson, result.Outcome);
    }

    [Fact]
    public void An_unknown_id_takes_precedence_even_when_both_ids_are_identical()
    {
        var graph = BuildGraph((1, 100, [1, 2]));

        var result = _finder.FindPath(graph, 999, 999);

        Assert.Equal(PathOutcome.UnknownFromPerson, result.Outcome);
    }
}
