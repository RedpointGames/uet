namespace Redpoint.Hashing
{
    using System.IO.Hashing;
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
            return Convert.ToHexString(value.ToArray()).ToLowerInvariant();
        }

        /// <summary>
        /// Returns the lowercase hexadecimal string that represents the signed 64-bit integer.
        /// </summary>
        /// <param name="value">The signed 64-bit integer to return the lowercase hexadecimal string for.</param>
        /// <returns>The lowercase hexadecimal string.</returns>
        public static string HexString(long value)
        {
            return Convert.ToHexString(BitConverter.GetBytes(value)).ToLowerInvariant();
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
        /// Computes the SHA256 hash for the stream and returns it as a lowercase hexadecimal string.
        /// </summary>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <returns>The lowercase hexadecimal string.</returns>
        public static string Sha256AsHexString(Stream stream)
        {
            return HexString(SHA256.HashData(stream));
        }

        /// <summary>
        /// Computes the SHA256 hash for the string value using the specified encoding and returns it as a lowercase hexadecimal string.
        /// </summary>
        /// <param name="value">The string value to compute the hash for.</param>
        /// <param name="encoding">The encoding of the string value.</param>
        /// <returns>The lowercase hexadecimal string.</returns>
        public static string Sha256AsHexString(string value, Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(encoding);
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
            ArgumentNullException.ThrowIfNull(encoding);
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

        /// <summary>
        /// Compute the xxHash64 of the specified file and return both the hash and byte length of the file.
        /// </summary>
        /// <param name="path">The full path to the file to compute the hash of.</param>
        /// <param name="cancellationToken">The cancellation token used to cancel the asynchronous operation.</param>
        /// <returns>The xxHash64 hash value and the byte length of the original file.</returns>
        public static async Task<XxHash64WithLength> XxHash64OfFileAsync(string path, CancellationToken cancellationToken)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hasher = new XxHash64();
                await hasher.AppendAsync(stream, cancellationToken).ConfigureAwait(false);
                return new XxHash64WithLength(BitConverter.ToInt64(hasher.GetCurrentHash()), stream.Length);
            }
        }

        /// <summary>
        /// Compute the xxHash64 of the specified string content and return both the hash and byte length of the string content.
        /// </summary>
        /// <param name="value">The string value to hash.</param>
        /// <param name="encoding">The encoding of the string value.</param>
        /// <returns>The xxHash64 hash value and the byte length of the original file.</returns>
        public static XxHash64WithLength XxHash64(string value, Encoding encoding)
        {
            ArgumentNullException.ThrowIfNull(encoding);
            var bytes = Encoding.UTF8.GetBytes(value);
            return new XxHash64WithLength(BitConverter.ToInt64(System.IO.Hashing.XxHash64.Hash(encoding.GetBytes(value))), bytes.Length);
        }
    }
}