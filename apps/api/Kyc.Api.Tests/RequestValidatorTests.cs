using FluentValidation;
using Kyc.Api.Application.Cases;
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
    public void Update_payload_rules_do_not_run_with_id_set()
    {
        var validator = new UpdateDraftCaseRequestValidator();
        var request = new UpdateDraftCaseRequest(Guid.NewGuid(), "  ", null);

        var idErrors = RequestValidation.Errors(validator, request, RequestValidation.IdSet);
        var payloadErrors = RequestValidation.Errors(validator, request, RequestValidation.PayloadSet);

        Assert.Empty(idErrors);
        Assert.Contains("Title is required.", payloadErrors);
    }

    [Fact]
    public void Empty_case_id_uses_shared_message()
    {
        var validator = new CaseIdInputValidator();
        var errors = RequestValidation.Errors(validator, new CaseIdInput(Guid.Empty));

        Assert.Equal(["Case id is required."], errors);
    }
}
