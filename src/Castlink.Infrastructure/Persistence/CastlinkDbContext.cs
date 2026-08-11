using Castlink.Domain;
using Microsoft.EntityFrameworkCore;

namespace Castlink.Infrastructure.Persistence;

public sealed class CastlinkDbContext : DbContext
{
    public CastlinkDbContext(DbContextOptions<CastlinkDbContext> options)
        : base(options)
    {
    }

    public DbSet<Person> People => Set<Person>();

    public DbSet<Film> Films => Set<Film>();

    public DbSet<Credit> Credits => Set<Credit>();

    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();

    public DbSet<SyncState> SyncStates => Set<SyncState>();

    public DbSet<DailyChallenge> DailyChallenges => Set<DailyChallenge>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<DailySubmission> DailySubmissions => Set<DailySubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Backs the GIN trigram index on people.name (see PersonConfiguration) — needed for
        // fuzzy actor-name search in Phase 3.
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CastlinkDbContext).Assembly);
    }
}
