using System.Net;

namespace Kyc.Api.Tests;

public sealed class CorsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string AllowedOrigin = "http://localhost:4200";
    private const string OtherLocalOrigin = "http://localhost:5173";
    private const string VueOrigin = "http://localhost:5174";
    private const string DeniedOrigin = "http://evil.example";

    [Fact]
    public async Task Allowed_origin_preflight_to_graphql_is_ok()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/graphql");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        var methods = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods"));
        Assert.Contains("POST", methods, StringComparison.OrdinalIgnoreCase);
        var headers = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers"));
        Assert.Contains("Authorization", headers, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Allowed_origin_preflight_to_document_download_is_ok()
    {
        using var client = factory.CreateClient();
        var path = $"/api/cases/{Guid.NewGuid()}/documents/{Guid.NewGuid()}";
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", OtherLocalOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(OtherLocalOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Vue_origin_preflight_to_graphql_is_ok()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/graphql");
        request.Headers.Add("Origin", VueOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(VueOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Denied_origin_preflight_has_no_allow_origin()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/graphql");
        request.Headers.Add("Origin", DeniedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        using var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Allowed_origin_on_health_exposes_request_id()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains(
            "X-Request-Id",
            response.Headers.GetValues("Access-Control-Expose-Headers"),
            StringComparer.OrdinalIgnoreCase);
    }
}
