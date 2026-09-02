using Kyc.Api.Infrastructure;

namespace Kyc.Api.Tests;

public sealed class GraphQlOperationClassifierTests
{
    [Fact]
    public void Login_mutation_is_login()
    {
        var kind = GraphQlOperationClassifier.ClassifyJson("""
            { "query": "mutation($input: LoginRequestInput!) { login(input: $input) { accessToken } }" }
            """);

        Assert.Equal(GraphQlOperationKind.Login, kind);
    }

    [Fact]
    public void Register_mutation_is_register()
    {
        var kind = GraphQlOperationClassifier.ClassifyJson("""
            { "query": "mutation { registerTenant(input: { tenantName: \"A\" }) { tenantSlug } }" }
            """);

        Assert.Equal(GraphQlOperationKind.Register, kind);
    }

    [Fact]
    public void OperationName_login_is_login()
    {
        var kind = GraphQlOperationClassifier.ClassifyJson("""
            { "operationName": "login", "query": "query { apiStatus }" }
            """);

        Assert.Equal(GraphQlOperationKind.Login, kind);
    }

    [Fact]
    public void Cases_query_is_other()
    {
        var kind = GraphQlOperationClassifier.ClassifyJson("""
            { "query": "query { cases { totalCount } }" }
            """);

        Assert.Equal(GraphQlOperationKind.Other, kind);
    }

    [Fact]
    public void Batch_with_register_uses_stricter_kind()
    {
        var kind = GraphQlOperationClassifier.ClassifyJson("""
            [
              { "query": "mutation { login(input: {}) { accessToken } }" },
              { "query": "mutation { registerTenant(input: {}) { tenantSlug } }" }
            ]
            """);

        Assert.Equal(GraphQlOperationKind.Register, kind);
    }

    [Fact]
    public void Invalid_json_is_other()
    {
        Assert.Equal(GraphQlOperationKind.Other, GraphQlOperationClassifier.ClassifyJson("not-json"));
    }

    [Fact]
    public void Variable_values_named_login_do_not_classify_as_login()
    {
        var kind = GraphQlOperationClassifier.ClassifyJson("""
            {
              "query": "query($title: String!) { cases { items { id } } }",
              "variables": { "title": "mutation { login(input: {}) { accessToken } }" }
            }
            """);

        Assert.Equal(GraphQlOperationKind.Other, kind);
    }
}
