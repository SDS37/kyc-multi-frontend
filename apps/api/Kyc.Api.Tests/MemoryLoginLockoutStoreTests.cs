using Kyc.Api.Application.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Kyc.Api.Tests;

public sealed class MemoryLoginLockoutStoreTests
{
    [Fact]
    public void Concurrent_failures_are_not_lost()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new LoginLockoutOptions
        {
            MaxFailedAttempts = 40,
            DurationMinutes = 15
        });
        var store = new MemoryLoginLockoutStore(cache, options);
        var now = DateTimeOffset.UtcNow;

        Parallel.For(0, 40, _ => store.RecordFailure("acme", "a@example.com", now));

        Assert.True(store.IsLocked("acme", "a@example.com", now));
    }
}
