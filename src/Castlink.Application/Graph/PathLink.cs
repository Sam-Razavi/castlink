namespace Castlink.Application.Graph;

/// <summary>One hop of a path: two people connected by a shared film credit.</summary>
public sealed record PathLink(int FromPersonId, int FilmId, int ToPersonId);
