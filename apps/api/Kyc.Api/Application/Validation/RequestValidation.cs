using FluentValidation;
using FluentValidation.Results;

namespace Kyc.Api.Application.Validation;

/// <summary>
/// Runs FluentValidation at the application-service boundary and maps failures to message lists.
/// GraphQL/REST adapters already treat a non-empty list as <c>VALIDATION</c> (HTTP 200 / 400), never 500.
/// </summary>
public static class RequestValidation
{
    public const string PayloadSet = "Payload";
    public const string CommentSet = "Comment";

    public static List<string> Errors<T>(IValidator<T> validator, T instance)
    {
        ValidationResult result = validator.Validate(instance);
        return ToMessages(result);
    }

    public static List<string> Errors<T>(IValidator<T> validator, T instance, string ruleSet)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleSet);
        ValidationResult result = validator.Validate(instance, options => options.IncludeRuleSets(ruleSet));
        if (!RuleSetRan(result, ruleSet))
        {
            throw new InvalidOperationException($"FluentValidation did not execute rule set '{ruleSet}'.");
        }

        return ToMessages(result);
    }

    private static bool RuleSetRan(ValidationResult result, string ruleSet) =>
        result.RuleSetsExecuted is { Length: > 0 } executed
        && executed.Contains(ruleSet, StringComparer.OrdinalIgnoreCase);

    private static List<string> ToMessages(ValidationResult result) =>
        result.IsValid
            ? []
            : result.Errors.ConvertAll(failure => failure.ErrorMessage);
}
