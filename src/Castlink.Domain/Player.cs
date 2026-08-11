namespace Castlink.Domain;

/// <summary>
/// An anonymous player identity, minted by <c>POST /api/players</c> and carried thereafter as a
/// signed token in the browser's localStorage — no login, no email, no password (see docs/PLAN.md
/// Phase 4).
/// </summary>
public sealed class Player
{
    public Guid Id { get; set; }

    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
