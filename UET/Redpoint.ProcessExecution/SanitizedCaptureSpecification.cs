namespace Redpoint.ProcessExecution
{
    using System.Globalization;
    using System.Text;

    internal class SanitizedCaptureSpecification : ICaptureSpecification
    {
        public bool InterceptStandardOutput => true;

        public bool InterceptStandardError => true;

        public bool InterceptStandardInput => false;

        public void OnReceiveStandardError(string data)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                var c = data[i];
                if (c < ' ')
                {
                    sb.Append(CultureInfo.InvariantCulture, $"\\x{(int)c:X}");
                }
                else
                {
                    sb.Append(c);
                }
            }
            Console.Error.WriteLine(sb.ToString().Replace("\\x1B[K", string.Empty, StringComparison.Ordinal));
        }

        public void OnReceiveStandardOutput(string data)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                var c = data[i];
                if (c < ' ')
                {
                    sb.Append(CultureInfo.InvariantCulture, $"\\x{(int)c:X}");
                }
                else
                {
                    sb.Append(c);
                }
            }
            Console.WriteLine(sb.ToString().Replace("\\x1B[K", string.Empty, StringComparison.Ordinal));
        }

        public string? OnRequestStandardInputAtStartup()
        {
            throw new NotSupportedException();
        }
    }
}
