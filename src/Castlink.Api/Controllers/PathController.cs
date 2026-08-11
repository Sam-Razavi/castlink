using System.Diagnostics;
using Castlink.Application.Graph;
using Castlink.Domain;
using Castlink.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Castlink.Api.Controllers;

[ApiController]
[Route("api/path")]
public sealed class PathController : ControllerBase
{
    private readonly IPathFinder _pathFinder;
    private readonly GraphSnapshotProvider _graphSnapshotProvider;
    private readonly IPathEnrichmentRepository _enrichmentRepository;

    public PathController(
        IPathFinder pathFinder,
        GraphSnapshotProvider graphSnapshotProvider,
        IPathEnrichmentRepository enrichmentRepository)
    {
        _pathFinder = pathFinder;
        _graphSnapshotProvider = graphSnapshotProvider;
        _enrichmentRepository = enrichmentRepository;
    }

    [HttpPost]
    public async Task<ActionResult<PathResponse>> Post([FromBody] PathRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = _pathFinder.FindPath(_graphSnapshotProvider.Current, request.FromPersonId, request.ToPersonId);
        stopwatch.Stop();

        if (result.Outcome != PathOutcome.Found)
        {
            return NotFound(new { reason = result.Outcome.ToString() });
        }

        var links = await EnrichAsync(result.Links, cancellationToken);
        return Ok(new PathResponse(result.Degrees, links, stopwatch.Elapsed.TotalMilliseconds));
    }

    // Shared with DailyController (Phase 4), which enriches a persisted canonical path the same
    // way — see PathLinkEnricher.
    private Task<IReadOnlyList<PathLinkDto>> EnrichAsync(IReadOnlyList<PathLink> links, CancellationToken cancellationToken) =>
        PathLinkEnricher.EnrichAsync(
            _enrichmentRepository,
            links.Select(link => new PathLinkRecord(link.FromPersonId, link.FilmId, link.ToPersonId)).ToList(),
            cancellationToken);
}
