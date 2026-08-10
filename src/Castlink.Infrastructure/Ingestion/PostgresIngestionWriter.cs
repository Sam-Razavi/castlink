using Castlink.Application.Ingestion;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Castlink.Infrastructure.Ingestion;

/// <summary>
/// Writes a batch of fetched films to Postgres via staged bulk COPY + <c>ON CONFLICT DO UPDATE</c>,
/// all inside one transaction. Idempotent by construction: re-running the same batch converges to
/// the same rows rather than accumulating duplicates, and a failure anywhere rolls the whole batch
/// back — there is no partial-write state to clean up on a retried run. See docs/PLAN.md Phase 1.
/// </summary>
internal sealed class PostgresIngestionWriter : IIngestionWriter
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresIngestionWriter> _logger;

    public PostgresIngestionWriter(NpgsqlDataSource dataSource, ILogger<PostgresIngestionWriter> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<IngestionWriteResult> UpsertBatchAsync(IReadOnlyList<MovieIngestionRecord> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return new IngestionWriteResult(0, 0);
        }

        // A person can appear in more than one film within the same batch — staging_people has a
        // primary key on id, so it must see each person once, not once per film they're credited on.
        var distinctPeople = batch
            .SelectMany(film => film.Cast)
            .GroupBy(cast => cast.PersonId)
            .Select(group => group.First())
            .ToList();
        var touchedFilmIds = batch.Select(film => film.FilmId).ToArray();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await CreateStagingTablesAsync(connection, cancellationToken);
        await CopyFilmsAsync(connection, batch, cancellationToken);
        await CopyPeopleAsync(connection, distinctPeople, cancellationToken);
        await CopyCreditsAsync(connection, batch, cancellationToken);

        await MergeFilmsAsync(connection, cancellationToken);
        await MergePeopleAsync(connection, cancellationToken);
        await DeleteStaleCreditsAsync(connection, touchedFilmIds, cancellationToken);
        await MergeCreditsAsync(connection, cancellationToken);
        await RecomputeCreditCountsAsync(connection, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Upserted {FilmCount} films and {PersonCount} people", batch.Count, distinctPeople.Count);

        return new IngestionWriteResult(batch.Count, distinctPeople.Count);
    }

    private static async Task CreateStagingTablesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE TEMP TABLE staging_films (
                id integer PRIMARY KEY,
                title character varying(500) NOT NULL,
                release_year integer,
                popularity double precision NOT NULL,
                vote_count integer NOT NULL,
                poster_path character varying(500)
            ) ON COMMIT DROP;

            CREATE TEMP TABLE staging_people (
                id integer PRIMARY KEY,
                name character varying(500) NOT NULL,
                popularity double precision NOT NULL,
                profile_path character varying(500),
                known_for_department character varying(100)
            ) ON COMMIT DROP;

            CREATE TEMP TABLE staging_credits (
                film_id integer NOT NULL,
                person_id integer NOT NULL,
                billing_order integer NOT NULL,
                character character varying(500),
                PRIMARY KEY (film_id, person_id)
            ) ON COMMIT DROP;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CopyFilmsAsync(NpgsqlConnection connection, IReadOnlyList<MovieIngestionRecord> batch, CancellationToken cancellationToken)
    {
        const string copySql = "COPY staging_films (id, title, release_year, popularity, vote_count, poster_path) FROM STDIN (FORMAT BINARY)";
        await using var writer = await connection.BeginBinaryImportAsync(copySql, cancellationToken);

        foreach (var film in batch)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(film.FilmId, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(film.Title, NpgsqlDbType.Varchar, cancellationToken);
            await WriteNullableAsync(writer, film.ReleaseYear, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(film.Popularity, NpgsqlDbType.Double, cancellationToken);
            await writer.WriteAsync(film.VoteCount, NpgsqlDbType.Integer, cancellationToken);
            await WriteNullableAsync(writer, film.PosterPath, NpgsqlDbType.Varchar, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static async Task CopyPeopleAsync(NpgsqlConnection connection, IReadOnlyList<CastCreditRecord> people, CancellationToken cancellationToken)
    {
        const string copySql = "COPY staging_people (id, name, popularity, profile_path, known_for_department) FROM STDIN (FORMAT BINARY)";
        await using var writer = await connection.BeginBinaryImportAsync(copySql, cancellationToken);

        foreach (var person in people)
        {
            await writer.StartRowAsync(cancellationToken);
            await writer.WriteAsync(person.PersonId, NpgsqlDbType.Integer, cancellationToken);
            await writer.WriteAsync(person.Name, NpgsqlDbType.Varchar, cancellationToken);
            await writer.WriteAsync(person.Popularity, NpgsqlDbType.Double, cancellationToken);
            await WriteNullableAsync(writer, person.ProfilePath, NpgsqlDbType.Varchar, cancellationToken);
            await WriteNullableAsync(writer, person.KnownForDepartment, NpgsqlDbType.Varchar, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static async Task CopyCreditsAsync(NpgsqlConnection connection, IReadOnlyList<MovieIngestionRecord> batch, CancellationToken cancellationToken)
    {
        const string copySql = "COPY staging_credits (film_id, person_id, billing_order, character) FROM STDIN (FORMAT BINARY)";
        await using var writer = await connection.BeginBinaryImportAsync(copySql, cancellationToken);

        foreach (var film in batch)
        {
            foreach (var cast in film.Cast)
            {
                await writer.StartRowAsync(cancellationToken);
                await writer.WriteAsync(film.FilmId, NpgsqlDbType.Integer, cancellationToken);
                await writer.WriteAsync(cast.PersonId, NpgsqlDbType.Integer, cancellationToken);
                await writer.WriteAsync(cast.BillingOrder, NpgsqlDbType.Integer, cancellationToken);
                await WriteNullableAsync(writer, cast.Character, NpgsqlDbType.Varchar, cancellationToken);
            }
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private static Task WriteNullableAsync(NpgsqlBinaryImporter writer, object? value, NpgsqlDbType type, CancellationToken cancellationToken) =>
        value is null ? writer.WriteNullAsync(cancellationToken) : writer.WriteAsync(value, type, cancellationToken);

    private static async Task MergeFilmsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO films (id, title, release_year, popularity, vote_count, poster_path, last_synced_at)
            SELECT id, title, release_year, popularity, vote_count, poster_path, now()
            FROM staging_films
            ON CONFLICT (id) DO UPDATE SET
                title = EXCLUDED.title,
                release_year = EXCLUDED.release_year,
                popularity = EXCLUDED.popularity,
                vote_count = EXCLUDED.vote_count,
                poster_path = EXCLUDED.poster_path,
                last_synced_at = EXCLUDED.last_synced_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MergePeopleAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        // credit_count is deliberately not set here (existing rows keep their current value; new
        // rows start at 0) — RecomputeCreditCountsAsync fixes it up afterward from the real table.
        const string sql = """
            INSERT INTO people (id, name, popularity, profile_path, known_for_department, credit_count, last_synced_at)
            SELECT id, name, popularity, profile_path, known_for_department, 0, now()
            FROM staging_people
            ON CONFLICT (id) DO UPDATE SET
                name = EXCLUDED.name,
                popularity = EXCLUDED.popularity,
                profile_path = EXCLUDED.profile_path,
                known_for_department = EXCLUDED.known_for_department,
                last_synced_at = EXCLUDED.last_synced_at;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DeleteStaleCreditsAsync(NpgsqlConnection connection, int[] touchedFilmIds, CancellationToken cancellationToken)
    {
        // A re-fetched film's cast is authoritative for this batch — any existing credit row for
        // one of these films that isn't in the new data means that cast member was removed
        // upstream, so it's deleted here rather than left to accumulate forever.
        const string sql = """
            DELETE FROM credits
            WHERE film_id = ANY(@touchedFilmIds)
              AND NOT EXISTS (
                  SELECT 1 FROM staging_credits sc
                  WHERE sc.film_id = credits.film_id AND sc.person_id = credits.person_id
              );
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("touchedFilmIds", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = touchedFilmIds });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MergeCreditsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO credits (film_id, person_id, billing_order, character)
            SELECT film_id, person_id, billing_order, character
            FROM staging_credits
            ON CONFLICT (film_id, person_id) DO UPDATE SET
                billing_order = EXCLUDED.billing_order,
                character = EXCLUDED.character;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RecomputeCreditCountsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        // Recomputed against the real credits table (not just this batch) for every person touched
        // by this batch — credit_count is a total, not a per-batch delta.
        const string sql = """
            UPDATE people p
            SET credit_count = (SELECT COUNT(*) FROM credits c WHERE c.person_id = p.id)
            FROM staging_people sp
            WHERE p.id = sp.id;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
