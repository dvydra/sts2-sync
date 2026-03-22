using System.Security.Cryptography;
using System.Text;

namespace Sts2Sync.Core.Services;

public static class SaveFileHasher
{
    public static string ComputeSha256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }

    public static string ComputeSha256(string content)
        => ComputeSha256(Encoding.UTF8.GetBytes(content));

    /// <summary>
    /// SHA-1 hash as used by Steam Cloud for upload verification.
    /// </summary>
    public static string ComputeSha1(byte[] data)
    {
        var hash = SHA1.HashData(data);
        return Convert.ToHexStringLower(hash);
    }

    public static string ComputeSha1(string content)
        => ComputeSha1(Encoding.UTF8.GetBytes(content));

    public static bool AreEqual(byte[] left, byte[] right)
        => left.AsSpan().SequenceEqual(right.AsSpan());
}
