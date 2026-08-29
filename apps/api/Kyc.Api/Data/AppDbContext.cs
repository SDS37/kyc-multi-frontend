using System.Linq.Expressions;
using System.Reflection;
using Kyc.Api.Application.Tenancy;
using Kyc.Api.Domain;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Documents;
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
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // jsonb is Postgres-only; leave default text for SQLite test host EnsureCreated.
        if (Database.IsNpgsql())
        {
            modelBuilder.Entity<Case>()
                .Property(c => c.FormData)
                .HasColumnType("jsonb");
        }

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
        // Fail closed: no JWT tenant ⇒ no tenant-scoped rows.
        // Cross-tenant reads (login) must use IgnoreQueryFilters() explicitly.
        Expression<Func<TEntity, bool>> filter =
            e => CurrentTenantId != null && e.TenantId == CurrentTenantId;

        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }
}
