using Castlink.Application.Ingestion;
using Castlink.Domain;
using Castlink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Castlink.Infrastructure.Ingestion;

internal sealed class SyncStateStore : ISyncStateStore
{
    private readonly CastlinkDbContext _dbContext;

    public SyncStateStore(CastlinkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var state = await _dbContext.SyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        return state?.Value;
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        var state = await _dbContext.SyncStates.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (state is null)
        {
            _dbContext.SyncStates.Add(new SyncState { Key = key, Value = value, UpdatedAt = now });
        }
        else
        {
            state.Value = value;
            state.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
