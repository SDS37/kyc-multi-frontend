using FluentValidation;

namespace Kyc.Api.Application.Validation;

/// <summary>Shared input for Guid-only case reads (detail, documents, audit, upload).</summary>
public sealed record CaseIdInput(Guid Id);

public sealed class CaseIdInputValidator : AbstractValidator<CaseIdInput>
{
    public CaseIdInputValidator()
    {
        RuleFor(input => input.Id)
            .NotEqual(Guid.Empty)
            .WithMessage(CaseIdRules.RequiredMessage);
    }
}

internal static class CaseIdRules
{
    public const string RequiredMessage = "Case id is required.";
}
