using System.Security.Cryptography;
using System.Text;

namespace Tangle.AspNetCore.Security;

/// <summary>
/// Constant-time secret comparison for gateway / internal access headers.
/// </summary>
public static class SecretComparer
{
    public static bool Equals(string? provided, string? expected)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(expected))
            return false;

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
