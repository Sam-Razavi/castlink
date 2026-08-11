using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Castlink.Api.Tests.Fakes;
using Castlink.Application.Daily;
using Castlink.Application.Graph;
using Castlink.Domain;
using Castlink.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Castlink.Api.Tests;

/// <summary>
/// Exercises <c>GET /api/daily</c>, <c>POST /api/daily/submit</c>, and
/// <c>GET /api/daily/{date}/leaderboard</c> through the real ASP.NET Core pipeline. Every daily-
/// challenge port is a fixture (including <see cref="TimeProvider"/>, pinned so "today" always
/// matches the fixture challenge's date) — no Docker, no Postgres, no Redis. The real
/// <see cref="IPlayerTokenService"/> mints and validates tokens via <c>POST /api/players</c>, the
/// same way a browser would. See docs/PLAN.md Phase 4.
/// </summary>
public sealed class DailyControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly ChallengeDate = new(2026, 8, 11);
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    // Fixture: person 1 --film 10--> person 2 --film 20--> person 3 (optimal length 2).
    private static readonly DailyChallenge Challenge = new()
    {
        Date = ChallengeDate,
        FromPersonId = 1,
        ToPersonId = 3,
        OptimalLength = 2,
        CanonicalPath = [new PathLinkRecord(1, 10, 2), new PathLinkRecord(2, 20, 3)],
        GeneratedAt = FixedNow,
    };

    private static readonly IReadOnlySet<(int, int)> KnownCredits = new HashSet<(int, int)>
    {
        (1, 10), (2, 10), (2, 20), (3, 20),
    };

    private readonly WebApplicationFactory<Program> _baseFactory;

    public DailyControllerTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private (HttpClient Client, FakeDailySubmissionRepository SubmissionRepository, FakeLeaderboardStore LeaderboardStore, FakePlayerRepository PlayerRepository) CreateClient(
        IReadOnlyList<LeaderboardEntryRecord>? leaderboardEntries = null,
        bool rateLimiterAllows = true,
        FakePlayerRepository? playerRepository = null)
    {
        var submissionRepository = new FakeDailySubmissionRepository();
        var leaderboardStore = new FakeLeaderboardStore(leaderboardEntries ?? []);
        var players = playerRepository ?? new FakePlayerRepository();

        var factory = _baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IGraphSnapshotSource>(new FakeGraphSnapshotSource([]));
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedNow));
                services.AddSingleton<IDailyChallengeRepository>(new FakeDailyChallengeRepository(Challenge));
                services.AddSingleton<ICreditLookupRepository>(new FakeCreditLookupRepository(KnownCredits));
                services.AddSingleton<IDailySubmissionRepository>(submissionRepository);
                services.AddSingleton<ILeaderboardStore>(leaderboardStore);
                services.AddSingleton<IDailySubmissionRateLimiter>(new FakeDailySubmissionRateLimiter(rateLimiterAllows));
                services.AddSingleton<IPlayerRepository>(players);
                services.AddSingleton<IPathEnrichmentRepository>(new FakePathEnrichmentRepository(
                    people: new Dictionary<int, PersonSummary>
                    {
                        [1] = new(1, "Alice", "/alice.jpg"),
                        [2] = new(2, "Bob", "/bob.jpg"),
                        [3] = new(3, "Carol", "/carol.jpg"),
                    },
                    films: new Dictionary<int, FilmSummary>
                    {
                        [10] = new(10, "Film A", "/filma.jpg"),
                        [20] = new(20, "Film B", "/filmb.jpg"),
                    }));
            }));

        return (factory.CreateClient(), submissionRepository, leaderboardStore, players);
    }

    private static async Task<string> CreatePlayerTokenAsync(HttpClient client, string displayName = "Tester")
    {
        var response = await client.PostAsJsonAsync("/api/players", new CreatePlayerRequestDto(displayName));
        var body = await response.Content.ReadFromJsonAsync<CreatePlayerResponseDto>();
        return body!.Token;
    }

    private static DailySubmitRequestDto ValidSubmitRequest(int durationMs = 5000) => new(
        [new DailySubmitPathStepDto(1, 10), new DailySubmitPathStepDto(2, 20), new DailySubmitPathStepDto(3, null)],
        durationMs);

    [Fact]
    public async Task GetToday_never_serializes_optimalLength_or_canonicalPath()
    {
        var (client, _, _, _) = CreateClient();

        var response = await client.GetAsync("/api/daily");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var topLevelPropertyNames = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        Assert.DoesNotContain(topLevelPropertyNames, name => name.Contains("optimal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(topLevelPropertyNames, name => name.Contains("canonical", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(topLevelPropertyNames, name => name.Equals("fromPerson", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(topLevelPropertyNames, name => name.Equals("toPerson", StringComparison.OrdinalIgnoreCase));

        var body = JsonSerializer.Deserialize<DailyChallengeDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(body);
        Assert.Equal(ChallengeDate, body!.Date);
        Assert.Equal("Alice", body.FromPerson.Name);
        Assert.Equal("Carol", body.ToPerson.Name);
    }

    [Fact]
    public async Task Submit_with_a_valid_path_returns_200_and_reveals_the_canonical_path()
    {
        var (client, submissionRepository, leaderboardStore, _) = CreateClient();
        var token = await CreatePlayerTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/daily/submit", ValidSubmitRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DailySubmitResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.PathLength);
        Assert.Equal(2, body.OptimalLength);
        Assert.Equal(1000, body.Score); // exactly optimal — full score
        Assert.Equal(2, body.CanonicalPath.Count);
        Assert.Equal("Alice", body.CanonicalPath[0].FromPersonName);
        Assert.Equal("Film A", body.CanonicalPath[0].FilmTitle);

        Assert.Single(submissionRepository.Submissions);
        Assert.Single(leaderboardStore.Submissions);
    }

    [Fact]
    public async Task Submit_with_a_fabricated_step_returns_400_and_is_not_persisted()
    {
        var (client, submissionRepository, leaderboardStore, _) = CreateClient();
        var token = await CreatePlayerTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var fabricated = new DailySubmitRequestDto(
            [new DailySubmitPathStepDto(1, 999), new DailySubmitPathStepDto(3, null)], DurationMs: 5000);

        var response = await client.PostAsJsonAsync("/api/daily/submit", fabricated);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(submissionRepository.Submissions);
        Assert.Empty(leaderboardStore.Submissions);
    }

    [Fact]
    public async Task Submit_twice_for_the_same_date_returns_409_on_the_second_attempt()
    {
        var (client, submissionRepository, _, _) = CreateClient();
        var token = await CreatePlayerTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await client.PostAsJsonAsync("/api/daily/submit", ValidSubmitRequest());
        var second = await client.PostAsJsonAsync("/api/daily/submit", ValidSubmitRequest());

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Single(submissionRepository.Submissions);
    }

    [Fact]
    public async Task Submit_without_an_authorization_header_returns_401()
    {
        var (client, _, _, _) = CreateClient();

        var response = await client.PostAsJsonAsync("/api/daily/submit", ValidSubmitRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Submit_when_the_rate_limiter_rejects_returns_429()
    {
        var (client, submissionRepository, _, _) = CreateClient(rateLimiterAllows: false);
        var token = await CreatePlayerTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/daily/submit", ValidSubmitRequest());

        Assert.Equal((HttpStatusCode)429, response.StatusCode);
        Assert.Empty(submissionRepository.Submissions);
    }

    [Fact]
    public async Task Leaderboard_returns_ranked_entries_with_display_names_resolved()
    {
        var playerId = Guid.NewGuid();
        var players = new FakePlayerRepository();
        await players.InsertAsync(new Player { Id = playerId, DisplayName = "Alice", CreatedAt = FixedNow }, CancellationToken.None);
        var entries = new List<LeaderboardEntryRecord> { new(playerId, Rank: 1, Score: 950) };
        var (client, _, _, _) = CreateClient(leaderboardEntries: entries, playerRepository: players);

        var response = await client.GetAsync($"/api/daily/{ChallengeDate:yyyy-MM-dd}/leaderboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<LeaderboardEntryDto>>();
        Assert.NotNull(body);
        var entry = Assert.Single(body!);
        Assert.Equal(1, entry.Rank);
        Assert.Equal("Alice", entry.DisplayName);
        Assert.Equal(950, entry.Score);
    }

    [Fact]
    public async Task Leaderboard_rejects_a_malformed_date()
    {
        var (client, _, _, _) = CreateClient();

        var response = await client.GetAsync("/api/daily/not-a-date/leaderboard");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
