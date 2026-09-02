using FluentValidation;
using Kyc.Api.Application.Validation;

namespace Kyc.Api.Application.Cases;

public sealed class UpdateDraftCaseRequestValidator : AbstractValidator<UpdateDraftCaseRequest>
{
    public UpdateDraftCaseRequestValidator()
    {
        // Default ruleset: runs when Errors(validator, request) is called with no set.
        RuleFor(request => request.Id).RequiredCaseId();

        RuleSet(RequestValidation.PayloadSet, () =>
        {
            RuleFor(request => request).Custom((request, context) =>
            {
                foreach (var error in CaseDraftValidation.ValidateTitleAndFormData(request.Title, request.FormData))
                {
                    context.AddFailure(error);
                }
            });
        });
    }
}
