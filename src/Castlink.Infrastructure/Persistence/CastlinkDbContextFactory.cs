using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Castlink.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` scaffold a model without a running host or a live database
/// connection — migration generation only needs a syntactically valid connection string, never an
/// actual connection. The fallback below is the documented local-dev default already committed in
/// docker-compose.yml, not a secret; real environments override it via
/// <c>ConnectionStrings__Postgres</c>.
/// </summary>
public sealed class CastlinkDbContextFactory : IDesignTimeDbContextFactory<CastlinkDbContext>
{
    public CastlinkDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=castlink;Username=castlink;Password=castlink_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<CastlinkDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new CastlinkDbContext(optionsBuilder.Options);
    }
}
