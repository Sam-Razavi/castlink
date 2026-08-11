using Castlink.Application.Daily;
using Castlink.Domain;
using Castlink.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Castlink.Api.Controllers;

[ApiController]
[Route("api/players")]
public sealed class PlayersController : ControllerBase
{
    private const int MaxDisplayNameLength = 100;

    private readonly IPlayerRepository _playerRepository;
    private readonly IPlayerTokenService _tokenService;

    public PlayersController(IPlayerRepository playerRepository, IPlayerTokenService tokenService)
    {
        _playerRepository = playerRepository;
        _tokenService = tokenService;
    }

    // Mints one anonymous identity per browser — the client calls this exactly once, the first
    // time it has no token in localStorage, then reuses the issued token on every subsequent
    // daily-challenge submission (see docs/PLAN.md Phase 4).
    [HttpPost]
    public async Task<ActionResult<CreatePlayerResponseDto>> Create([FromBody] CreatePlayerRequestDto request, CancellationToken cancellationToken)
    {
        var displayName = (request.DisplayName ?? string.Empty).Trim();
        if (displayName.Length == 0)
        {
            return BadRequest(new { reason = "DisplayNameRequired" });
        }

        if (displayName.Length > MaxDisplayNameLength)
        {
            displayName = displayName[..MaxDisplayNameLength];
        }

        var player = new Player
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _playerRepository.InsertAsync(player, cancellationToken);

        var token = _tokenService.Issue(player.Id, player.DisplayName);
        return Ok(new CreatePlayerResponseDto(player.Id, player.DisplayName, token));
    }
}
