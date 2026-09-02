using Microsoft.AspNetCore.Http;
using Kyc.Api.Infrastructure;

namespace Kyc.Api.Tests;

public sealed class HttpsRedirectTests
{
    [Fact]
    public void Health_is_not_redirected()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        Assert.False(HttpsRedirect.ShouldRedirect(context));
    }

    [Fact]
    public void Ready_is_not_redirected()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/ready";
        Assert.False(HttpsRedirect.ShouldRedirect(context));
    }

    [Fact]
    public void Graphql_http_is_redirected()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/graphql";
        Assert.True(HttpsRedirect.ShouldRedirect(context));
    }

    [Fact]
    public void Graphql_with_forwarded_https_is_not_redirected()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/graphql";
        context.Request.Headers.Append("X-Forwarded-Proto", "https");
        Assert.False(HttpsRedirect.ShouldRedirect(context));
    }

    [Fact]
    public void Graphql_with_forwarded_http_is_redirected()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/graphql";
        context.Request.Headers.Append("X-Forwarded-Proto", "http");
        Assert.True(HttpsRedirect.ShouldRedirect(context));
    }
}
