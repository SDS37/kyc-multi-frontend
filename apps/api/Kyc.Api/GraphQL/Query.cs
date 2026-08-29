using HotChocolate;
using HotChocolate.Authorization;
using Kyc.Api.Application.Cases;
using Kyc.Api.Domain.Cases;

namespace Kyc.Api.GraphQL;

/// <summary>
/// Root GraphQL query type. Deny by default — authenticated callers only (KYC-021).
/// </summary>
[Authorize]
public class Query
{
    /// <summary>Lightweight liveness field so the schema is non-empty from day one.</summary>
    public string ApiStatus() => "ok";

    /// <summary>
    /// List cases visible to the caller (KYC-036). Customer: own cases; Reviewer/TenantAdmin: all tenant cases.
    /// Optional status filter; offset pagination (<c>skip</c>/<c>take</c>).
    /// </summary>
    public async Task<CaseListResponse> Cases(
        ListCasesService service,
        CancellationToken cancellationToken,
        CaseStatus? status = null,
        int? skip = null,
        int? take = null)
    {
        var (result, validationErrors, unauthorized) =
            await service.ListAsync(new ListCasesRequest(status, skip, take), cancellationToken);

        if (validationErrors.Count > 0)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(string.Join(" ", validationErrors))
                    .SetCode("VALIDATION")
                    .Build());
        }

        if (unauthorized || result is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(CreateDraftCaseService.GenericAuthFailure)
                    .SetCode("AUTH_FAILED")
                    .Build());
        }

        return result;
    }
}
