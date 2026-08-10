using Castlink.Application.Ingestion;
using Castlink.Infrastructure.Configuration;
using Castlink.Infrastructure.Ingestion;
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

        var connectionString = configuration.GetConnectionString("Postgres") ?? LocalDevConnectionStringFallback;
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

        return services;
    }
}
