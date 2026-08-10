namespace Castlink.Domain;

public enum IngestionRunStatus
{
    Running,
    Completed,
    Failed,
}

/// <summary>Operational record of a single ingestion batch (full seed or incremental sync).</summary>
public sealed class IngestionRun
{
    public Guid Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public IngestionRunStatus Status { get; set; }

    public int FilmsProcessed { get; set; }

    public int PeopleProcessed { get; set; }

    public string? Error { get; set; }
}
