using FluentValidation;
using Kyc.Api.Application.Validation;

namespace Kyc.Api.Application.Cases;

public sealed class StartCaseReviewRequestValidator : AbstractValidator<StartCaseReviewRequest>
{
    public StartCaseReviewRequestValidator()
    {
        RuleFor(request => request.Id).RequiredCaseId();
    }
}
