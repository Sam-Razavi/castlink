namespace Castlink.Application.Daily;

/// <summary>
/// One node of a submitted path, in the wire shape from docs/PLAN.md Phase 4:
/// <c>{path: [{personId, filmId}...]}</c>. <see cref="FilmId"/> is the credit connecting this
/// person to the *next* node in the list — the last node's <see cref="FilmId"/> is meaningless and
/// ignored by <see cref="IDailyPathValidator"/>.
/// </summary>
public sealed record DailyPathStepRecord(int PersonId, int? FilmId);
