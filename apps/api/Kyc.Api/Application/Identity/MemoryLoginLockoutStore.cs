using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Kyc.Api.Application.Identity;

public sealed class MemoryLoginLockoutStore(IMemoryCache cache, IOptions<LoginLockoutOptions> options) : ILoginLockoutStore
{
    private sealed class Counter
    {
        public int Failures { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }

    public bool IsLocked(string tenantSlug, string email, DateTimeOffset utcNow)
    {
        var entry = cache.Get<Counter>(Key(tenantSlug, email));
        return entry?.LockedUntil is { } until && until > utcNow;
    }

    public void RecordFailure(string tenantSlug, string email, DateTimeOffset utcNow)
    {
        var key = Key(tenantSlug, email);
        var settings = options.Value;
        var lockoutFor = TimeSpan.FromMinutes(settings.DurationMinutes);
        var entry = cache.Get<Counter>(key) ?? new Counter();
        if (entry.LockedUntil is { } until && until > utcNow)
        {
            return;
        }

        if (entry.LockedUntil is { } expired && expired <= utcNow)
        {
            entry.Failures = 0;
            entry.LockedUntil = null;
        }

        entry.Failures++;
        if (entry.Failures >= settings.MaxFailedAttempts)
        {
            entry.LockedUntil = utcNow.Add(lockoutFor);
        }

        cache.Set(key, entry, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lockoutFor + TimeSpan.FromMinutes(1)
        });
    }

    public void RecordSuccess(string tenantSlug, string email) => cache.Remove(Key(tenantSlug, email));

    private static string Key(string tenantSlug, string email) =>
        $"lockout:{tenantSlug.Trim().ToLowerInvariant()}\n{email.Trim().ToLowerInvariant()}";
}
