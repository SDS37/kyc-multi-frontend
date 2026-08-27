using System.Security.Claims;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Data;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Tests;

public sealed class TenantIsolationTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private FakeCurrentTenant _currentTenant = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _currentTenant = new FakeCurrentTenant();

        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Tenant_A_cannot_read_tenant_B_users()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Seed writes (filters apply to queries, not inserts).
        _currentTenant.TenantId = null;
        await using (var seed = CreateDb())
        {
            seed.Tenants.AddRange(
                new Tenant { Id = tenantA, Name = "A", Slug = "tenant-a", IsActive = true, CreatedAt = now },
                new Tenant { Id = tenantB, Name = "B", Slug = "tenant-b", IsActive = true, CreatedAt = now });
            seed.Users.AddRange(
                new User
                {
                    Id = userAId,
                    TenantId = tenantA,
                    Email = "a@example.com",
                    PasswordHash = "hash-a",
                    Role = UserRole.TenantAdmin,
                    CreatedAt = now
                },
                new User
                {
                    Id = userBId,
                    TenantId = tenantB,
                    Email = "b@example.com",
                    PasswordHash = "hash-b",
                    Role = UserRole.Customer,
                    CreatedAt = now
                });
            await seed.SaveChangesAsync();
        }

        // Fail closed: no tenant context ⇒ no tenant-scoped rows.
        _currentTenant.TenantId = null;
        await using (var anon = CreateDb())
        {
            Assert.Empty(await anon.Users.AsNoTracking().ToListAsync());
        }

        // Act as tenant A — User stands in for cases until KYC-030 (same ITenantScoped filter).
        _currentTenant.TenantId = tenantA;
        await using (var asA = CreateDb())
        {
            var visible = await asA.Users.AsNoTracking().Select(u => u.Id).ToListAsync();
            Assert.Single(visible);
            Assert.Equal(userAId, visible[0]);
            Assert.Null(await asA.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userBId));
        }

        _currentTenant.TenantId = tenantB;
        await using (var asB = CreateDb())
        {
            var visible = await asB.Users.AsNoTracking().Select(u => u.Id).ToListAsync();
            Assert.Single(visible);
            Assert.Equal(userBId, visible[0]);
            Assert.Null(await asB.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userAId));
        }
    }

    [Fact]
    public void HttpCurrentTenant_reads_tenant_id_claim()
    {
        var tenantId = Guid.NewGuid();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(HttpCurrentTenant.TenantIdClaimType, tenantId.ToString())],
                    authenticationType: "Test"))
        };

        var accessor = new HttpContextAccessor { HttpContext = http };
        var current = new HttpCurrentTenant(accessor);

        Assert.Equal(tenantId, current.TenantId);
    }

    private AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options, _currentTenant);
    }
}
