using FluentValidation;
using Kyc.Api.Application.Validation;

namespace Kyc.Api.Application.Cases;

public sealed class SubmitCaseRequestValidator : AbstractValidator<SubmitCaseRequest>
{
    public SubmitCaseRequestValidator()
    {
        RuleFor(request => request.Id)
            .NotEqual(Guid.Empty)
            .WithMessage(CaseIdRules.RequiredMessage);
    }
}
