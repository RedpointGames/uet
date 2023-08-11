namespace Redpoint.OpenGE.Core
{
    using System;
    using System.Threading.Tasks;
    using System.IO.Hashing;
    using System.Text;

    public static class XxHash64Helpers
    {
        public static async Task<(long hash, long byteLength)> HashFile(string path, CancellationToken cancellationToken)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var hasher = new XxHash64();
                await hasher.AppendAsync(stream, cancellationToken);
                return (BitConverter.ToInt64(hasher.GetCurrentHash()), stream.Length);
            }
        }

        public static (long hash, long byteLength) HashString(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            return (BitConverter.ToInt64(XxHash64.Hash(bytes)), bytes.Length);
        }
    }
}
