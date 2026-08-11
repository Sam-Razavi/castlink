using Castlink.Api.Hubs;
using Castlink.Application.Daily;
using Castlink.Application.Graph;
using Castlink.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Castlink.Api.Controllers;

[ApiController]
[Route("api/daily")]
public sealed class DailyController : ControllerBase
{
    private const int DefaultLeaderboardTop = 20;
    private const int MaxLeaderboardTop = 100;

    private readonly DailyChallengeService _dailyChallengeService;
    private readonly DailySubmissionService _submissionService;
    private readonly IPlayerTokenService _playerTokenService;
    private readonly ILeaderboardStore _leaderboardStore;
    private readonly IPlayerRepository _playerRepository;
    private readonly IPathEnrichmentRepository _enrichmentRepository;
    private readonly IHubContext<LeaderboardHub> _hubContext;

    public DailyController(
        DailyChallengeService dailyChallengeService,
        DailySubmissionService submissionService,
        IPlayerTokenService playerTokenService,
        ILeaderboardStore leaderboardStore,
        IPlayerRepository playerRepository,
        IPathEnrichmentRepository enrichmentRepository,
        IHubContext<LeaderboardHub> hubContext)
    {
        _dailyChallengeService = dailyChallengeService;
        _submissionService = submissionService;
        _playerTokenService = playerTokenService;
        _leaderboardStore = leaderboardStore;
        _playerRepository = playerRepository;
        _enrichmentRepository = enrichmentRepository;
        _hubContext = hubContext;
    }

    // Never Ok(challenge) on the raw entity — that's the single most load-bearing line for
    // docs/PLAN.md Phase 4's "must not leak the answer" rule. OptimalLength/CanonicalPath simply
    // don't exist on DailyChallengeDto.
    [HttpGet]
    public async Task<ActionResult<DailyChallengeDto>> GetToday(CancellationToken cancellationToken)
    {
        var challenge = await _dailyChallengeService.GetOrGenerateForTodayAsync(cancellationToken);
        var people = await _enrichmentRepository.GetPeopleAsync(
            [challenge.FromPersonId, challenge.ToPersonId], cancellationToken);

        return Ok(new DailyChallengeDto(
            challenge.Date,
            ToPersonSummaryDto(people, challenge.FromPersonId),
            ToPersonSummaryDto(people, challenge.ToPersonId)));
    }

    [HttpPost("submit")]
    public async Task<ActionResult<DailySubmitResponseDto>> Submit(
        [FromBody] DailySubmitRequestDto request, CancellationToken cancellationToken)
    {
        if (!TryGetPlayerIdFromAuthHeader(out var playerId))
        {
            return Unauthorized(new { reason = "MissingOrInvalidToken" });
        }

        var submittedPath = request.Path
            .Select(step => new DailyPathStepRecord(step.PersonId, step.FilmId))
            .ToList();

        var result = await _submissionService.SubmitAsync(playerId, submittedPath, request.DurationMs, cancellationToken);

        if (result.Outcome != DailySubmissionOutcome.Accepted)
        {
            return result.Outcome switch
            {
                DailySubmissionOutcome.RateLimited => StatusCode(StatusCodes.Status429TooManyRequests, new { reason = result.Outcome.ToString() }),
                DailySubmissionOutcome.AlreadySubmitted => Conflict(new { reason = result.Outcome.ToString() }),
                DailySubmissionOutcome.ChallengeNotFound => NotFound(new { reason = result.Outcome.ToString() }),
                _ => BadRequest(new { reason = result.Outcome.ToString() }),
            };
        }

        var canonicalPath = await PathLinkEnricher.EnrichAsync(_enrichmentRepository, result.CanonicalPath, cancellationToken);
        await PushLeaderboardUpdateAsync(cancellationToken);

        return Ok(new DailySubmitResponseDto(result.PathLength, result.OptimalLength, result.Score, canonicalPath));
    }

    [HttpGet("{date}/leaderboard")]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboard(
        string date, [FromQuery] int? top, CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
        {
            return BadRequest(new { reason = "InvalidDate" });
        }

        var effectiveTop = Math.Clamp(top ?? DefaultLeaderboardTop, 1, MaxLeaderboardTop);
        var entries = await _leaderboardStore.GetTopAsync(parsedDate, effectiveTop, cancellationToken);
        return Ok(await ResolveDisplayNamesAsync(entries, cancellationToken));
    }

    private bool TryGetPlayerIdFromAuthHeader(out Guid playerId)
    {
        playerId = default;

        const string bearerPrefix = "Bearer ";
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(bearerPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var token = header[bearerPrefix.Length..];
        return _playerTokenService.TryValidate(token, out playerId, out _);
    }

    private async Task PushLeaderboardUpdateAsync(CancellationToken cancellationToken)
    {
        var today = _dailyChallengeService.Today;
        var entries = await _leaderboardStore.GetTopAsync(today, DefaultLeaderboardTop, cancellationToken);
        var dtos = await ResolveDisplayNamesAsync(entries, cancellationToken);
        await _hubContext.Clients
            .Group(LeaderboardHub.GroupName(today.ToString("yyyy-MM-dd")))
            .SendAsync("LeaderboardUpdated", dtos, cancellationToken);
    }

    private async Task<IReadOnlyList<LeaderboardEntryDto>> ResolveDisplayNamesAsync(
        IReadOnlyList<LeaderboardEntryRecord> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var playerIds = entries.Select(entry => entry.PlayerId).Distinct().ToArray();
        var players = await _playerRepository.GetByIdsAsync(playerIds, cancellationToken);

        return entries
            .Select(entry => new LeaderboardEntryDto(
                entry.Rank,
                players.TryGetValue(entry.PlayerId, out var player) ? player.DisplayName : "Unknown player",
                entry.Score))
            .ToList();
    }

    private static PersonSummaryDto ToPersonSummaryDto(IReadOnlyDictionary<int, PersonSummary> people, int personId) =>
        people.TryGetValue(personId, out var person)
            ? new PersonSummaryDto(person.Id, person.Name, person.ProfilePath)
            : new PersonSummaryDto(personId, $"#{personId}", null);
}
