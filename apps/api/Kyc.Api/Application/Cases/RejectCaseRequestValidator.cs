using FluentValidation;
using Kyc.Api.Application.Validation;

namespace Kyc.Api.Application.Cases;

public sealed class RejectCaseRequestValidator : AbstractValidator<RejectCaseRequest>
{
    public RejectCaseRequestValidator()
    {
        RuleFor(request => request.Id).RequiredCaseId();

        RuleSet(RequestValidation.CommentSet, () =>
        {
            RuleFor(request => request.Comment)
                .Cascade(CascadeMode.Stop)
                .Must(comment => !string.IsNullOrWhiteSpace(comment))
                .WithMessage(CompleteCaseReviewService.RejectCommentRequiredMessage)
                .Must(comment =>
                    (comment ?? string.Empty).Trim().Length <= CompleteCaseReviewService.MaxCommentLength)
                .WithMessage($"Comment must be at most {CompleteCaseReviewService.MaxCommentLength} characters.");
        });
    }
}
