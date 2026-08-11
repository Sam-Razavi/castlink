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

    public PeopleController(IPersonSearchRepository searchRepository)
    {
        _searchRepository = searchRepository;
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
}
