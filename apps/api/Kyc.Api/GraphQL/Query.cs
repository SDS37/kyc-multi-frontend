namespace Kyc.Api.GraphQL;

/// <summary>
/// Root GraphQL query type. Domain fields arrive in later stories (cases, etc.).
/// </summary>
public class Query
{
    /// <summary>Lightweight liveness field so the schema is non-empty from day one.</summary>
    public string ApiStatus() => "ok";
}
