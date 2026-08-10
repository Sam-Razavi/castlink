using System.Net;
using System.Text;
using Castlink.Infrastructure.Configuration;
using Castlink.Infrastructure.Tests.Fakes;
using Castlink.Infrastructure.Tmdb;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Castlink.Infrastructure.Tests.Tmdb;

public sealed class TmdbClientTests
{
    private static TmdbClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") };
        var options = Options.Create(new TmdbOptions { ApiKey = "test-key" });
        return new TmdbClient(httpClient, options, NullLogger<TmdbClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetMovieWithCreditsAsync_requests_the_correct_url_with_append_to_response_and_api_key()
    {
        var handler = new FakeHttpMessageHandler
        {
            Respond = _ => JsonResponse(HttpStatusCode.OK,
                """{"id":1,"title":"T","release_date":"2000-01-01","popularity":1,"vote_count":1,"poster_path":null}"""),
        };
        var client = CreateClient(handler);

        await client.GetMovieWithCreditsAsync(42, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/3/movie/42", request.RequestUri!.AbsolutePath);
        Assert.Contains("append_to_response=credits", request.RequestUri.Query);
        Assert.Contains("api_key=test-key", request.RequestUri.Query);
    }

    [Fact]
    public async Task GetMovieWithCreditsAsync_returns_null_on_404_instead_of_throwing()
    {
        var handler = new FakeHttpMessageHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound) };
        var client = CreateClient(handler);

        var result = await client.GetMovieWithCreditsAsync(404, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DiscoverMovieIdsAsync_pages_until_total_pages_is_reached()
    {
        const string page1 = """{"page":1,"total_pages":2,"total_results":3,"results":[{"id":1},{"id":2}]}""";
        const string page2 = """{"page":2,"total_pages":2,"total_results":3,"results":[{"id":3}]}""";
        var handler = new FakeHttpMessageHandler
        {
            Respond = req => JsonResponse(HttpStatusCode.OK, req.RequestUri!.Query.Contains("page=2") ? page2 : page1),
        };
        var client = CreateClient(handler);

        var ids = await client.DiscoverMovieIdsAsync(2000, minVoteCount: 200, CancellationToken.None);

        Assert.Equal([1, 2, 3], ids);
        Assert.Equal(2, handler.Requests.Count);
        var firstRequestQuery = handler.Requests[0].RequestUri!.Query;
        Assert.Contains("primary_release_year=2000", firstRequestQuery);
        Assert.Contains("vote_count.gte=200", firstRequestQuery);
    }

    [Fact]
    public async Task GetChangedMovieIdsAsync_sends_the_requested_date_window()
    {
        var handler = new FakeHttpMessageHandler
        {
            Respond = _ => JsonResponse(HttpStatusCode.OK,
                """{"page":1,"total_pages":1,"total_results":1,"results":[{"id":9,"adult":false}]}"""),
        };
        var client = CreateClient(handler);

        var ids = await client.GetChangedMovieIdsAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 14), CancellationToken.None);

        Assert.Equal([9], ids);
        var query = handler.Requests[0].RequestUri!.Query;
        Assert.Contains("start_date=2026-01-01", query);
        Assert.Contains("end_date=2026-01-14", query);
    }
}
