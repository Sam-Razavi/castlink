using Castlink.Application.Ingestion;
using Castlink.Infrastructure.Ingestion;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Castlink.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises <see cref="PostgresIngestionWriter"/> against a real Postgres (via Testcontainers) —
/// the one thing that can't be faked, since idempotency is a property of the actual SQL, not of
/// C# logic. Requires Docker; not runnable in the sandbox this was written in (see docs/PLAN.md
/// Phase 1) but runs in CI and locally wherever Docker is available.
/// </summary>
public sealed class IngestionIdempotencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private CastlinkDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CastlinkDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new CastlinkDbContext(options);
    }

    private PostgresIngestionWriter CreateWriter()
    {
        var dataSource = new NpgsqlDataSourceBuilder(_container.GetConnectionString()).Build();
        return new PostgresIngestionWriter(dataSource, NullLogger<PostgresIngestionWriter>.Instance);
    }

    private static MovieIngestionRecord Movie(int filmId, params (int PersonId, string Name)[] cast) =>
        new(
            FilmId: filmId,
            Title: $"Film {filmId}",
            ReleaseYear: 2020,
            Popularity: 10.5,
            VoteCount: 250,
            PosterPath: "/poster.jpg",
            Cast: [.. cast.Select((c, i) => new CastCreditRecord(c.PersonId, c.Name, 5.0, null, "Acting", i, "Character"))]);

    [Fact]
    public async Task Running_the_same_batch_twice_does_not_duplicate_rows_and_advances_last_synced_at()
    {
        var writer = CreateWriter();
        var batch = new[] { Movie(1, (100, "Actor One"), (101, "Actor Two")) };

        await writer.UpsertBatchAsync(batch, CancellationToken.None);

        await using var db1 = CreateDbContext();
        var firstSyncedAt = await db1.Films.AsNoTracking().Where(f => f.Id == 1).Select(f => f.LastSyncedAt).SingleAsync();
        var firstFilmCount = await db1.Films.CountAsync();
        var firstPersonCount = await db1.People.CountAsync();
        var firstCreditCount = await db1.Credits.CountAsync();

        // Real time must actually advance between the two writes for last_synced_at to prove
        // anything — Postgres now() has enough resolution that this reliably differs.
        await Task.Delay(50);

        await writer.UpsertBatchAsync(batch, CancellationToken.None);

        await using var db2 = CreateDbContext();
        var secondSyncedAt = await db2.Films.AsNoTracking().Where(f => f.Id == 1).Select(f => f.LastSyncedAt).SingleAsync();
        var secondFilmCount = await db2.Films.CountAsync();
        var secondPersonCount = await db2.People.CountAsync();
        var secondCreditCount = await db2.Credits.CountAsync();

        Assert.Equal(firstFilmCount, secondFilmCount);
        Assert.Equal(firstPersonCount, secondPersonCount);
        Assert.Equal(firstCreditCount, secondCreditCount);
        Assert.True(secondSyncedAt > firstSyncedAt, "a re-run should still refresh last_synced_at, not silently no-op");
    }

    [Fact]
    public async Task A_cast_member_dropped_from_the_second_fetch_is_removed_from_credits_not_just_left_stale()
    {
        var writer = CreateWriter();

        await writer.UpsertBatchAsync([Movie(2, (200, "Stays"), (201, "Leaves"))], CancellationToken.None);
        await writer.UpsertBatchAsync([Movie(2, (200, "Stays"))], CancellationToken.None);

        await using var db = CreateDbContext();
        var creditsForFilm2 = await db.Credits.AsNoTracking().Where(c => c.FilmId == 2).ToListAsync();
        Assert.Single(creditsForFilm2);
        Assert.Equal(200, creditsForFilm2[0].PersonId);

        // The removed person's own row isn't deleted (they may have other credits elsewhere) —
        // only the stale credit edge is. Their credit_count reflects the removal, though.
        var leavingPerson = await db.People.AsNoTracking().SingleAsync(p => p.Id == 201);
        Assert.Equal(0, leavingPerson.CreditCount);
    }

    [Fact]
    public async Task An_empty_batch_is_a_no_op_not_an_error()
    {
        var writer = CreateWriter();

        var result = await writer.UpsertBatchAsync([], CancellationToken.None);

        Assert.Equal(new IngestionWriteResult(0, 0), result);
    }
}
