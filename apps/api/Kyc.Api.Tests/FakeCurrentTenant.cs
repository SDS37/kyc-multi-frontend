using Kyc.Api.Application.Tenancy;

namespace Kyc.Api.Tests;

internal sealed class FakeCurrentTenant : ICurrentTenant
{
    public Guid? TenantId { get; set; }
}
