using HotChocolate;
using HotChocolate.Authorization;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;

namespace Kyc.Api.GraphQL;

/// <summary>
/// Root GraphQL mutations. Type is deny-by-default; only login/register are anonymous (KYC-021).
/// Role gates (KYC-022) protect reviewer stubs and customer case operations.
/// </summary>
[Authorize]
public class Mutation
{
    [AllowAnonymous]
    public async Task<RegisterTenantResponse> RegisterTenant(
        RegisterTenantRequest input,
        RegisterTenantService service,
        CancellationToken cancellationToken)
    {
        var (result, errors) = await service.RegisterAsync(input, cancellationToken);
        if (errors.Count > 0)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(string.Join(" ", errors))
                    .SetCode("VALIDATION")
                    .Build());
        }

        return result!;
    }

    [AllowAnonymous]
    public async Task<LoginResponse> Login(
        LoginRequest input,
        LoginService service,
        CancellationToken cancellationToken)
    {
        var (result, validationErrors, unauthorized) = await service.LoginAsync(input, cancellationToken);
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
                    .SetMessage(LoginService.GenericAuthFailure)
                    .SetCode("AUTH_FAILED")
                    .Build());
        }

        return result;
    }

    /// <summary>
    /// Reviewer-only gate (KYC-022). Placeholder until case review mutations.
    /// </summary>
    [Authorize(Roles = new[] { AuthRoles.Reviewer })]
    public string ReviewerOnlyPing() => "reviewer-ok";

    /// <summary>
    /// Customer creates a draft KYC case (KYC-031). Tenant and owner come from the JWT only.
    /// </summary>
    [Authorize(Roles = new[] { AuthRoles.Customer })]
    public async Task<CreateDraftCaseResponse> CreateDraftCase(
        CreateDraftCaseRequest input,
        CreateDraftCaseService service,
        CancellationToken cancellationToken)
    {
        var (result, errors) = await service.CreateAsync(input, cancellationToken);
        if (errors.Count > 0)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(string.Join(" ", errors))
                    .SetCode("VALIDATION")
                    .Build());
        }

        return result!;
    }
}
