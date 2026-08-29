using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Kyc.Api.Application.Documents;

/// <summary>
/// S3-compatible MinIO adapter. Bucket is created without a public policy — clients never read objects
/// directly; downloads go through the authenticated API (KYC-042).
/// </summary>
public sealed partial class MinioObjectStorage : IObjectStorage, IAsyncDisposable
{
    private readonly AmazonS3Client _s3;
    private readonly string _bucket;
    private readonly ILogger<MinioObjectStorage> _logger;
    private readonly SemaphoreSlim _bucketGate = new(1, 1);
    private bool _bucketReady;

    public MinioObjectStorage(IOptions<ObjectStorageOptions> options, ILogger<MinioObjectStorage> logger)
    {
        _logger = logger;
        var o = options.Value;
        _bucket = o.BucketName;

        var config = new AmazonS3Config
        {
            ServiceURL = o.Endpoint.TrimEnd('/'),
            ForcePathStyle = o.ForcePathStyle,
            AuthenticationRegion = "us-east-1"
        };

        _s3 = new AmazonS3Client(o.AccessKey, o.SecretKey, config);
    }

    public async Task PutAsync(
        string key,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);

        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            Headers = { ContentLength = contentLength }
        };

        await _s3.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);

        try
        {
            // Buffer within KYC-040 max size so ASP.NET can set Content-Length and dispose cleanly.
            // Presigned URLs are deferred — browser/CORS and InMemory tests favor API streaming for MVP.
            using var response = await _s3.GetObjectAsync(_bucket, key, cancellationToken);
            var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            return buffer;
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden
            || string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.Ordinal))
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3.DeleteObjectAsync(_bucket, key, cancellationToken);
        }
        catch (Exception ex)
        {
            LogDeleteFailed(_logger, ex, key);
        }
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady)
        {
            return;
        }

        await _bucketGate.WaitAsync(cancellationToken);
        try
        {
            if (_bucketReady)
            {
                return;
            }

            var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3, _bucket);
            if (!exists)
            {
                await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, cancellationToken);
            }

            _bucketReady = true;
        }
        finally
        {
            _bucketGate.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _s3.Dispose();
        _bucketGate.Dispose();
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete object {StorageKey}")]
    private static partial void LogDeleteFailed(ILogger logger, Exception ex, string storageKey);
}
