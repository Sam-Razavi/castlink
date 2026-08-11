using System.Net;
using System.Net.Http.Json;
using Castlink.Api.Tests.Fakes;
using Castlink.Application.Daily;
using Castlink.Application.Graph;
using Castlink.Shared;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Castlink.Api.Tests;

/// <summary>
/// Exercises <c>POST /api/players</c> through the real ASP.NET Core pipeline, including the real
/// <see cref="IPlayerTokenService"/> (<c>HmacPlayerTokenService</c>, using its documented
/// local-dev signing-key fallback) — only <see cref="IPlayerRepository"/> is a fixture. See
/// docs/PLAN.md Phase 4.
/// </summary>
public sealed class PlayersControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _baseFactory;

    public PlayersControllerTests(WebApplicationFactory<Program> baseFactory)
    {
        _baseFactory = baseFactory;
    }

    private (HttpClient Client, FakePlayerRepository PlayerRepository, WebApplicationFactory<Program> Factory) CreateClient()
    {
        var playerRepository = new FakePlayerRepository();
        var factory = _baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IGraphSnapshotSource>(new FakeGraphSnapshotSource([]));
                services.AddSingleton<IPlayerRepository>(playerRepository);
            }));

        return (factory.CreateClient(), playerRepository, factory);
    }

    [Fact]
    public async Task Creates_a_player_and_issues_a_token_that_validates_back_to_the_same_player()
    {
        var (client, playerRepository, factory) = CreateClient();

        var response = await client.PostAsJsonAsync("/api/players", new CreatePlayerRequestDto("Alice"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreatePlayerResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("Alice", body!.DisplayName);
        Assert.NotEqual(Guid.Empty, body.PlayerId);
        Assert.False(string.IsNullOrWhiteSpace(body.Token));

        var player = Assert.Single(playerRepository.InsertedPlayers);
        Assert.Equal(body.PlayerId, player.Id);

        // Resolved from the *customized* factory, not the raw IClassFixture one — the base factory
        // has no fixtures wired in, so touching its .Services would build the real host and try to
        // hit Postgres for the startup graph load, exactly the thing every other test here avoids.
        var tokenService = factory.Services.GetRequiredService<IPlayerTokenService>();
        Assert.True(tokenService.TryValidate(body.Token, out var validatedPlayerId, out var validatedDisplayName));
        Assert.Equal(body.PlayerId, validatedPlayerId);
        Assert.Equal("Alice", validatedDisplayName);
    }

    [Fact]
    public async Task Rejects_a_blank_display_name()
    {
        var (client, _, _) = CreateClient();

        var response = await client.PostAsJsonAsync("/api/players", new CreatePlayerRequestDto("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Truncates_a_display_name_longer_than_the_stored_column_allows()
    {
        var (client, _, _) = CreateClient();
        var longName = new string('a', 250);

        var response = await client.PostAsJsonAsync("/api/players", new CreatePlayerRequestDto(longName));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreatePlayerResponseDto>();
        Assert.Equal(100, body!.DisplayName.Length);
    }
}
