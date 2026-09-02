using FluentValidation;
using Kyc.Api.Application.Validation;

namespace Kyc.Api.Application.Cases;

public sealed class ApproveCaseRequestValidator : AbstractValidator<ApproveCaseRequest>
{
    public ApproveCaseRequestValidator()
    {
        RuleFor(request => request.Id).RequiredCaseId();

        RuleSet(RequestValidation.CommentSet, () =>
        {
            RuleFor(request => request.Comment)
                .Must(comment =>
                    string.IsNullOrWhiteSpace(comment) ||
                    comment.Trim().Length <= CompleteCaseReviewService.MaxCommentLength)
                .WithMessage($"Comment must be at most {CompleteCaseReviewService.MaxCommentLength} characters.");
        });
    }
}
