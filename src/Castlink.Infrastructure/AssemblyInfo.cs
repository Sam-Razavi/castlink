using System.Runtime.CompilerServices;

// Lets Castlink.Infrastructure.Tests exercise internal types directly (TmdbClient, its DTOs, the
// resilience pipeline, PostgresIngestionWriter) without making them part of the public API.
[assembly: InternalsVisibleTo("Castlink.Infrastructure.Tests")]
