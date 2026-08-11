namespace Castlink.Domain;

/// <summary>
/// One hop in a path through the actor/film graph: <see cref="FromPersonId"/> and
/// <see cref="ToPersonId"/> share a credit on <see cref="FilmId"/>. Shape-identical to
/// <c>Castlink.Application.Graph.PathLink</c> by design, but Domain has zero project references
/// and can't depend on Application — this is the persisted (jsonb) equivalent, used by
/// <see cref="DailyChallenge.CanonicalPath"/> and <see cref="DailySubmission.Path"/>.
/// </summary>
public sealed record PathLinkRecord(int FromPersonId, int FilmId, int ToPersonId);
