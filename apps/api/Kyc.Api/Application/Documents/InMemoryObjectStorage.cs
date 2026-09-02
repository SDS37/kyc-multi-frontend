using System.Collections.Concurrent;

namespace Kyc.Api.Application.Documents;

/// <summary>In-process store for tests (roadmap allows a non-MinIO backend behind the same interface).</summary>
public sealed class InMemoryObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public async Task PutAsync(
        string key,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        await using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        _objects[key] = ms.ToArray();
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_objects.TryGetValue(key, out var bytes))
        {
            return Task.FromResult<Stream?>(null);
        }

        if (bytes.LongLength > DocumentUploadValidation.MaxFileBytes)
        {
            throw new InvalidOperationException(
                $"Object exceeds maximum size of {DocumentUploadValidation.MaxFileBytes} bytes.");
        }

        return Task.FromResult<Stream?>(new MemoryStream(bytes, writable: false));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public bool Contains(string key) => _objects.ContainsKey(key);

    public int ObjectCount => _objects.Count;
}
