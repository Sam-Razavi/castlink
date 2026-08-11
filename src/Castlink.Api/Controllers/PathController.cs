using System.Diagnostics;
using Castlink.Application.Graph;
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

    private async Task<IReadOnlyList<PathLinkDto>> EnrichAsync(IReadOnlyList<PathLink> links, CancellationToken cancellationToken)
    {
        if (links.Count == 0)
        {
            return [];
        }

        var personIds = links.SelectMany(link => new[] { link.FromPersonId, link.ToPersonId }).Distinct().ToArray();
        var filmIds = links.Select(link => link.FilmId).Distinct().ToArray();

        var people = await _enrichmentRepository.GetPeopleAsync(personIds, cancellationToken);
        var films = await _enrichmentRepository.GetFilmsAsync(filmIds, cancellationToken);

        return links
            .Select(link => new PathLinkDto(
                FromPersonId: link.FromPersonId,
                FromPersonName: DisplayName(people, link.FromPersonId),
                FromProfilePath: people.GetValueOrDefault(link.FromPersonId)?.ProfilePath,
                FilmId: link.FilmId,
                FilmTitle: DisplayTitle(films, link.FilmId),
                FilmPosterPath: films.GetValueOrDefault(link.FilmId)?.PosterPath,
                ToPersonId: link.ToPersonId,
                ToPersonName: DisplayName(people, link.ToPersonId),
                ToProfilePath: people.GetValueOrDefault(link.ToPersonId)?.ProfilePath))
            .ToList();
    }

    // Falls back to a placeholder rather than throwing if the graph and the `people`/`films`
    // tables have drifted (e.g. the in-memory snapshot is older than the last ingestion write) —
    // a display glitch is a far better failure mode here than a 500.
    private static string DisplayName(IReadOnlyDictionary<int, PersonSummary> people, int personId) =>
        people.TryGetValue(personId, out var person) ? person.Name : $"#{personId}";

    private static string DisplayTitle(IReadOnlyDictionary<int, FilmSummary> films, int filmId) =>
        films.TryGetValue(filmId, out var film) ? film.Title : $"#{filmId}";
}
