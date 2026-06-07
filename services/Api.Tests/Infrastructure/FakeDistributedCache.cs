using Microsoft.Extensions.Caching.Distributed;

namespace Api.Tests.Infrastructure;

internal sealed class FakeDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _store = [];
    private readonly object _sync = new();

    public byte[]? Get(string key)
    {
        lock (_sync)
        {
            return _store.TryGetValue(key, out var value) ? value : null;
        }
    }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        lock (_sync)
        {
            _store[key] = value;
        }
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = default) =>
        Task.CompletedTask;

    public void Remove(string key)
    {
        lock (_sync)
        {
            _store.Remove(key);
        }
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }
}
