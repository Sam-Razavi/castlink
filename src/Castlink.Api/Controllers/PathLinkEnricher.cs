using Castlink.Application.Graph;
using Castlink.Domain;
using Castlink.Shared;

namespace Castlink.Api.Controllers;

/// <summary>
/// Shared "turn a list of graph hops into display-ready DTOs" logic, used by both
/// <see cref="PathController"/> (a freshly-computed BFS result) and <see cref="DailyController"/>
/// (a persisted canonical path) — same enrichment repository, same DTO shape, same graceful
/// fallback if the graph and the people/films tables have drifted.
/// </summary>
internal static class PathLinkEnricher
{
    public static async Task<IReadOnlyList<PathLinkDto>> EnrichAsync(
        IPathEnrichmentRepository enrichmentRepository,
        IReadOnlyList<PathLinkRecord> links,
        CancellationToken cancellationToken)
    {
        if (links.Count == 0)
        {
            return [];
        }

        var personIds = links.SelectMany(link => new[] { link.FromPersonId, link.ToPersonId }).Distinct().ToArray();
        var filmIds = links.Select(link => link.FilmId).Distinct().ToArray();

        var people = await enrichmentRepository.GetPeopleAsync(personIds, cancellationToken);
        var films = await enrichmentRepository.GetFilmsAsync(filmIds, cancellationToken);

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
