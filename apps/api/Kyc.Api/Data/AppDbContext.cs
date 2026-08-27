using System.Linq.Expressions;
using System.Reflection;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Domain;
using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kyc.Api.Data;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenant currentTenant) : DbContext(options)
{
    /// <summary>
    /// Used by global query filters. Must be an instance property so EF re-evaluates per context.
    /// </summary>
    public Guid? CurrentTenantId => currentTenant.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(AppDbContext)
                .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, [modelBuilder]);
        }

        base.OnModelCreating(modelBuilder);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped
    {
        // Null CurrentTenantId = public/system path (register/login): no tenant filter.
        // Authenticated requests only see rows for the JWT tenant.
        Expression<Func<TEntity, bool>> filter =
            e => CurrentTenantId == null || e.TenantId == CurrentTenantId;

        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }
}
