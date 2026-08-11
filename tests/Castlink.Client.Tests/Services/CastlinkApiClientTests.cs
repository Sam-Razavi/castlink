using System.Net;
using System.Net.Http.Json;
using System.Text;
using Castlink.Client.Services;
using Castlink.Client.Tests.Fakes;
using Castlink.Shared;
using Xunit;

namespace Castlink.Client.Tests.Services;

public sealed class CastlinkApiClientTests
{
    private static CastlinkApiClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new CastlinkApiClient(httpClient);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task SearchPeopleAsync_does_not_call_the_api_for_a_blank_query()
    {
        var handler = new FakeHttpMessageHandler();
        var client = CreateClient(handler);

        var results = await client.SearchPeopleAsync("   ", CancellationToken.None);

        Assert.Empty(results);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchPeopleAsync_url_encodes_the_query_and_returns_the_deserialised_results()
    {
        var handler = new FakeHttpMessageHandler
        {
            Respond = _ => JsonResponse(HttpStatusCode.OK, """[{"id":1,"name":"Tom Hanks","profilePath":"/tom.jpg","popularity":50.2}]"""),
        };
        var client = CreateClient(handler);

        var results = await client.SearchPeopleAsync("tom &hanks", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Contains("q=tom%20%26hanks", request.RequestUri!.Query);
        var result = Assert.Single(results);
        Assert.Equal("Tom Hanks", result.Name);
        Assert.Equal("/tom.jpg", result.ProfilePath);
    }

    [Fact]
    public async Task FindPathAsync_returns_the_response_when_a_path_is_found()
    {
        var handler = new FakeHttpMessageHandler
        {
            Respond = _ => JsonResponse(HttpStatusCode.OK,
                """{"degrees":1,"links":[{"fromPersonId":1,"fromPersonName":"Alice","fromProfilePath":null,"filmId":10,"filmTitle":"Some Film","filmPosterPath":null,"toPersonId":2,"toPersonName":"Bob","toProfilePath":null}],"computedInMs":1.5}"""),
        };
        var client = CreateClient(handler);

        var outcome = await client.FindPathAsync(1, 2, CancellationToken.None);

        Assert.True(outcome.Found);
        Assert.NotNull(outcome.Response);
        Assert.Equal(1, outcome.Response!.Degrees);
        var request = Assert.Single(handler.Requests);
        var sentBody = await request.Content!.ReadFromJsonAsync<PathRequest>();
        Assert.Equal(new PathRequest(1, 2), sentBody);
    }

    [Fact]
    public async Task FindPathAsync_returns_not_found_as_data_not_an_exception()
    {
        var handler = new FakeHttpMessageHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound) };
        var client = CreateClient(handler);

        var outcome = await client.FindPathAsync(1, 999, CancellationToken.None);

        Assert.False(outcome.Found);
        Assert.Null(outcome.Response);
    }

    [Fact]
    public async Task FindPathAsync_still_throws_for_a_genuine_server_error()
    {
        var handler = new FakeHttpMessageHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) };
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FindPathAsync(1, 2, CancellationToken.None));
    }
}
