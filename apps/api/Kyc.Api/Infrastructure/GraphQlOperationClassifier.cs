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
/// The stricter of the two wins so a login <c>operationName</c> cannot hide <c>registerTenant</c>.
/// </summary>
public static partial class GraphQlOperationClassifier
{
    public const int MaxPeekBytes = 32 * 1024;

    public static async Task<GraphQlOperationKind> ClassifyAsync(Stream body, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxPeekBytes];
        var read = 0;
        while (read < buffer.Length)
        {
            var chunk = await body.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken);
            if (chunk == 0)
            {
                break;
            }

            read += chunk;
        }

        if (read <= 0)
        {
            return GraphQlOperationKind.Other;
        }

        var json = Encoding.UTF8.GetString(buffer.AsSpan(0, read));
        var truncated = read == MaxPeekBytes;
        return ClassifyJson(json, failClosedWhenUnparsed: truncated);
    }

    public static GraphQlOperationKind ClassifyJson(string json) =>
        ClassifyJson(json, failClosedWhenUnparsed: false);

    private static GraphQlOperationKind ClassifyJson(string json, bool failClosedWhenUnparsed)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            // Truncated peeks must not fall through to the looser GraphQL bucket (login/register after padding).
            return failClosedWhenUnparsed ? GraphQlOperationKind.Register : GraphQlOperationKind.Other;
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

        var kind = GraphQlOperationKind.Other;
        if (element.TryGetProperty("operationName", out var operationName) &&
            operationName.ValueKind == JsonValueKind.String)
        {
            var name = operationName.GetString();
            if (IsRegisterName(name))
            {
                kind = GraphQlOperationKind.Register;
            }
            else if (IsLoginName(name))
            {
                kind = GraphQlOperationKind.Login;
            }
        }

        if (!element.TryGetProperty("query", out var queryElement) ||
            queryElement.ValueKind != JsonValueKind.String)
        {
            return kind;
        }

        var query = queryElement.GetString() ?? string.Empty;
        if (RegisterField().IsMatch(query))
        {
            return Max(kind, GraphQlOperationKind.Register);
        }

        if (LoginField().IsMatch(query))
        {
            return Max(kind, GraphQlOperationKind.Login);
        }

        return kind;
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
