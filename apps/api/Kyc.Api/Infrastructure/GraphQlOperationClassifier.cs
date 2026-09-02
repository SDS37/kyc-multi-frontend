using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kyc.Api.Infrastructure;

public enum GraphQlOperationKind
{
    Other = 0,
    Login = 1,
    Register = 2
}

public interface IGraphQlOperationFeature
{
    GraphQlOperationKind Kind { get; }
}

public sealed class GraphQlOperationFeature(GraphQlOperationKind kind) : IGraphQlOperationFeature
{
    public GraphQlOperationKind Kind { get; } = kind;
}

/// <summary>
/// Classifies a GraphQL POST so login/register can use the auth buckets instead of the general GraphQL limiter.
/// Inspects <c>operationName</c> and the <c>query</c> document only — never variable values.
/// </summary>
public static partial class GraphQlOperationClassifier
{
    public const int MaxPeekBytes = 32 * 1024;

    public static async Task<GraphQlOperationKind> ClassifyAsync(Stream body, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxPeekBytes];
        var read = await body.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        if (read <= 0)
        {
            return GraphQlOperationKind.Other;
        }

        var json = Encoding.UTF8.GetString(buffer.AsSpan(0, read));
        return ClassifyJson(json);
    }

    public static GraphQlOperationKind ClassifyJson(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return GraphQlOperationKind.Other;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                var kind = GraphQlOperationKind.Other;
                foreach (var item in root.EnumerateArray())
                {
                    kind = Max(kind, ClassifyObject(item));
                }

                return kind;
            }

            return ClassifyObject(root);
        }
    }

    private static GraphQlOperationKind ClassifyObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return GraphQlOperationKind.Other;
        }

        if (element.TryGetProperty("operationName", out var operationName) &&
            operationName.ValueKind == JsonValueKind.String)
        {
            var name = operationName.GetString();
            if (IsRegisterName(name))
            {
                return GraphQlOperationKind.Register;
            }

            if (IsLoginName(name))
            {
                return GraphQlOperationKind.Login;
            }
        }

        if (!element.TryGetProperty("query", out var queryElement) ||
            queryElement.ValueKind != JsonValueKind.String)
        {
            return GraphQlOperationKind.Other;
        }

        var query = queryElement.GetString() ?? string.Empty;
        if (RegisterField().IsMatch(query))
        {
            return GraphQlOperationKind.Register;
        }

        if (LoginField().IsMatch(query))
        {
            return GraphQlOperationKind.Login;
        }

        return GraphQlOperationKind.Other;
    }

    private static GraphQlOperationKind Max(GraphQlOperationKind left, GraphQlOperationKind right) =>
        left > right ? left : right;

    private static bool IsLoginName(string? name) =>
        string.Equals(name, "login", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Login", StringComparison.Ordinal);

    private static bool IsRegisterName(string? name) =>
        string.Equals(name, "registerTenant", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "RegisterTenant", StringComparison.Ordinal);

    [GeneratedRegex(@"\bregisterTenant\s*[\({]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RegisterField();

    [GeneratedRegex(@"\blogin\s*[\({]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex LoginField();
}
