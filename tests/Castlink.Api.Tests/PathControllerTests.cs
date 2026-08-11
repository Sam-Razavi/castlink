using System.Net;
using System.Net.Http.Json;
using Castlink.Api.Tests.Fakes;
using Castlink.Application.Graph;
using Castlink.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Castlink.Api.Tests;

/// <summary>
/// Exercises <c>POST /api/path</c> through the real ASP.NET Core pipeline and the real startup
/// graph-loading code in <c>Program.cs</c> — only <see cref="IGraphSnapshotSource"/> and
/// <see cref="IPathEnrichmentRepository"/> are swapped for fixtures, so this needs no Docker, no
/// Postgres, and no TMDB. See docs/PLAN.md Phase 2.
/// </summary>
public sealed class PathControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public PathControllerTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private HttpClient CreateClient(
        IReadOnlyList<GraphEdgeRecord> edges,
        IReadOnlyDictionary<int, PersonSummary> people,
        IReadOnlyDictionary<int, FilmSummary> films)
    {
        var factory = _baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IGraphSnapshotSource>(new FakeGraphSnapshotSource(edges));
                services.AddSingleton<IPathEnrichmentRepository>(new FakePathEnrichmentRepository(people, films));
            }));

        return factory.CreateClient();
    }

    [Fact]
    public async Task Returns_200_with_the_enriched_path_for_a_direct_costar_pair()
    {
        var client = CreateClient(
            edges: [new GraphEdgeRecord(FilmId: 10, PersonId: 1, FilmVoteCount: 500), new GraphEdgeRecord(10, 2, 500)],
            people: new Dictionary<int, PersonSummary> { [1] = new(1, "Alice", "/alice.jpg"), [2] = new(2, "Bob", null) },
            films: new Dictionary<int, FilmSummary> { [10] = new(10, "Some Film", "/poster.jpg") });

        var response = await client.PostAsJsonAsync("/api/path", new PathRequest(1, 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PathResponse>();
        Assert.NotNull(body);
        Assert.Equal(1, body!.Degrees);
        var link = Assert.Single(body.Links);
        Assert.Equal("Alice", link.FromPersonName);
        Assert.Equal("/alice.jpg", link.FromProfilePath);
        Assert.Equal("Some Film", link.FilmTitle);
        Assert.Equal("/poster.jpg", link.FilmPosterPath);
        Assert.Equal("Bob", link.ToPersonName);
        Assert.Null(link.ToProfilePath);
    }

    [Fact]
    public async Task Returns_404_when_both_people_exist_but_no_path_connects_them()
    {
        var client = CreateClient(
            edges: [new GraphEdgeRecord(10, 1, 500), new GraphEdgeRecord(20, 2, 500)],
            people: new Dictionary<int, PersonSummary> { [1] = new(1, "Alice", null), [2] = new(2, "Bob", null) },
            films: new Dictionary<int, FilmSummary>());

        var response = await client.PostAsJsonAsync("/api/path", new PathRequest(1, 2));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_person_id()
    {
        var client = CreateClient(
            edges: [new GraphEdgeRecord(10, 1, 500)],
            people: new Dictionary<int, PersonSummary> { [1] = new(1, "Alice", null) },
            films: new Dictionary<int, FilmSummary>());

        var response = await client.PostAsJsonAsync("/api/path", new PathRequest(1, 999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Returns_200_with_zero_degrees_for_the_same_actor_on_both_ends()
    {
        var client = CreateClient(
            edges: [new GraphEdgeRecord(10, 1, 500)],
            people: new Dictionary<int, PersonSummary> { [1] = new(1, "Alice", null) },
            films: new Dictionary<int, FilmSummary>());

        var response = await client.PostAsJsonAsync("/api/path", new PathRequest(1, 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PathResponse>();
        Assert.NotNull(body);
        Assert.Equal(0, body!.Degrees);
        Assert.Empty(body.Links);
    }
}
