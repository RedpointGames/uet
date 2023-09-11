namespace Redpoint.OpenGE.Core
{
    using System;
    using System.Threading.Tasks;
    using System.IO.Hashing;
    using System.Text;
    using Redpoint.Hashing;

    public static class XxHash64Helpers
    {
        public static async Task<(long hash, long byteLength)> HashFile(string path, CancellationToken cancellationToken)
        {
            var hash = await Hash.XxHash64OfFileAsync(path, cancellationToken).ConfigureAwait(false);
            return (hash.Hash, hash.ByteLength);
        }

        public static (long hash, long byteLength) HashString(string content)
        {
            var hash = Hash.XxHash64(content, Encoding.UTF8);
            return (hash.Hash, hash.ByteLength);
        }

        public static string HexString(this long hash)
        {
            return Hash.HexString(hash);
        }
    }
}
