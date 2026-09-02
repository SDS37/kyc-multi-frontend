using FluentValidation;
using FluentValidation.Results;

namespace Kyc.Api.Application.Validation;

/// <summary>
/// Runs FluentValidation at the application-service boundary and maps failures to message lists.
/// GraphQL/REST adapters already treat a non-empty list as <c>VALIDATION</c> (HTTP 200 / 400), never 500.
/// </summary>
public static class RequestValidation
{
    public const string IdSet = "Id";
    public const string PayloadSet = "Payload";
    public const string CommentSet = "Comment";

    public static IReadOnlyList<string> Errors<T>(IValidator<T> validator, T instance)
    {
        ValidationResult result = validator.Validate(instance);
        return ToMessages(result);
    }

    public static IReadOnlyList<string> Errors<T>(IValidator<T> validator, T instance, string ruleSet)
    {
        ValidationResult result = validator.Validate(instance, options => options.IncludeRuleSets(ruleSet));
        return ToMessages(result);
    }

    private static IReadOnlyList<string> ToMessages(ValidationResult result) =>
        result.IsValid
            ? []
            : result.Errors.Select(failure => failure.ErrorMessage).ToList();
}
