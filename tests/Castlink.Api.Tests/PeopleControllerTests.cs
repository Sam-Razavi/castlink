using System.Net;
using System.Net.Http.Json;
using Castlink.Api.Tests.Fakes;
using Castlink.Application.Graph;
using Castlink.Application.People;
using Castlink.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Castlink.Api.Tests;

/// <summary>
/// Exercises <c>GET /api/people/search</c> through the real ASP.NET Core pipeline with
/// <see cref="IPersonSearchRepository"/> substituted for a fixture — no Docker, no Postgres.
/// See docs/PLAN.md Phase 3.
/// </summary>
public sealed class PeopleControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public PeopleControllerTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private (HttpClient Client, FakePersonSearchRepository Fake) CreateClient(IReadOnlyList<PersonSearchResult> results)
    {
        var fake = new FakePersonSearchRepository(results);
        var factory = _baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                // Program.cs's startup graph load runs unconditionally on every host build,
                // regardless of which controller a test is actually exercising — every
                // WebApplicationFactory-based test needs this override, not just PathController's.
                services.AddSingleton<IGraphSnapshotSource>(new FakeGraphSnapshotSource([]));
                services.AddSingleton<IPersonSearchRepository>(fake);
            }));

        return (factory.CreateClient(), fake);
    }

    [Fact]
    public async Task Returns_matching_people_for_a_query()
    {
        var (client, _) = CreateClient([new PersonSearchResult(1, "Tom Hanks", "/tom.jpg", 50.2)]);

        var response = await client.GetAsync("/api/people/search?q=tom");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PersonSearchResultDto>>();
        Assert.NotNull(body);
        var result = Assert.Single(body!);
        Assert.Equal(1, result.Id);
        Assert.Equal("Tom Hanks", result.Name);
        Assert.Equal("/tom.jpg", result.ProfilePath);
        Assert.Equal(50.2, result.Popularity);
    }

    [Fact]
    public async Task Returns_an_empty_list_for_a_blank_query_without_calling_the_repository()
    {
        var (client, fake) = CreateClient([new PersonSearchResult(1, "Should not appear", null, 1)]);

        var response = await client.GetAsync("/api/people/search?q=");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PersonSearchResultDto>>();
        Assert.Empty(body!);
        Assert.Null(fake.LastQuery);
    }

    [Fact]
    public async Task Returns_an_empty_list_when_the_query_parameter_is_missing_entirely()
    {
        var (client, fake) = CreateClient([new PersonSearchResult(1, "Should not appear", null, 1)]);

        var response = await client.GetAsync("/api/people/search");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<PersonSearchResultDto>>();
        Assert.Empty(body!);
        Assert.Null(fake.LastQuery);
    }

    [Fact]
    public async Task Defaults_the_limit_when_none_is_supplied()
    {
        var (client, fake) = CreateClient([]);

        await client.GetAsync("/api/people/search?q=tom");

        Assert.Equal(10, fake.LastLimit);
    }

    [Fact]
    public async Task Clamps_an_out_of_range_limit_before_it_reaches_the_repository()
    {
        var (client, fake) = CreateClient([]);

        await client.GetAsync("/api/people/search?q=tom&limit=1000");

        Assert.Equal(50, fake.LastLimit);
    }
}
