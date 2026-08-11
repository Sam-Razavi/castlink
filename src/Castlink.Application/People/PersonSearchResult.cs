namespace Castlink.Application.People;

public sealed record PersonSearchResult(int Id, string Name, string? ProfilePath, double Popularity);
