using FluentValidation;
using Kyc.Api.Application.Validation;

namespace Kyc.Api.Application.Documents;

public sealed record DownloadDocumentIds(Guid CaseId, Guid DocumentId);

public sealed class DownloadDocumentIdsValidator : AbstractValidator<DownloadDocumentIds>
{
    public const string DocumentIdRequiredMessage = "Document id is required.";

    public DownloadDocumentIdsValidator()
    {
        RuleFor(ids => ids.CaseId).RequiredCaseId();
        RuleFor(ids => ids.DocumentId)
            .NotEqual(Guid.Empty)
            .WithMessage(DocumentIdRequiredMessage);
    }
}
