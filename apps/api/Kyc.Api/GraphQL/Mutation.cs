using HotChocolate.Authorization;
using Kyc.Api.Application.Cases;
using Kyc.Api.Application.Identity;

namespace Kyc.Api.GraphQL;

/// <summary>
/// Root GraphQL mutations. Type is deny-by-default; only login/register are anonymous (KYC-021).
/// Role gates protect Customer case mutations and Reviewer/TenantAdmin review lifecycle (KYC-022+).
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
    /// Reviewer or TenantAdmin moves a submitted case to In Review (KYC-034). Same-tenant via JWT filters.
    /// </summary>
    [Authorize(Roles = new[] { AuthRoles.Reviewer, AuthRoles.TenantAdmin })]
    public async Task<CaseResponse> StartCaseReview(
        StartCaseReviewRequest input,
        StartCaseReviewService service,
        CancellationToken cancellationToken)
    {
        var (result, validationErrors, unauthorized, errorCode, errorMessage) =
            await service.StartAsync(input, cancellationToken);

        if (validationErrors.Count > 0)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(string.Join(" ", validationErrors))
                    .SetCode("VALIDATION")
                    .Build());
        }

        if (unauthorized)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(CreateDraftCaseService.GenericAuthFailure)
                    .SetCode("AUTH_FAILED")
                    .Build());
        }

        if (errorCode is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(errorMessage ?? "Request failed.")
                    .SetCode(errorCode)
                    .Build());
        }

        return result!;
    }

    /// <summary>
    /// Customer creates a draft KYC case (KYC-031). Tenant and owner come from the JWT only.
    /// </summary>
    [Authorize(Roles = new[] { AuthRoles.Customer })]
    public async Task<CaseResponse> CreateDraftCase(
        CreateDraftCaseRequest input,
        CreateDraftCaseService service,
        CancellationToken cancellationToken)
    {
        var (result, validationErrors, unauthorized) = await service.CreateAsync(input, cancellationToken);
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

    /// <summary>
    /// Customer updates their own draft case (KYC-032 / KYC-106 / KYC-095). Missing or not owner → NOT_FOUND; non-draft → DOMAIN. Status is re-checked at persist (`ExecuteUpdate` where Draft).
    /// </summary>
    [Authorize(Roles = new[] { AuthRoles.Customer })]
    public async Task<CaseResponse> UpdateDraftCase(
        UpdateDraftCaseRequest input,
        UpdateDraftCaseService service,
        CancellationToken cancellationToken)
    {
        var (result, validationErrors, unauthorized, errorCode, errorMessage) =
            await service.UpdateAsync(input, cancellationToken);

        if (validationErrors.Count > 0)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(string.Join(" ", validationErrors))
                    .SetCode("VALIDATION")
                    .Build());
        }

        if (unauthorized)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(CreateDraftCaseService.GenericAuthFailure)
                    .SetCode("AUTH_FAILED")
                    .Build());
        }

        if (errorCode is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(errorMessage ?? "Request failed.")
                    .SetCode(errorCode)
                    .Build());
        }

        return result!;
    }

    /// <summary>
    /// Customer submits their own draft (KYC-033). Required FormData fields must already be persisted.
    /// </summary>
    [Authorize(Roles = new[] { AuthRoles.Customer })]
    public async Task<CaseResponse> SubmitCase(
        SubmitCaseRequest input,
        SubmitCaseService service,
        CancellationToken cancellationToken)
    {
        var (result, validationErrors, unauthorized, errorCode, errorMessage) =
            await service.SubmitAsync(input, cancellationToken);

        if (validationErrors.Count > 0)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(string.Join(" ", validationErrors))
                    .SetCode("VALIDATION")
                    .Build());
        }

        if (unauthorized)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(CreateDraftCaseService.GenericAuthFailure)
                    .SetCode("AUTH_FAILED")
                    .Build());
        }

        if (errorCode is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(errorMessage ?? "Request failed.")
                    .SetCode(errorCode)
                    .Build());
        }

        return result!;
    }

    /// <summary>
    /// Reviewer or TenantAdmin approves an InReview case (KYC-035). Comment optional.
    /// </summary>
    [Authorize(Roles = new[] { AuthRoles.Reviewer, AuthRoles.TenantAdmin })]
    public async Task<CaseResponse> ApproveCase(
        ApproveCaseRequest input,
        CompleteCaseReviewService service,
        CancellationToken cancellationToken)
    {
        var (result, validationErrors, unauthorized, errorCode, errorMessage) =
            await service.ApproveAsync(input, cancellationToken);
        return MapCaseMutationResult(result, validationErrors, unauthorized, errorCode, errorMessage);
    }

    /// <summary>
    /// Reviewer or TenantAdmin rejects an InReview case (KYC-035). Comment required.
    /// </summary>
    [Authorize(Roles = new[] { AuthRoles.Reviewer, AuthRoles.TenantAdmin })]
    public async Task<CaseResponse> RejectCase(
        RejectCaseRequest input,
        CompleteCaseReviewService service,
        CancellationToken cancellationToken)
    {
        var (result, validationErrors, unauthorized, errorCode, errorMessage) =
            await service.RejectAsync(input, cancellationToken);
        return MapCaseMutationResult(result, validationErrors, unauthorized, errorCode, errorMessage);
    }

    private static CaseResponse MapCaseMutationResult(
        CaseResponse? result,
        IReadOnlyList<string> validationErrors,
        bool unauthorized,
        string? errorCode,
        string? errorMessage)
    {
        if (validationErrors.Count > 0)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(string.Join(" ", validationErrors))
                    .SetCode("VALIDATION")
                    .Build());
        }

        if (unauthorized)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(CreateDraftCaseService.GenericAuthFailure)
                    .SetCode("AUTH_FAILED")
                    .Build());
        }

        if (errorCode is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(errorMessage ?? "Request failed.")
                    .SetCode(errorCode)
                    .Build());
        }

        return result!;
    }
}
