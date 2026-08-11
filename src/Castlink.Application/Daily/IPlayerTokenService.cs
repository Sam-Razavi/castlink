namespace Castlink.Application.Daily;

/// <summary>
/// Signs and validates the anonymous player identity carried in the browser's localStorage (see
/// docs/PLAN.md Phase 4: "Anonymous player tokens (signed, localStorage)"). Deliberately not a full
/// auth framework — stakes are low, forging a token only lets someone claim a different anonymous
/// identity on a leaderboard, not a real security boundary.
/// </summary>
public interface IPlayerTokenService
{
    string Issue(Guid playerId, string displayName);

    bool TryValidate(string token, out Guid playerId, out string displayName);
}
