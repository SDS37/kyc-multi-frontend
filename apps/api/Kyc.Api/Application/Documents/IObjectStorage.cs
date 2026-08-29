namespace Kyc.Api.Application.Documents;

/// <summary>Object storage for document bytes (ADR-006). Metadata stays in Postgres.</summary>
public interface IObjectStorage
{
    Task PutAsync(
        string key,
        Stream content,
        string contentType,
        long contentLength,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens object bytes for reading. Returns <c>null</c> if the key is missing.
    /// Caller disposes the stream. Implementations may buffer up to upload size limits.
    /// </summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
