using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Documents;
using Kyc.Api.Application.Identity;
using Kyc.Api.Application.Validation;

namespace Kyc.Api.Tests;

public sealed class RequestValidatorTests
{
    [Fact]
    public void Register_short_password_keeps_existing_message()
    {
        var validator = new RegisterTenantRequestValidator();
        var request = new RegisterTenantRequest("Acme", "acme", "admin@acme.example", "Short1");

        var errors = RequestValidation.Errors(validator, request);

        Assert.Contains($"Password must be at least {PasswordPolicy.MinLength} characters.", errors);
    }

    [Fact]
    public void Register_short_invite_code_is_rejected()
    {
        var validator = new RegisterTenantRequestValidator();
        var request = new RegisterTenantRequest(
            "Acme",
            "acme",
            "admin@acme.example",
            "ChangeMe1234",
            InviteCode: "tooshort");

        var errors = RequestValidation.Errors(validator, request);

        Assert.Contains("Invite code must be between 16 and 64 characters.", errors);
    }

    [Fact]
    public void Login_oversized_password_keeps_existing_message()
    {
        var validator = new LoginRequestValidator();
        var password = new string('x', PasswordPolicy.MaxLength + 1);
        var request = new LoginRequest("acme", "admin@acme.example", password);

        var errors = RequestValidation.Errors(validator, request);

        Assert.Contains($"Password must be at most {PasswordPolicy.MaxLength} characters.", errors);
    }

    [Fact]
    public void List_take_above_max_keeps_existing_message()
    {
        var validator = new ListCasesRequestValidator();
        var request = new ListCasesRequest(null, 0, ListCasesService.MaxPageSize + 1);

        var errors = RequestValidation.Errors(validator, request);

        Assert.Contains($"Take must be between 1 and {ListCasesService.MaxPageSize}.", errors);
    }

    [Fact]
    public void Update_default_rules_check_id_only()
    {
        var validator = new UpdateDraftCaseRequestValidator();
        var request = new UpdateDraftCaseRequest(Guid.Empty, "  ", null);

        var defaultErrors = RequestValidation.Errors(validator, request);
        var payloadErrors = RequestValidation.Errors(validator, request, RequestValidation.PayloadSet);

        Assert.Equal(["Case id is required."], defaultErrors);
        Assert.Contains("Title is required.", payloadErrors);
        Assert.DoesNotContain("Case id is required.", payloadErrors);
    }

    [Fact]
    public void Unknown_rule_set_throws()
    {
        var validator = new UpdateDraftCaseRequestValidator();
        var request = new UpdateDraftCaseRequest(Guid.NewGuid(), "Title", null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RequestValidation.Errors(validator, request, "NoSuchSet"));

        Assert.Contains("NoSuchSet", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_case_id_uses_shared_message()
    {
        var validator = new CaseIdInputValidator();
        var errors = RequestValidation.Errors(validator, new CaseIdInput(Guid.Empty));

        Assert.Equal(["Case id is required."], errors);
    }

    [Fact]
    public void Download_ids_keep_existing_messages()
    {
        var validator = new DownloadDocumentIdsValidator();
        var errors = RequestValidation.Errors(validator, new DownloadDocumentIds(Guid.Empty, Guid.Empty));

        Assert.Contains("Case id is required.", errors);
        Assert.Contains(DownloadDocumentIdsValidator.DocumentIdRequiredMessage, errors);
    }
}
