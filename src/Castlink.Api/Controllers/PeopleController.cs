using Castlink.Application.People;
using Castlink.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Castlink.Api.Controllers;

[ApiController]
[Route("api/people")]
public sealed class PeopleController : ControllerBase
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    private readonly IPersonSearchRepository _searchRepository;
    private readonly ISharedFilmsRepository _sharedFilmsRepository;

    public PeopleController(IPersonSearchRepository searchRepository, ISharedFilmsRepository sharedFilmsRepository)
    {
        _searchRepository = searchRepository;
        _sharedFilmsRepository = sharedFilmsRepository;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<PersonSearchResultDto>>> Search(
        [FromQuery] string? q, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Array.Empty<PersonSearchResultDto>());
        }

        // A missing/absurd limit shouldn't be able to force a full-table-ish scan.
        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var results = await _searchRepository.SearchAsync(q, effectiveLimit, cancellationToken);

        return Ok(results
            .Select(result => new PersonSearchResultDto(result.Id, result.Name, result.ProfilePath, result.Popularity))
            .ToList());
    }

    // Backs the daily-challenge chain-builder UI (Phase 4): the player picks the next actor, then
    // this resolves which real film actually connects them, so the submission's filmId is
    // something the player genuinely chose rather than something the server silently inferred.
    [HttpGet("{fromId:int}/shared-films/{toId:int}")]
    public async Task<ActionResult<IReadOnlyList<SharedFilmDto>>> SharedFilms(int fromId, int toId, CancellationToken cancellationToken)
    {
        var films = await _sharedFilmsRepository.GetSharedFilmsAsync(fromId, toId, cancellationToken);
        return Ok(films.Select(film => new SharedFilmDto(film.Id, film.Title, film.PosterPath)).ToList());
    }
}
