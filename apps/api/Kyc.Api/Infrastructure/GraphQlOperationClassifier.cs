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

/// <summary>
/// Result of peeking a GraphQL POST. Field counts cover aliases and JSON batches so one HTTP
/// request cannot multiply <c>login</c> / <c>registerTenant</c> (KYC-095).
/// </summary>
public readonly record struct GraphQlClassification(
    GraphQlOperationKind Kind,
    int LoginFieldCount,
    int RegisterFieldCount)
{
    public bool ExceedsSingleAuthOpLimit => LoginFieldCount > 1 || RegisterFieldCount > 1;
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

    public static async Task<GraphQlClassification> ClassifyAsync(Stream body, CancellationToken cancellationToken)
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
            return new GraphQlClassification(GraphQlOperationKind.Other, 0, 0);
        }

        var json = Encoding.UTF8.GetString(buffer.AsSpan(0, read));
        var truncated = read == MaxPeekBytes;
        return ClassifyDocument(json, failClosedWhenUnparsed: truncated);
    }

    public static GraphQlOperationKind ClassifyJson(string json) =>
        ClassifyDocument(json, failClosedWhenUnparsed: false).Kind;

    public static GraphQlClassification ClassifyDocument(string json) =>
        ClassifyDocument(json, failClosedWhenUnparsed: false);

    private static GraphQlClassification ClassifyDocument(string json, bool failClosedWhenUnparsed)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            // Truncated peeks must not fall through to the looser GraphQL bucket (login/register after padding).
            return failClosedWhenUnparsed
                ? new GraphQlClassification(GraphQlOperationKind.Register, 0, 1)
                : new GraphQlClassification(GraphQlOperationKind.Other, 0, 0);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                var kind = GraphQlOperationKind.Other;
                var loginCount = 0;
                var registerCount = 0;
                foreach (var item in root.EnumerateArray())
                {
                    var part = ClassifyObject(item);
                    kind = Max(kind, part.Kind);
                    loginCount += part.LoginFieldCount;
                    registerCount += part.RegisterFieldCount;
                }

                return new GraphQlClassification(kind, loginCount, registerCount);
            }

            return ClassifyObject(root);
        }
    }

    private static GraphQlClassification ClassifyObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return new GraphQlClassification(GraphQlOperationKind.Other, 0, 0);
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
            return new GraphQlClassification(kind, 0, 0);
        }

        var query = queryElement.GetString() ?? string.Empty;
        var loginCount = LoginField().Count(query);
        var registerCount = RegisterField().Count(query);
        if (registerCount > 0)
        {
            kind = Max(kind, GraphQlOperationKind.Register);
        }
        else if (loginCount > 0)
        {
            kind = Max(kind, GraphQlOperationKind.Login);
        }

        return new GraphQlClassification(kind, loginCount, registerCount);
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
