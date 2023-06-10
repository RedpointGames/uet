namespace Redpoint.ProcessExecution
{
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
                    sb.Append($"\\x{(int)c:X}");
                }
                else
                {
                    sb.Append(c);
                }
            }
            Console.Error.WriteLine(sb.ToString());
        }

        public void OnReceiveStandardOutput(string data)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++)
            {
                var c = data[i];
                if (c < ' ')
                {
                    sb.Append($"\\x{(int)c:X}");
                }
                else
                {
                    sb.Append(c);
                }
            }
            Console.WriteLine(sb.ToString());
        }

        public string? OnRequestStandardInputAtStartup()
        {
            throw new NotSupportedException();
        }
    }
}
