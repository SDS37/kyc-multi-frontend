using Kyc.Api.Application.Documents;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kyc.Api.Tests;

public sealed class DemoSeedTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private readonly FakeCurrentTenant _currentTenant = new();
    private readonly InMemoryObjectStorage _storage = new();
    private readonly PasswordHasher<User> _hasher = new();

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();

    [Fact]
    public async Task Seed_creates_two_tenants_all_roles_and_every_case_status()
    {
        await using var db = CreateDb();
        await CreateSeeder(db).SeedAsync();

        var tenants = await db.Tenants.AsNoTracking().OrderBy(t => t.Slug).ToListAsync();
        Assert.Equal(["acme", "globex"], tenants.ConvertAll(t => t.Slug));

        foreach (var spec in DemoSeedCatalog.Tenants)
        {
            var tenant = tenants.Single(t => t.Slug == spec.Slug);
            var users = await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.TenantId == tenant.Id)
                .ToListAsync();
            Assert.Equal(3, users.Count);
            Assert.Contains(users, u => u.Email == spec.AdminEmail && u.Role == UserRole.TenantAdmin);
            Assert.Contains(users, u => u.Email == spec.ReviewerEmail && u.Role == UserRole.Reviewer);
            Assert.Contains(users, u => u.Email == spec.CustomerEmail && u.Role == UserRole.Customer);

            var customer = users.Single(u => u.Role == UserRole.Customer);
            var result = _hasher.VerifyHashedPassword(customer, customer.PasswordHash, DemoSeedCatalog.Password);
            Assert.Equal(PasswordVerificationResult.Success, result);

            var cases = await db.Cases.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.TenantId == tenant.Id)
                .ToListAsync();
            Assert.Equal(5, cases.Count);
            Assert.Contains(cases, c => c.Title == DemoSeedCatalog.DraftTitle && c.Status == CaseStatus.Draft);
            Assert.Contains(cases, c => c.Title == DemoSeedCatalog.SubmittedTitle && c.Status == CaseStatus.Submitted);
            Assert.Contains(cases, c => c.Title == DemoSeedCatalog.InReviewTitle && c.Status == CaseStatus.InReview);
            Assert.Contains(cases, c => c.Title == DemoSeedCatalog.ApprovedTitle && c.Status == CaseStatus.Approved);
            Assert.Contains(cases, c => c.Title == DemoSeedCatalog.RejectedTitle && c.Status == CaseStatus.Rejected);

            var documents = await db.Documents.IgnoreQueryFilters().AsNoTracking()
                .Where(d => d.TenantId == tenant.Id)
                .ToListAsync();
            Assert.Equal(4, documents.Count);
            Assert.All(documents, d => Assert.True(_storage.Contains(d.StorageKey)));
        }
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        await using (var first = CreateDb())
        {
            await CreateSeeder(first).SeedAsync();
        }

        await using var db = CreateDb();
        await CreateSeeder(db).SeedAsync();
        await CreateSeeder(db).SeedAsync();

        Assert.Equal(2, await db.Tenants.CountAsync());
        Assert.Equal(6, await db.Users.IgnoreQueryFilters().CountAsync());
        Assert.Equal(10, await db.Cases.IgnoreQueryFilters().CountAsync());
        Assert.Equal(8, await db.Documents.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Seed_fills_missing_roles_on_existing_acme_without_resetting_admin_password()
    {
        var now = DateTimeOffset.UtcNow;
        var tenantId = Guid.NewGuid();
        const string existingHash = "existing-hash-do-not-replace";
        await using (var setup = CreateDb())
        {
            setup.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Acme",
                Slug = "acme",
                IsActive = true,
                CreatedAt = now
            });
            setup.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Email = "admin@acme.example",
                PasswordHash = existingHash,
                Role = UserRole.TenantAdmin,
                CreatedAt = now
            });
            await setup.SaveChangesAsync();
        }

        await using var db = CreateDb();
        await CreateSeeder(db).SeedAsync();

        var admin = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(u => u.TenantId == tenantId && u.Email == "admin@acme.example");
        Assert.Equal(existingHash, admin.PasswordHash);
        Assert.Equal(3, await db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == tenantId));
        Assert.Equal(5, await db.Cases.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId));
        Assert.True(await db.Tenants.AnyAsync(t => t.Slug == "globex"));
    }

    [Fact]
    public async Task Seed_second_run_does_not_log_information()
    {
        var logger = new ListLogger<DemoSeedService>();
        await using (var first = CreateDb())
        {
            await CreateSeeder(first, logger).SeedAsync();
        }

        Assert.Contains(logger.Messages, m => m.Contains("Demo seed completed", StringComparison.Ordinal));
        logger.Messages.Clear();
        logger.Levels.Clear();

        await using var db = CreateDb();
        await CreateSeeder(db, logger).SeedAsync();

        Assert.DoesNotContain(LogLevel.Information, logger.Levels);
        Assert.Contains(logger.Messages, m => m.Contains("Demo seed unchanged", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Seed_repairs_missing_object_for_existing_document_row()
    {
        await using (var first = CreateDb())
        {
            await CreateSeeder(first).SeedAsync();
        }

        await using var db = CreateDb();
        var keys = await db.Documents.IgnoreQueryFilters().AsNoTracking()
            .Select(d => d.StorageKey)
            .ToListAsync();
        Assert.NotEmpty(keys);
        foreach (var key in keys)
        {
            await _storage.DeleteAsync(key);
            Assert.False(_storage.Contains(key));
        }

        await CreateSeeder(db).SeedAsync();

        Assert.Equal(8, await db.Documents.IgnoreQueryFilters().CountAsync());
        Assert.All(keys, key => Assert.True(_storage.Contains(key)));
    }

    private DemoSeedService CreateSeeder(AppDbContext db, ILogger<DemoSeedService>? logger = null) =>
        new(db, _hasher, _storage, logger ?? NullLogger<DemoSeedService>.Instance);

    private AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options, _currentTenant);
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
