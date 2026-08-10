using Castlink.Application.Ingestion;

namespace Castlink.Application.Tests.Fakes;

internal sealed class FakeSyncStateStore : ISyncStateStore
{
    private readonly Dictionary<string, string> _values = [];

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(_values.GetValueOrDefault(key));

    public Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }
}
