namespace Castlink.Shared;

public sealed record DailySubmitRequestDto(IReadOnlyList<DailySubmitPathStepDto> Path, int DurationMs);
