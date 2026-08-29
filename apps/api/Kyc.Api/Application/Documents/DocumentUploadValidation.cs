using System.Text;

namespace Kyc.Api.Application.Documents;

/// <summary>Filename / content-type / magic-byte rules for KYC-040 uploads.</summary>
public static class DocumentUploadValidation
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxFileNameLength = 255;

    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg"
    };

    public static string? SanitizeFileName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var name = System.IO.Path.GetFileName(raw.Trim());
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var cleaned = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (ch is < (char)32 or (char)127 || ch is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|')
            {
                cleaned.Append('_');
                continue;
            }

            cleaned.Append(ch);
        }

        var result = cleaned.ToString().Trim('.', ' ');
        if (result.Length == 0)
        {
            return null;
        }

        return result.Length <= MaxFileNameLength ? result : result[..MaxFileNameLength];
    }

    public static string? NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var type = contentType.Split(';', 2)[0].Trim();
        if (type.Equals("image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            type = "image/jpeg";
        }

        return AllowedContentTypes.Contains(type) ? type.ToLowerInvariant() : null;
    }

    public static bool MatchesMagicBytes(string contentType, ReadOnlySpan<byte> header)
    {
        if (header.Length < 4)
        {
            return false;
        }

        return contentType switch
        {
            "application/pdf" => header[0] == (byte)'%' && header[1] == (byte)'P' && header[2] == (byte)'D' && header[3] == (byte)'F',
            "image/png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            "image/jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            _ => false
        };
    }

    public static string BuildStorageKey(Guid tenantId, Guid caseId, Guid documentId, string safeFileName) =>
        $"tenants/{tenantId:N}/cases/{caseId:N}/{documentId:N}/{safeFileName}";
}
