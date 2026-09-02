using FluentValidation;
using Kyc.Api.Application.Validation;

namespace Kyc.Api.Application.Cases;

public sealed class UpdateDraftCaseRequestValidator : AbstractValidator<UpdateDraftCaseRequest>
{
    public UpdateDraftCaseRequestValidator()
    {
        RuleSet(RequestValidation.IdSet, () =>
        {
            RuleFor(request => request.Id)
                .NotEqual(Guid.Empty)
                .WithMessage(CaseIdRules.RequiredMessage);
        });

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
