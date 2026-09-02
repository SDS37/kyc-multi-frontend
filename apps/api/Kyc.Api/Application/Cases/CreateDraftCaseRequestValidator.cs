using FluentValidation;

namespace Kyc.Api.Application.Cases;

public sealed class CreateDraftCaseRequestValidator : AbstractValidator<CreateDraftCaseRequest>
{
    public CreateDraftCaseRequestValidator()
    {
        RuleFor(request => request).Custom((request, context) =>
        {
            foreach (var error in CaseDraftValidation.ValidateTitleAndFormData(request.Title, request.FormData))
            {
                context.AddFailure(error);
            }
        });
    }
}
