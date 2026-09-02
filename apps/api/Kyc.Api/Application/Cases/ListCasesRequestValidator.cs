using FluentValidation;

namespace Kyc.Api.Application.Cases;

public sealed class ListCasesRequestValidator : AbstractValidator<ListCasesRequest>
{
    public ListCasesRequestValidator()
    {
        RuleFor(request => request.Skip)
            .GreaterThanOrEqualTo(0)
            .When(request => request.Skip.HasValue)
            .WithMessage("Skip must be zero or greater.");

        RuleFor(request => request.Take)
            .InclusiveBetween(1, ListCasesService.MaxPageSize)
            .When(request => request.Take.HasValue)
            .WithMessage($"Take must be between 1 and {ListCasesService.MaxPageSize}.");
    }
}
