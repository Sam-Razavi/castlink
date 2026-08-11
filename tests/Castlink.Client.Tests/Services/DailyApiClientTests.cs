using System.Net;
using System.Net.Http.Json;
using System.Text;
using Castlink.Client.Services;
using Castlink.Client.Tests.Fakes;
using Castlink.Shared;
using Xunit;

namespace Castlink.Client.Tests.Services;

public sealed class DailyApiClientTests
{
    private static DailyApiClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new DailyApiClient(httpClient);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetTodayChallengeAsync_deserialises_the_response()
    {
        var handler = new FakeHttpMessageHandler
        {
            Respond = _ => JsonResponse(HttpStatusCode.OK,
                """{"date":"2026-08-11","fromPerson":{"id":1,"name":"Alice","profilePath":"/alice.jpg"},"toPerson":{"id":3,"name":"Carol","profilePath":null}}"""),
        };
        var client = CreateClient(handler);

        var challenge = await client.GetTodayChallengeAsync(CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 8, 11), challenge.Date);
        Assert.Equal("Alice", challenge.FromPerson.Name);
        Assert.Equal("Carol", challenge.ToPerson.Name);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/daily", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetSharedFilmsAsync_hits_the_expected_route_and_returns_the_list()
    {
        var handler = new FakeHttpMessageHandler
        {
            Respond = _ => JsonResponse(HttpStatusCode.OK, """[{"id":10,"title":"Film A","posterPath":"/a.jpg"}]"""),
        };
        var client = CreateClient(handler);

        var films = await client.GetSharedFilmsAsync(1, 2, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/people/1/shared-films/2", request.RequestUri!.AbsolutePath);
        var film = Assert.Single(films);
        Assert.Equal("Film A", film.Title);
    }

    [Fact]
    public async Task SubmitDailyAsync_attaches_the_bearer_token_and_returns_the_accepted_body()
    {
        var handler = new FakeHttpMessageHandler
        {
            Respond = _ => JsonResponse(HttpStatusCode.OK,
                """{"pathLength":2,"optimalLength":2,"score":1000,"canonicalPath":[]}"""),
        };
        var client = CreateClient(handler);
        var path = new List<DailySubmitPathStepDto> { new(1, 10), new(2, null) };

        var outcome = await client.SubmitDailyAsync(path, durationMs: 5000, playerToken: "test-token", CancellationToken.None);

        Assert.Equal(DailySubmitOutcome.Accepted, outcome.Outcome);
        Assert.NotNull(outcome.Response);
        Assert.Equal(1000, outcome.Response!.Score);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
        var sentBody = await request.Content!.ReadFromJsonAsync<DailySubmitRequestDto>();
        Assert.Equal(5000, sentBody!.DurationMs);
        Assert.Equal(2, sentBody.Path.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, DailySubmitOutcome.Invalid)]
    [InlineData(HttpStatusCode.Unauthorized, DailySubmitOutcome.Unauthorized)]
    [InlineData(HttpStatusCode.Conflict, DailySubmitOutcome.AlreadySubmitted)]
    [InlineData((HttpStatusCode)429, DailySubmitOutcome.RateLimited)]
    [InlineData(HttpStatusCode.NotFound, DailySubmitOutcome.ChallengeNotFound)]
    public async Task SubmitDailyAsync_maps_non_2xx_statuses_to_the_matching_outcome_with_no_body(
        HttpStatusCode status, DailySubmitOutcome expectedOutcome)
    {
        var handler = new FakeHttpMessageHandler { Respond = _ => new HttpResponseMessage(status) };
        var client = CreateClient(handler);

        var outcome = await client.SubmitDailyAsync([], durationMs: 0, playerToken: "token", CancellationToken.None);

        Assert.Equal(expectedOutcome, outcome.Outcome);
        Assert.Null(outcome.Response);
    }

    [Fact]
    public async Task SubmitDailyAsync_still_throws_for_a_genuine_server_error()
    {
        var handler = new FakeHttpMessageHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) };
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.SubmitDailyAsync([], durationMs: 0, playerToken: "token", CancellationToken.None));
    }

    [Fact]
    public async Task GetLeaderboardAsync_builds_the_expected_route_with_top()
    {
        var handler = new FakeHttpMessageHandler
        {
            Respond = _ => JsonResponse(HttpStatusCode.OK, """[{"rank":1,"displayName":"Alice","score":950}]"""),
        };
        var client = CreateClient(handler);

        var entries = await client.GetLeaderboardAsync(new DateOnly(2026, 8, 11), top: 20, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/daily/2026-08-11/leaderboard", request.RequestUri!.AbsolutePath);
        Assert.Equal("?top=20", request.RequestUri!.Query);
        var entry = Assert.Single(entries);
        Assert.Equal("Alice", entry.DisplayName);
    }

    [Fact]
    public async Task CreatePlayerAsync_posts_the_display_name_and_returns_the_issued_token()
    {
        var handler = new FakeHttpMessageHandler
        {
            Respond = _ => JsonResponse(HttpStatusCode.OK,
                """{"playerId":"11111111-1111-1111-1111-111111111111","displayName":"Alice","token":"issued-token"}"""),
        };
        var client = CreateClient(handler);

        var response = await client.CreatePlayerAsync("Alice", CancellationToken.None);

        Assert.Equal("issued-token", response.Token);
        Assert.Equal("Alice", response.DisplayName);
        var request = Assert.Single(handler.Requests);
        var sentBody = await request.Content!.ReadFromJsonAsync<CreatePlayerRequestDto>();
        Assert.Equal("Alice", sentBody!.DisplayName);
    }
}
