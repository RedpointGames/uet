namespace Redpoint.Hashing
{
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Provides additional helpers for hashing data.
    /// </summary>
    public static class Hash
    {
        /// <summary>
        /// Returns the lowercase hexadecimal string that represents the byte sequence.
        /// </summary>
        /// <param name="value">The byte sequence to return the lowercase hexadecimal string for.</param>
        /// <returns>The lowercase hexadecimal string.</returns>
        public static string HexString(ReadOnlySpan<byte> value)
        {
            return BitConverter.ToString(value.ToArray()).Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
        }

        /// <summary>
        /// Computes the SHA256 hash for the byte data and returns it as a lowercase hexadecimal string.
        /// </summary>
        /// <param name="value">The bytes to compute the hash for.</param>
        /// <returns>The lowercase hexadecimal string.</returns>
        public static string Sha256AsHexString(ReadOnlySpan<byte> value)
        {
            return HexString(SHA256.HashData(value));
        }

        /// <summary>
        /// Computes the SHA256 hash for the string value using the specified encoding and returns it as a lowercase hexadecimal string.
        /// </summary>
        /// <param name="value">The string value to compute the hash for.</param>
        /// <param name="encoding">The encoding of the string value.</param>
        /// <returns>The lowercase hexadecimal string.</returns>
        public static string Sha256AsHexString(string value, Encoding encoding)
        {
            if (encoding == null) throw new ArgumentNullException(nameof(encoding));
            return HexString(SHA256.HashData(encoding.GetBytes(value)));
        }

        /// <summary>
        /// Computes the SHA1 hash for the byte data and returns it as a lowercase hexadecimal string.
        /// </summary>
        /// <param name="value">The bytes to compute the hash for.</param>
        /// <returns>The lowercase hexadecimal string.</returns>
        public static string Sha1AsHexString(ReadOnlySpan<byte> value)
        {
            return HexString(SHA1.HashData(value));
        }

        /// <summary>
        /// Computes the SHA1 hash for the string value using the specified encoding and returns it as a lowercase hexadecimal string.
        /// </summary>
        /// <param name="value">The string value to compute the hash for.</param>
        /// <param name="encoding">The encoding of the string value.</param>
        /// <returns>The lowercase hexadecimal string.</returns>
        public static string Sha1AsHexString(string value, Encoding encoding)
        {
            if (encoding == null) throw new ArgumentNullException(nameof(encoding));
            return HexString(SHA1.HashData(encoding.GetBytes(value)));
        }

        /// <summary>
        /// Computes the SHA1 hash for the provided stream and returns it as a lowercase hexadecimal string.
        /// </summary>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <returns>The lowercase hexadecimal string.</returns>
        public static string Sha1AsHexString(Stream stream)
        {
            return HexString(SHA1.HashData(stream));
        }

        /// <summary>
        /// Computes the SHA1 hash for the provided stream and returns it as a lowercase hexadecimal string.
        /// </summary>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="cancellationToken">The cancellation token that can be used to cancel the hashing operation.</param>
        /// <returns>The lowercase hexadecimal string.</returns>
        public static async Task<string> Sha1AsHexStringAsync(Stream stream, CancellationToken cancellationToken)
        {
            return HexString(await SHA1.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        }

        /// <summary>
        /// Retrieves the bytes that make up the GUID and returns a lowercase hexadecimal string representing those bytes.
        /// </summary>
        /// <param name="value">The GUID to use the bytes of.</param>
        /// <returns>The lowercase hexadecimal string.</returns>
        public static string GuidAsHexString(Guid value)
        {
            return HexString(value.ToByteArray());
        }
    }
}