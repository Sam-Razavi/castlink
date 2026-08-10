using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Castlink.Application.Ingestion;
using Castlink.Infrastructure.Configuration;
using Castlink.Infrastructure.Tmdb.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Castlink.Infrastructure.Tmdb;

/// <summary>
/// <see cref="ITmdbClient"/> over TMDB's v3 REST API. Rate limiting, retry, and concurrency are
/// handled by <see cref="TmdbResilienceHandler"/> on the underlying <see cref="HttpClient"/> — this
/// class only builds requests and maps responses.
/// </summary>
internal sealed class TmdbClient : ITmdbClient
{
    /// <summary>TMDB hard-caps `/discover` at 500 pages regardless of total_pages reported.</summary>
    private const int MaxDiscoverPages = 500;

    private readonly HttpClient _httpClient;
    private readonly TmdbOptions _options;
    private readonly ILogger<TmdbClient> _logger;

    public TmdbClient(HttpClient httpClient, IOptions<TmdbOptions> options, ILogger<TmdbClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<int>> DiscoverMovieIdsAsync(int year, int minVoteCount, CancellationToken cancellationToken)
    {
        var ids = new List<int>();
        var page = 1;
        var totalPages = 1;

        while (page <= totalPages && page <= MaxDiscoverPages)
        {
            var url = $"discover/movie?primary_release_year={year}&vote_count.gte={minVoteCount}&sort_by=vote_count.desc&page={page}";
            var response = await GetAsync<TmdbDiscoverResponse>(url, cancellationToken);
            if (response is null)
            {
                break;
            }

            ids.AddRange(response.Results.Select(r => r.Id));
            totalPages = Math.Min(response.TotalPages, MaxDiscoverPages);
            page++;
        }

        return ids;
    }

    public async Task<MovieIngestionRecord?> GetMovieWithCreditsAsync(int movieId, CancellationToken cancellationToken)
    {
        var url = $"movie/{movieId}?append_to_response=credits";
        var response = await GetAsync<TmdbMovieDetailResponse>(url, cancellationToken, treatNotFoundAsNull: true);
        return response is null ? null : TmdbClientMapping.ToIngestionRecord(response);
    }

    public async Task<IReadOnlyList<int>> GetChangedMovieIdsAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
    {
        var ids = new List<int>();
        var page = 1;
        var totalPages = 1;
        var start = startDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = endDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        while (page <= totalPages)
        {
            var url = $"movie/changes?start_date={start}&end_date={end}&page={page}";
            var response = await GetAsync<TmdbChangesResponse>(url, cancellationToken);
            if (response is null)
            {
                break;
            }

            ids.AddRange(response.Results.Select(r => r.Id));
            totalPages = response.TotalPages;
            page++;
        }

        return ids;
    }

    private async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken, bool treatNotFoundAsNull = false)
        where T : class
    {
        var separator = relativeUrl.Contains('?') ? '&' : '?';
        var url = $"{relativeUrl}{separator}api_key={Uri.EscapeDataString(_options.ApiKey)}";

        using var response = await _httpClient.GetAsync(url, cancellationToken);

        if (treatNotFoundAsNull && response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogDebug("TMDB returned 404 for {Url} (treated as absent, not an error)", relativeUrl);
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(TmdbJsonOptions.Default, cancellationToken);
    }
}
