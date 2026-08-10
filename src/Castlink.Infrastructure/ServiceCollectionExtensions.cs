using Castlink.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Castlink.Infrastructure;

/// <summary>
/// Composition root for infrastructure services, called from both
/// <c>Castlink.Api</c> and <c>Castlink.Ingestion</c> so the two hosts stay
/// wired up identically. EF Core's <c>DbContext</c>, the Redis connection
/// multiplexer, and the TMDB HTTP client are added here starting Phase 1 —
/// this phase only wires configuration binding.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCastlinkInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<TmdbOptions>()
            .Bind(configuration.GetSection(TmdbOptions.SectionName));

        return services;
    }
}
