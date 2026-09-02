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
/// request cannot multiply <c>login</c> / <c>registerTenant</c> (KYC-095). Mixed login+register
/// in one request also exceeds the limit (login would otherwise skip the login bucket).
/// </summary>
public readonly record struct GraphQlClassification(
    GraphQlOperationKind Kind,
    int LoginFieldCount,
    int RegisterFieldCount)
{
    public bool ExceedsSingleAuthOpLimit => LoginFieldCount + RegisterFieldCount > 1;
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
/// GraphQL <c>#</c> comments are stripped before field counts so <c>login # x\\n(</c> still hits the login bucket.
/// Named operations (<c>mutation Login(...) { login(...) }</c>) are stripped before those counts so
/// the operation name is not treated as a second auth field.
/// </summary>
public static partial class GraphQlOperationClassifier
{
    /// <summary>
    /// Peek cap must exceed max FormData (64 KiB) plus GraphQL envelope so a valid
    /// <c>updateDraftCase</c> is not fail-closed to 429. Larger padded bodies still truncate.
    /// </summary>
    public const int MaxPeekBytes = 96 * 1024;

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
            // Truncated peeks must not fall through to the looser GraphQL bucket, and counts must
            // trip ExceedsSingleAuthOpLimit so a padded batch cannot skip the alias/batch 429.
            return failClosedWhenUnparsed
                ? new GraphQlClassification(GraphQlOperationKind.Register, 2, 2)
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

        var query = StripNamedOperations(StripGraphQlComments(queryElement.GetString() ?? string.Empty));
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

    /// <summary>
    /// Drops <c>mutation Login</c> / <c>query Foo</c> names so field regexes do not count
    /// the operation name as a second <c>login(</c> / <c>registerTenant(</c>. All three UIs
    /// send <c>mutation Login($input: ...) { login(...) }</c>.
    /// </summary>
    [GeneratedRegex(
        @"\b(mutation|query|subscription)\s+[A-Za-z_]\w*",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex NamedOperation();

    private static string StripNamedOperations(string query) =>
        NamedOperation().Replace(query, "${1}");

    /// <summary>
    /// Drops GraphQL <c>#</c> line comments so field regexes still see <c>login(</c> / <c>registerTenant(</c>.
    /// Leaves <c>#</c> inside strings and block strings alone.
    /// </summary>
    private static string StripGraphQlComments(string query)
    {
        var output = new StringBuilder(query.Length);
        var i = 0;
        while (i < query.Length)
        {
            if (i + 2 < query.Length && query[i] == '"' && query[i + 1] == '"' && query[i + 2] == '"')
            {
                var end = query.IndexOf("\"\"\"", i + 3, StringComparison.Ordinal);
                if (end < 0)
                {
                    output.Append(query.AsSpan(i));
                    break;
                }

                output.Append(query.AsSpan(i, end + 3 - i));
                i = end + 3;
                continue;
            }

            if (query[i] == '"')
            {
                output.Append('"');
                i++;
                while (i < query.Length)
                {
                    var c = query[i];
                    output.Append(c);
                    if (c == '\\' && i + 1 < query.Length)
                    {
                        output.Append(query[i + 1]);
                        i += 2;
                        continue;
                    }

                    i++;
                    if (c == '"')
                    {
                        break;
                    }
                }

                continue;
            }

            if (query[i] == '#')
            {
                while (i < query.Length && query[i] != '\n' && query[i] != '\r')
                {
                    i++;
                }

                continue;
            }

            output.Append(query[i]);
            i++;
        }

        return output.ToString();
    }

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
