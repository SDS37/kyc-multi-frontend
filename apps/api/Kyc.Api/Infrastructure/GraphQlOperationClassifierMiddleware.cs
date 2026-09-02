namespace Kyc.Api.Infrastructure;

/// <summary>Peeks POST /graphql so the rate limiter can partition login/register away from UI traffic.</summary>
public sealed class GraphQlOperationClassifierMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.StartsWithSegments("/graphql"))
        {
            context.Request.EnableBuffering();
            var classification = await GraphQlOperationClassifier.ClassifyAsync(
                context.Request.Body,
                context.RequestAborted);
            context.Request.Body.Position = 0;
            context.Features.Set<IGraphQlOperationFeature>(new GraphQlOperationFeature(classification.Kind));
            if (classification.ExceedsSingleAuthOpLimit)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsJsonAsync(
                    new { error = AuthRateLimiting.TooManyRequestsMessage },
                    context.RequestAborted);
                return;
            }
        }

        await next(context);
    }
}
