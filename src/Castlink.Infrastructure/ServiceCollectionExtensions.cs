using Castlink.Application.Daily;
using Castlink.Application.Graph;
using Castlink.Application.Ingestion;
using Castlink.Application.People;
using Castlink.Infrastructure.Configuration;
using Castlink.Infrastructure.Daily;
using Castlink.Infrastructure.Graph;
using Castlink.Infrastructure.Ingestion;
using Castlink.Infrastructure.People;
using Castlink.Infrastructure.Persistence;
using Castlink.Infrastructure.Tmdb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using StackExchange.Redis;

namespace Castlink.Infrastructure;

/// <summary>
/// Composition root for infrastructure services, called from both <c>Castlink.Api</c> and
/// <c>Castlink.Ingestion</c> so the two hosts stay wired up identically.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Local-dev fallback connection string — the documented docker-compose default, not a secret.
    /// Real environments always set <c>ConnectionStrings__Postgres</c> (Azure) or configure user
    /// secrets (local); this only keeps `dotnet run` working against a freshly-started
    /// `docker compose up` with zero extra setup.
    /// </summary>
    private const string LocalDevConnectionStringFallback =
        "Host=localhost;Port=5432;Database=castlink;Username=castlink;Password=castlink_dev_password";

    /// <summary>Same story as <see cref="LocalDevConnectionStringFallback"/>, for the unauthenticated
    /// local Redis in docker-compose.yml.</summary>
    private const string LocalDevRedisConnectionStringFallback = "localhost:6379";

    public static IServiceCollection AddCastlinkInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<TmdbOptions>()
            .Bind(configuration.GetSection(TmdbOptions.SectionName));

        services
            .AddOptions<DailyChallengeOptions>()
            .Bind(configuration.GetSection(DailyChallengeOptions.SectionName));

        services
            .AddOptions<PlayerTokenOptions>()
            .Bind(configuration.GetSection(PlayerTokenOptions.SectionName));

        var configuredConnectionString = configuration.GetConnectionString("Postgres");
        // GetConnectionString returns "" (not null) when appsettings.json defines the key with an
        // empty placeholder value — which it does by design (see the appsettings.json comment on
        // this key) — so a plain `?? fallback` never actually triggers. Treat blank the same as
        // absent, or the local-dev fallback below is dead code.
        var connectionString = string.IsNullOrWhiteSpace(configuredConnectionString)
            ? LocalDevConnectionStringFallback
            : configuredConnectionString;
        var dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
        services.AddSingleton(dataSource);

        services.AddDbContext<CastlinkDbContext>(options => options.UseNpgsql(dataSource));

        var configuredRedisConnectionString = configuration.GetConnectionString("Redis");
        // Same "blank means absent" gotcha as Postgres above — GetConnectionString returns "" for
        // an explicitly-empty appsettings.json key, not null.
        var redisConnectionString = string.IsNullOrWhiteSpace(configuredRedisConnectionString)
            ? LocalDevRedisConnectionStringFallback
            : configuredRedisConnectionString;
        // Lazy + shared with AddStackExchangeRedisCache's ConnectionMultiplexerFactory below so the
        // process opens exactly one physical connection to Redis, not two — easy to miss since
        // AddStackExchangeRedisCache is happy to open its own if given a bare connection string
        // instead of a factory.
        var redisMultiplexer = new Lazy<IConnectionMultiplexer>(
            () => ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddSingleton(_ => redisMultiplexer.Value);
        services.AddStackExchangeRedisCache(options =>
        {
            options.InstanceName = "castlink:";
            options.ConnectionMultiplexerFactory = () => Task.FromResult(redisMultiplexer.Value);
        });

        services
            .AddHttpClient<ITmdbClient, TmdbClient>("Tmdb", (sp, client) =>
            {
                var tmdbOptions = sp.GetRequiredService<IOptions<TmdbOptions>>().Value;
                client.BaseAddress = new Uri(tmdbOptions.BaseUrl);
            })
            .AddHttpMessageHandler(sp =>
            {
                var tmdbOptions = sp.GetRequiredService<IOptions<TmdbOptions>>().Value;
                return new TmdbResilienceHandler(tmdbOptions);
            });

        services.AddScoped<IIngestionWriter, PostgresIngestionWriter>();
        services.AddScoped<ISyncStateStore, SyncStateStore>();
        services.AddScoped<IIngestionRunTracker, EfIngestionRunTracker>();
        services.AddScoped<IngestionService>();

        services.AddScoped<IGraphSnapshotSource, PostgresGraphSnapshotSource>();
        services.AddScoped<IPathEnrichmentRepository, EfPathEnrichmentRepository>();
        services.AddSingleton<GraphSnapshotProvider>();
        services.AddSingleton<IPathFinder, BidirectionalPathFinder>();

        services.AddScoped<IPersonSearchRepository, EfPersonSearchRepository>();
        services.AddScoped<ISharedFilmsRepository, EfSharedFilmsRepository>();

        // Daily challenge (docs/PLAN.md Phase 4). Registered Scoped throughout except where a type
        // has no scoped dependency of its own (DailyPathValidator is pure; the Redis-backed pieces
        // only need the singleton IConnectionMultiplexer) — those are Singleton since nothing about
        // them is per-request.
        services.AddScoped<IDailyChallengePool, EfDailyChallengePool>();
        services.AddScoped<DailyChallengeGenerator>();
        services.AddScoped<IDailyChallengeRepository, EfDailyChallengeRepository>();
        services.AddScoped<DailyChallengeService>();

        services.AddScoped<ICreditLookupRepository, EfCreditLookupRepository>();
        services.AddSingleton<IDailyPathValidator, DailyPathValidator>();
        services.AddSingleton<IDailySubmissionRateLimiter, RedisDailySubmissionRateLimiter>();
        services.AddSingleton<ILeaderboardStore, RedisLeaderboardStore>();
        services.AddScoped<IDailySubmissionRepository, EfDailySubmissionRepository>();
        services.AddScoped<DailySubmissionService>();

        services.AddSingleton<IPlayerTokenService, HmacPlayerTokenService>();
        services.AddScoped<IPlayerRepository, EfPlayerRepository>();

        return services;
    }
}
