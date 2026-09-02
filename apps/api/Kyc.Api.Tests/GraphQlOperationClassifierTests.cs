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
    public async Task Truncated_peek_fails_closed_to_register_bucket()
    {
        var pad = new string('x', GraphQlOperationClassifier.MaxPeekBytes);
        var json = $$"""{ "variables": { "pad": "{{pad}}" }, "query": "mutation { login(input: {}) { accessToken } }" }""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var kind = await GraphQlOperationClassifier.ClassifyAsync(stream, CancellationToken.None);

        Assert.True(json.Length > GraphQlOperationClassifier.MaxPeekBytes);
        Assert.Equal(GraphQlOperationKind.Register, kind);
    }

    [Fact]
    public async Task Short_reads_still_classify_a_small_login()
    {
        const string json =
            """{ "query": "mutation { login(input: {}) { accessToken } }" }""";
        using var stream = new OneByteReadStream(Encoding.UTF8.GetBytes(json));

        var kind = await GraphQlOperationClassifier.ClassifyAsync(stream, CancellationToken.None);

        Assert.Equal(GraphQlOperationKind.Login, kind);
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
