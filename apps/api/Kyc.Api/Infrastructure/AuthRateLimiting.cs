using System.Threading.RateLimiting;
using Kyc.Api.Application.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace Kyc.Api.Infrastructure;

public static class AuthRateLimiting
{
    public const string LoginPolicy = "login";
    public const string RegisterPolicy = "register";
    public const string GraphqlPolicy = "graphql";
    public const string TooManyRequestsMessage = "Too many requests.";

    public static void Configure(RateLimiterOptions options, AuthLimitsOptions limits)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            }

            await response.WriteAsJsonAsync(new { error = TooManyRequestsMessage }, cancellationToken);
        };

        options.AddPolicy(LoginPolicy, httpContext =>
            Partition(httpContext, LoginPolicy, limits.LoginPermitPerMinute));
        options.AddPolicy(RegisterPolicy, httpContext =>
            Partition(httpContext, RegisterPolicy, limits.RegisterPermitPerMinute));
        options.AddPolicy(GraphqlPolicy, httpContext =>
        {
            var kind = httpContext.Features.Get<IGraphQlOperationFeature>()?.Kind ?? GraphQlOperationKind.Other;
            return kind switch
            {
                GraphQlOperationKind.Register => Partition(
                    httpContext,
                    RegisterPolicy,
                    limits.RegisterPermitPerMinute),
                GraphQlOperationKind.Login => Partition(
                    httpContext,
                    LoginPolicy,
                    limits.LoginPermitPerMinute),
                _ => Partition(httpContext, GraphqlPolicy, limits.GraphqlPermitPerMinute)
            };
        });
    }

    private static RateLimitPartition<string> Partition(HttpContext httpContext, string prefix, int permitLimit)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            $"{prefix}:{ip}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    }
}
