using System.Security.Cryptography;
using System.Text;

namespace Alify.Core.Infrastructure.Security;

/// <summary>
/// Implements RFC 7636 Proof Key for Code Exchange (PKCE) for public OAuth clients.
/// </summary>
public static class PkceUtil
{
    private const string UnreservedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";

    /// <summary>
    /// Generates a cryptographically random code_verifier string between 43 and 128 characters.
    /// Default length is 64 characters.
    /// </summary>
    public static string GenerateCodeVerifier(int length = 64)
    {
        if (length < 43 || length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "PKCE code_verifier length must be between 43 and 128 characters.");
        }

        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);

        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = UnreservedCharacters[bytes[i] % UnreservedCharacters.Length];
        }

        return new string(chars);
    }

    /// <summary>
    /// Generates the S256 code_challenge from a code_verifier per RFC 7636.
    /// code_challenge = BASE64URL-ENCODE(SHA256(ASCII(code_verifier)))
    /// </summary>
    public static string GenerateCodeChallenge(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);

        byte[] challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Base64UrlEncode(challengeBytes);
    }

    /// <summary>
    /// Encodes a byte array to a URL-safe Base64 string without '=' padding.
    /// </summary>
    public static string Base64UrlEncode(byte[] input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return Convert.ToBase64String(input)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
