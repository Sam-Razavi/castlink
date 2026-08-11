namespace Castlink.Shared;

public sealed record CreatePlayerResponseDto(Guid PlayerId, string DisplayName, string Token);
