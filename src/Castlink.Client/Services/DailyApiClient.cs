using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Castlink.Shared;

namespace Castlink.Client.Services;

public enum DailySubmitOutcome
{
    Accepted,
    Invalid,
    Unauthorized,
    AlreadySubmitted,
    RateLimited,
    ChallengeNotFound,
}

/// <summary>Non-2xx submit outcomes are everyday results here (an already-played player, a
/// double-click race), not exceptions — same philosophy as <see cref="PathSearchOutcome"/>.</summary>
public sealed record DailySubmitOutcomeResult(DailySubmitOutcome Outcome, DailySubmitResponseDto? Response);

/// <summary>
/// Daily-challenge/leaderboard/player counterpart to <see cref="CastlinkApiClient"/> — kept as a
/// separate class rather than folded in, the same way <c>PeopleController</c> is a separate
/// controller from <c>PathController</c>: distinct enough resources to earn their own file.
/// </summary>
public sealed class DailyApiClient
{
    private readonly HttpClient _httpClient;

    public DailyApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<DailyChallengeDto> GetTodayChallengeAsync(CancellationToken cancellationToken)
    {
        var challenge = await _httpClient.GetFromJsonAsync<DailyChallengeDto>("api/daily", cancellationToken);
        return challenge ?? throw new InvalidOperationException("GET api/daily returned no content.");
    }

    public async Task<IReadOnlyList<SharedFilmDto>> GetSharedFilmsAsync(int fromPersonId, int toPersonId, CancellationToken cancellationToken)
    {
        var films = await _httpClient.GetFromJsonAsync<List<SharedFilmDto>>(
            $"api/people/{fromPersonId}/shared-films/{toPersonId}", cancellationToken);
        return films ?? [];
    }

    public async Task<DailySubmitOutcomeResult> SubmitDailyAsync(
        IReadOnlyList<DailySubmitPathStepDto> path, int durationMs, string playerToken, CancellationToken cancellationToken)
    {
        // Deliberately not `using` here: HttpRequestMessage.Dispose() also disposes its Content,
        // and callers (including tests) may still want to inspect the request after this returns.
        var request = new HttpRequestMessage(HttpMethod.Post, "api/daily/submit")
        {
            Content = JsonContent.Create(new DailySubmitRequestDto(path, durationMs)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", playerToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        var outcome = response.StatusCode switch
        {
            HttpStatusCode.OK => DailySubmitOutcome.Accepted,
            HttpStatusCode.BadRequest => DailySubmitOutcome.Invalid,
            HttpStatusCode.Unauthorized => DailySubmitOutcome.Unauthorized,
            HttpStatusCode.Conflict => DailySubmitOutcome.AlreadySubmitted,
            HttpStatusCode.TooManyRequests => DailySubmitOutcome.RateLimited,
            HttpStatusCode.NotFound => DailySubmitOutcome.ChallengeNotFound,
            _ => throw new HttpRequestException($"Unexpected status {(int)response.StatusCode} from POST api/daily/submit."),
        };

        if (outcome != DailySubmitOutcome.Accepted)
        {
            return new DailySubmitOutcomeResult(outcome, null);
        }

        var body = await response.Content.ReadFromJsonAsync<DailySubmitResponseDto>(cancellationToken: cancellationToken);
        return new DailySubmitOutcomeResult(outcome, body);
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(DateOnly date, int top, CancellationToken cancellationToken)
    {
        var entries = await _httpClient.GetFromJsonAsync<List<LeaderboardEntryDto>>(
            $"api/daily/{date:yyyy-MM-dd}/leaderboard?top={top}", cancellationToken);
        return entries ?? [];
    }

    public async Task<CreatePlayerResponseDto> CreatePlayerAsync(string displayName, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("api/players", new CreatePlayerRequestDto(displayName), cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreatePlayerResponseDto>(cancellationToken: cancellationToken);
        return body ?? throw new InvalidOperationException("POST api/players returned no content.");
    }
}
