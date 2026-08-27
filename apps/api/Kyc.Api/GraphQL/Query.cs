using HotChocolate.Authorization;

namespace Kyc.Api.GraphQL;

/// <summary>
/// Root GraphQL query type. Deny by default — authenticated callers only (KYC-021).
/// Domain fields arrive in later stories (cases, etc.).
/// </summary>
[Authorize]
public class Query
{
    /// <summary>Lightweight liveness field so the schema is non-empty from day one.</summary>
    public string ApiStatus() => "ok";
}
