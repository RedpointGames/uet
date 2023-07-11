namespace Redpoint.ProcessExecution
{
    using System.Text;

    internal class SanitizedStringBuilderCaptureSpecification : ICaptureSpecification
    {
        private readonly StringBuilder _stdout;

        public SanitizedStringBuilderCaptureSpecification(StringBuilder stdout)
        {
            _stdout = stdout;
        }

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
            Console.Error.WriteLine(sb.ToString().Replace("\\x1B[K", string.Empty));
        }

        public void OnReceiveStandardOutput(string data)
        {
            _stdout.AppendLine(data);
        }

        public string? OnRequestStandardInputAtStartup()
        {
            throw new NotSupportedException();
        }
    }
}
