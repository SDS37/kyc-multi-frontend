using System.Data.Common;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;
using Kyc.Api.Data;
using Kyc.Api.Domain.Audit;
using Kyc.Api.Domain.Cases;
using Kyc.Api.Domain.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Kyc.Api.Tests;

/// <summary>
/// What the next <c>cases</c> update should do before <c>submitCase</c> persist (KYC-111).
/// Concurrent HTTP is not used: two in-flight requests share one SQLite connection.
/// </summary>
public enum SubmitPersistRace
{
    InvalidFormData,
    ValidFormData,
    LeaveDraft,
    DeleteRow
}

public sealed class SubmitCaseRaceState
{
    public Guid CaseId { get; set; }

    public bool Armed { get; set; }

    public SubmitPersistRace Flip { get; set; } = SubmitPersistRace.InvalidFormData;

    public string NewerFormData { get; set; } = """{"fullName":"Only name"}""";

    /// <summary>Sentinel <c>SubmittedAt</c> when the flip leaves Draft, so a winning submit is visible.</summary>
    public DateTimeOffset LeftDraftAt { get; set; } = new(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
}

/// <summary>
/// Rewrites the draft on the submit transaction's connection before <c>ExecuteUpdate</c> runs,
/// after in-memory FormData validation has already passed.
/// </summary>
public sealed class SubmitPersistRaceInterceptor(SubmitCaseRaceState state) : DbCommandInterceptor
{
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        FlipBeforeCasesUpdate(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        FlipBeforeCasesUpdate(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void FlipBeforeCasesUpdate(DbCommand command)
    {
        if (!state.Armed || state.CaseId == Guid.Empty)
        {
            return;
        }

        var sql = command.CommandText;
        if (!sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
            || !sql.Contains("cases", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        state.Armed = false;

        DbParameter? caseIdParameter = null;
        foreach (DbParameter parameter in command.Parameters)
        {
            if (parameter.ParameterName.Contains("caseId", StringComparison.OrdinalIgnoreCase))
            {
                caseIdParameter = parameter;
                break;
            }
        }

        if (caseIdParameter is null)
        {
            throw new InvalidOperationException("submit ExecuteUpdate had no caseId parameter.");
        }

        using var flip = command.Connection!.CreateCommand();
        flip.Transaction = command.Transaction;
        var id = flip.CreateParameter();
        id.ParameterName = "@id";
        id.Value = caseIdParameter.Value ?? DBNull.Value;
        id.DbType = caseIdParameter.DbType;
        flip.Parameters.Add(id);

        if (state.Flip == SubmitPersistRace.LeaveDraft)
        {
            var at = flip.CreateParameter();
            at.ParameterName = "@at";
            at.Value = state.LeftDraftAt.ToString("O");
            flip.Parameters.Add(at);
            flip.CommandText = """
                UPDATE "cases"
                SET "Status" = 'Submitted', "SubmittedAt" = @at, "UpdatedAt" = @at
                WHERE "Id" = @id AND "Status" = 'Draft'
                """;
        }
        else if (state.Flip == SubmitPersistRace.DeleteRow)
        {
            flip.CommandText = """
                DELETE FROM "cases"
                WHERE "Id" = @id AND "Status" = 'Draft'
                """;
        }
        else
        {
            var form = flip.CreateParameter();
            form.ParameterName = "@form";
            form.Value = state.NewerFormData;
            flip.Parameters.Add(form);
            flip.CommandText = """
                UPDATE "cases"
                SET "FormData" = @form
                WHERE "Id" = @id AND "Status" = 'Draft'
                """;
        }

        var flipped = flip.ExecuteNonQuery();
        if (flipped != 1)
        {
            throw new InvalidOperationException(
                $"Submit race hook changed {flipped} rows for {state.Flip}. SQL: {flip.CommandText}");
        }
    }
}

public sealed class SubmitCaseRaceFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<SubmitCaseRaceState>();
            services.AddSingleton<SubmitPersistRaceInterceptor>();
        });
    }

    protected override void ConfigureSqlite(IServiceProvider serviceProvider, DbContextOptionsBuilder options)
    {
        options.AddInterceptors(serviceProvider.GetRequiredService<SubmitPersistRaceInterceptor>());
    }
}

public sealed class SubmitCaseRaceTests(SubmitCaseRaceFactory factory)
    : IClassFixture<SubmitCaseRaceFactory>, IAsyncLifetime
{
    private const string CompleteFormData = """
        {
          "fullName": "Ada Lovelace",
          "dateOfBirth": "1815-12-10",
          "nationality": "British",
          "address": "12 Analytical Engine Rd"
        }
        """;

    private const string InvalidFormData = """{"fullName":"Only name"}""";

    private const string OtherValidFormData = """
        {
          "fullName": "Ada Lovelace",
          "dateOfBirth": "1815-12-10",
          "nationality": "British",
          "address": "1 Concurrent Save Rd"
        }
        """;

    private HttpClient _client = null!;
    private Guid _tenantId;
    private Guid _customerId;
    private Guid _draftCaseId;

    public async Task InitializeAsync()
    {
        _client = factory.CreateClient();
        _tenantId = Guid.NewGuid();
        _customerId = Guid.NewGuid();
        _draftCaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Submit Race Co",
            Slug = $"sbrace-{_tenantId:N}"[..20],
            IsActive = true,
            CreatedAt = now
        });
        db.Users.Add(new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "c@sbrace.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = now
        });
        db.Cases.Add(new Case
        {
            Id = _draftCaseId,
            TenantId = _tenantId,
            CustomerUserId = _customerId,
            Title = "Submit race draft",
            Status = CaseStatus.Draft,
            FormData = CompleteFormData,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var state = scope.ServiceProvider.GetRequiredService<SubmitCaseRaceState>();
        state.CaseId = _draftCaseId;
        state.Armed = false;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Invalid_formData_written_after_read_stays_draft_and_returns_VALIDATION()
    {
        await ResetDraftAsync(CompleteFormData);
        Arm(SubmitPersistRace.InvalidFormData, InvalidFormData);

        using var document = await SubmitAsync();
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("VALIDATION", errors, StringComparison.Ordinal);
        Assert.Contains("dateOfBirth is required.", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("DOMAIN", errors, StringComparison.Ordinal);

        var stored = await LoadCaseAsync();
        Assert.Equal(CaseStatus.Draft, stored.Status);
        Assert.Equal(InvalidFormData, stored.FormData);
        Assert.Null(stored.SubmittedAt);
        Assert.Equal(0, await SubmittedAuditCountAsync());
    }

    [Fact]
    public async Task Valid_formData_written_after_read_stays_draft_and_returns_DOMAIN()
    {
        await ResetDraftAsync(CompleteFormData);
        Arm(SubmitPersistRace.ValidFormData, OtherValidFormData);

        using var document = await SubmitAsync();
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("DOMAIN", errors, StringComparison.Ordinal);
        Assert.Contains(SubmitCaseService.DraftChangedMessage, errors, StringComparison.Ordinal);
        Assert.DoesNotContain("VALIDATION", errors, StringComparison.Ordinal);
        Assert.DoesNotContain(SubmitCaseService.NotDraftMessage, errors, StringComparison.Ordinal);

        var stored = await LoadCaseAsync();
        Assert.Equal(CaseStatus.Draft, stored.Status);
        Assert.Equal(OtherValidFormData, stored.FormData);
        Assert.Null(stored.SubmittedAt);
        Assert.Equal(0, await SubmittedAuditCountAsync());
    }

    [Fact]
    public async Task Status_leaving_draft_after_read_returns_DOMAIN()
    {
        await ResetDraftAsync(CompleteFormData);
        var state = factory.Services.GetRequiredService<SubmitCaseRaceState>();
        state.Flip = SubmitPersistRace.LeaveDraft;
        state.Armed = true;

        using var document = await SubmitAsync();
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("DOMAIN", errors, StringComparison.Ordinal);
        Assert.Contains(SubmitCaseService.NotDraftMessage, errors, StringComparison.Ordinal);
        Assert.DoesNotContain("VALIDATION", errors, StringComparison.Ordinal);

        var stored = await LoadCaseAsync();
        Assert.Equal(CaseStatus.Submitted, stored.Status);
        Assert.Equal(CompleteFormData, stored.FormData);
        Assert.Equal(state.LeftDraftAt, stored.SubmittedAt);
        Assert.Equal(0, await SubmittedAuditCountAsync());
    }

    [Fact]
    public async Task Row_removed_after_read_returns_NOT_FOUND()
    {
        await ResetDraftAsync(CompleteFormData);
        var state = factory.Services.GetRequiredService<SubmitCaseRaceState>();
        state.Flip = SubmitPersistRace.DeleteRow;
        state.Armed = true;

        using var document = await SubmitAsync();
        var errors = document.RootElement.GetProperty("errors").ToString();
        Assert.Contains("NOT_FOUND", errors, StringComparison.Ordinal);
        Assert.Contains(SubmitCaseService.NotFoundMessage, errors, StringComparison.Ordinal);
        Assert.DoesNotContain("DOMAIN", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("VALIDATION", errors, StringComparison.Ordinal);

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Cases.IgnoreQueryFilters()
            .SingleOrDefaultAsync(c => c.Id == _draftCaseId);
        Assert.Null(stored);
        Assert.Equal(0, await SubmittedAuditCountAsync());
    }

    private void Arm(SubmitPersistRace flip, string newerFormData)
    {
        var state = factory.Services.GetRequiredService<SubmitCaseRaceState>();
        state.Flip = flip;
        state.NewerFormData = newerFormData;
        state.Armed = true;
    }

    private async Task ResetDraftAsync(string formData)
    {
        var state = factory.Services.GetRequiredService<SubmitCaseRaceState>();
        state.Armed = false;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await db.Cases
            .IgnoreQueryFilters()
            .Where(c => c.Id == _draftCaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.Status, CaseStatus.Draft)
                .SetProperty(c => c.FormData, formData)
                .SetProperty(c => c.SubmittedAt, (DateTimeOffset?)null)
                .SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow));
        if (updated == 0)
        {
            var now = DateTimeOffset.UtcNow;
            db.Cases.Add(new Case
            {
                Id = _draftCaseId,
                TenantId = _tenantId,
                CustomerUserId = _customerId,
                Title = "Submit race draft",
                Status = CaseStatus.Draft,
                FormData = formData,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        await db.AuditEntries
            .IgnoreQueryFilters()
            .Where(a => a.EntityId == _draftCaseId && a.Action == AuditActions.CaseSubmitted)
            .ExecuteDeleteAsync();
    }

    private async Task<JsonDocument> SubmitAsync()
    {
        using var jwtScope = factory.Services.CreateScope();
        var jwt = jwtScope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var user = new User
        {
            Id = _customerId,
            TenantId = _tenantId,
            Email = "c@sbrace.example",
            PasswordHash = "unused",
            Role = UserRole.Customer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var (token, _) = jwt.CreateAccessToken(user);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.PostAsync(
            "/graphql",
            new StringContent(
                $$"""
                {
                  "query": "mutation($input: SubmitCaseRequestInput!) { submitCase(input: $input) { id status } }",
                  "variables": { "input": { "id": "{{_draftCaseId}}" } }
                }
                """,
                Encoding.UTF8,
                "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body);
    }

    private async Task<Case> LoadCaseAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Cases.IgnoreQueryFilters().SingleAsync(c => c.Id == _draftCaseId);
    }

    private async Task<int> SubmittedAuditCountAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .AuditEntries.IgnoreQueryFilters()
            .CountAsync(a => a.EntityId == _draftCaseId && a.Action == AuditActions.CaseSubmitted);
    }
}
