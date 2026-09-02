using System.Security.Cryptography;
using System.Text;

namespace Kyc.Api.Application.Identity;

/// <summary>SHA-256 hex of a trimmed invite code. Codes are high-entropy; this is lookup, not password hashing.</summary>
public static class InviteCodeHasher
{
    public static string Hash(string code)
    {
        var normalized = code.Trim();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }
}
