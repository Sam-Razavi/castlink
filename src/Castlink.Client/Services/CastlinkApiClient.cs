using System.Net;
using System.Net.Http.Json;
using Castlink.Shared;

namespace Castlink.Client.Services;

/// <summary>Wraps a 404 from `/api/path` as data instead of an exception — "no connection found"
/// is an everyday result for this app, not an error condition.</summary>
public sealed record PathSearchOutcome(bool Found, PathResponse? Response);

/// <summary>
/// Thin wrapper around the <see cref="HttpClient"/> that <c>Program.cs</c> registers with its
/// <c>BaseAddress</c> already pointed at this app's own origin (see docs/PLAN.md Phase 0) — every
/// call here uses a relative path, no CORS involved. A plain C# class, not a component: injected
/// via <c>@inject CastlinkApiClient Api</c> wherever it's needed, and unit tested directly with a
/// fake <see cref="HttpMessageHandler"/> — no browser required (same pattern as the server-side
/// TmdbClient tests from Phase 1).
/// </summary>
public sealed class CastlinkApiClient
{
    private readonly HttpClient _httpClient;

    public CastlinkApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<PersonSearchResultDto>> SearchPeopleAsync(string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var url = $"api/people/search?q={Uri.EscapeDataString(query)}";
        var results = await _httpClient.GetFromJsonAsync<List<PersonSearchResultDto>>(url, cancellationToken);
        return results ?? [];
    }

    public async Task<PathSearchOutcome> FindPathAsync(int fromPersonId, int toPersonId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("api/path", new PathRequest(fromPersonId, toPersonId), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new PathSearchOutcome(false, null);
        }

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<PathResponse>(cancellationToken: cancellationToken);
        return new PathSearchOutcome(true, body);
    }
}
