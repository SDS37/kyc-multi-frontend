using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Kyc.Api.Infrastructure;

namespace Kyc.Api.Tests;

public sealed class SecurityHeadersTests(ApiFactory development, ProductionApiFactory production)
    : IClassFixture<ApiFactory>, IClassFixture<ProductionApiFactory>
{
    [Fact]
    public async Task Development_health_sends_csp_and_does_not_redirect()
    {
        using var client = development.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            SecurityHeaders.ContentSecurityPolicy,
            Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task Production_https_health_sends_csp()
    {
        using var client = production.CreateClient();
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            SecurityHeaders.ContentSecurityPolicy,
            Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task Production_http_health_does_not_redirect()
    {
        using var client = production.CreateHttpClientNoRedirect();
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Production_http_ready_does_not_redirect()
    {
        using var client = production.CreateHttpClientNoRedirect();
        using var response = await client.GetAsync("/ready");

        Assert.NotEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Production_http_graphql_redirects_to_https()
    {
        using var client = production.CreateHttpClientNoRedirect();
        using var response = await client.PostAsync(
            "/graphql",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(Uri.UriSchemeHttps, response.Headers.Location.Scheme);
        Assert.Equal("/graphql", response.Headers.Location.PathAndQuery);
    }

    [Fact]
    public async Task Production_http_graphql_with_forwarded_https_does_not_redirect()
    {
        using var client = production.CreateHttpClientNoRedirect();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.TemporaryRedirect, response.StatusCode);
    }
}
