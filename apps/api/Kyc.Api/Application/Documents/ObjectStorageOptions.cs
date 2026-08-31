namespace Kyc.Api.Application.Documents;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>
    /// Required. <c>Minio</c> for local/prod hosts; <c>InMemory</c> only in Development/Testing.
    /// Empty provider fails closed at startup (see Program.cs) — copy appsettings.Development.json.example.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    public string Endpoint { get; set; } = "http://127.0.0.1:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "kyc-documents";
    public bool ForcePathStyle { get; set; } = true;
}
