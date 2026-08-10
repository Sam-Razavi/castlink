namespace Castlink.Shared;

public sealed record PathResponse(int Degrees, IReadOnlyList<PathLinkDto> Links, double ComputedInMs);
