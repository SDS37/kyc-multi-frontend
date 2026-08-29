namespace Kyc.Api.Application.Documents;

public sealed class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    /// <summary>
    /// <c>Minio</c> when fully configured; blank or <c>InMemory</c> uses the in-process store
    /// (tests, design-time, and hosts that have not configured MinIO yet — see Program.cs).
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    public string Endpoint { get; set; } = "http://127.0.0.1:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BucketName { get; set; } = "kyc-documents";
    public bool ForcePathStyle { get; set; } = true;
}
