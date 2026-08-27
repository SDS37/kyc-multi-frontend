using System.Security.Claims;

namespace Kyc.Api.Application.Tenancy;

public sealed class HttpCurrentTenant(IHttpContextAccessor httpContextAccessor) : ICurrentTenant
{
    public const string TenantIdClaimType = "tenant_id";

    public Guid? TenantId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var raw = user.FindFirstValue(TenantIdClaimType);
            return Guid.TryParse(raw, out var tenantId) ? tenantId : null;
        }
    }
}
