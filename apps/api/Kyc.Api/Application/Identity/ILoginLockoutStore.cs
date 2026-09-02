namespace Kyc.Api.Application.Identity;

public interface ILoginLockoutStore
{
    bool IsLocked(string tenantSlug, string email, DateTimeOffset utcNow);
    void RecordFailure(string tenantSlug, string email, DateTimeOffset utcNow);
    void RecordSuccess(string tenantSlug, string email);
}
