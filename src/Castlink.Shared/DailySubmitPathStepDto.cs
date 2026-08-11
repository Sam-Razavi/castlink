namespace Castlink.Shared;

/// <summary>
/// One node of a player-submitted path: <c>{personId, filmId}</c>, per docs/PLAN.md Phase 4's wire
/// contract for <c>POST /api/daily/submit</c>. <see cref="FilmId"/> is the credit connecting this
/// person to the *next* node — the last node's <see cref="FilmId"/> is ignored by the server.
/// </summary>
public sealed record DailySubmitPathStepDto(int PersonId, int? FilmId);
