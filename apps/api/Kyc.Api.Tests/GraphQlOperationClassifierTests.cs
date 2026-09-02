using System.Text;
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
    public void Named_login_operation_is_one_login_field()
    {
        var classified = GraphQlOperationClassifier.ClassifyDocument("""
            { "query": "mutation Login($input: LoginRequestInput!) { login(input: $input) { accessToken tokenType expiresInSeconds } }" }
            """);

        Assert.Equal(GraphQlOperationKind.Login, classified.Kind);
        Assert.Equal(1, classified.LoginFieldCount);
        Assert.False(classified.ExceedsSingleAuthOpLimit);
    }

    [Fact]
    public void Named_register_operation_is_one_register_field()
    {
        var classified = GraphQlOperationClassifier.ClassifyDocument("""
            { "query": "mutation RegisterTenant($input: RegisterTenantRequestInput!) { registerTenant(input: $input) { tenantSlug } }" }
            """);

        Assert.Equal(GraphQlOperationKind.Register, classified.Kind);
        Assert.Equal(1, classified.RegisterFieldCount);
        Assert.False(classified.ExceedsSingleAuthOpLimit);
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
    public void Mismatched_operationName_uses_stricter_query_kind()
    {
        var kind = GraphQlOperationClassifier.ClassifyJson("""
            { "operationName": "login", "query": "mutation { registerTenant(input: {}) { tenantSlug } }" }
            """);

        Assert.Equal(GraphQlOperationKind.Register, kind);
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
    public void Aliased_double_login_exceeds_single_op_limit()
    {
        var classified = GraphQlOperationClassifier.ClassifyDocument("""
            { "query": "mutation { a: login(input: {}) { accessToken } b: login(input: {}) { accessToken } }" }
            """);

        Assert.Equal(2, classified.LoginFieldCount);
        Assert.True(classified.ExceedsSingleAuthOpLimit);
    }

    [Fact]
    public void Named_operation_with_aliased_double_login_still_exceeds()
    {
        var classified = GraphQlOperationClassifier.ClassifyDocument("""
            { "query": "mutation Login { a: login(input: {}) { accessToken } b: login(input: {}) { accessToken } }" }
            """);

        Assert.Equal(2, classified.LoginFieldCount);
        Assert.True(classified.ExceedsSingleAuthOpLimit);
    }

    [Fact]
    public void Json_batch_of_two_logins_exceeds_single_op_limit()
    {
        var classified = GraphQlOperationClassifier.ClassifyDocument("""
            [
              { "query": "mutation { login(input: {}) { accessToken } }" },
              { "query": "mutation { login(input: {}) { accessToken } }" }
            ]
            """);

        Assert.Equal(2, classified.LoginFieldCount);
        Assert.Equal(GraphQlOperationKind.Login, classified.Kind);
        Assert.True(classified.ExceedsSingleAuthOpLimit);
    }

    [Fact]
    public void One_login_and_one_register_exceeds_single_op_limit()
    {
        var classified = GraphQlOperationClassifier.ClassifyDocument("""
            { "query": "mutation { login(input: {}) { accessToken } registerTenant(input: {}) { tenantSlug } }" }
            """);

        Assert.Equal(1, classified.LoginFieldCount);
        Assert.Equal(1, classified.RegisterFieldCount);
        Assert.True(classified.ExceedsSingleAuthOpLimit);
        Assert.Equal(GraphQlOperationKind.Register, classified.Kind);
    }

    [Fact]
    public void Login_with_hash_comment_before_args_is_login()
    {
        var classified = GraphQlOperationClassifier.ClassifyDocument("""
            { "query": "mutation { login # x\n(input: {}) { accessToken } }" }
            """);

        Assert.Equal(GraphQlOperationKind.Login, classified.Kind);
        Assert.Equal(1, classified.LoginFieldCount);
        Assert.False(classified.ExceedsSingleAuthOpLimit);
    }

    [Fact]
    public void Register_with_hash_comment_before_args_is_register()
    {
        var classified = GraphQlOperationClassifier.ClassifyDocument("""
            { "query": "mutation { registerTenant # x\n(input: {}) { tenantSlug } }" }
            """);

        Assert.Equal(GraphQlOperationKind.Register, classified.Kind);
        Assert.Equal(1, classified.RegisterFieldCount);
    }

    [Fact]
    public void Hash_comment_containing_login_is_not_login()
    {
        var kind = GraphQlOperationClassifier.ClassifyJson("""
            { "query": "query { apiStatus # login(input: {})\n }" }
            """);

        Assert.Equal(GraphQlOperationKind.Other, kind);
    }

    [Fact]
    public void Hash_inside_string_does_not_strip_login()
    {
        var classified = GraphQlOperationClassifier.ClassifyDocument("""
            { "query": "mutation { login(input: { tenantSlug: \"acme#x\" }) { accessToken } }" }
            """);

        Assert.Equal(GraphQlOperationKind.Login, classified.Kind);
        Assert.Equal(1, classified.LoginFieldCount);
    }

    [Fact]
    public async Task Truncated_peek_fails_closed_to_register_bucket()
    {
        var pad = new string('x', GraphQlOperationClassifier.MaxPeekBytes);
        var json = $$"""{ "variables": { "pad": "{{pad}}" }, "query": "mutation { login(input: {}) { accessToken } }" }""";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var kind = await GraphQlOperationClassifier.ClassifyAsync(stream, CancellationToken.None);

        Assert.True(json.Length > GraphQlOperationClassifier.MaxPeekBytes);
        Assert.Equal(GraphQlOperationKind.Register, kind.Kind);
        Assert.True(kind.ExceedsSingleAuthOpLimit);
    }

    [Fact]
    public async Task Short_reads_still_classify_a_small_login()
    {
        const string json =
            """{ "query": "mutation { login(input: {}) { accessToken } }" }""";
        await using var stream = new OneByteReadStream(Encoding.UTF8.GetBytes(json));

        var kind = await GraphQlOperationClassifier.ClassifyAsync(stream, CancellationToken.None);

        Assert.Equal(GraphQlOperationKind.Login, kind.Kind);
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

internal sealed class OneByteReadStream(byte[] data) : Stream
{
    private int _offset;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => data.Length;
    public override long Position
    {
        get => _offset;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_offset >= data.Length || count <= 0)
        {
            return 0;
        }

        buffer[offset] = data[_offset];
        _offset++;
        return 1;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
