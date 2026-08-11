using Castlink.Application.Graph;
using Castlink.Application.Ingestion;
using Castlink.Application.People;
using Castlink.Infrastructure.Configuration;
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

    public static IServiceCollection AddCastlinkInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<TmdbOptions>()
            .Bind(configuration.GetSection(TmdbOptions.SectionName));

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

        return services;
    }
}
