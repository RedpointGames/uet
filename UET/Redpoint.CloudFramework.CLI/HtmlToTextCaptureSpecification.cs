namespace Redpoint.CloudFramework.CLI
{
    using Redpoint.ProcessExecution;
    using System;
    using System.Text;

    internal class HtmlToTextCaptureSpecification : ICaptureSpecification
    {
        private readonly string _input;
        private readonly StringBuilder _output;
        private int _emptyNewLineCount;

        public HtmlToTextCaptureSpecification(string input, StringBuilder output)
        {
            _input = input;
            _output = output;
            _emptyNewLineCount = 0;
        }

        public bool InterceptStandardInput => true;

        public bool InterceptStandardOutput => true;

        public bool InterceptStandardError => false;

        public void OnReceiveStandardError(string data)
        {
            throw new NotImplementedException();
        }

        public void OnReceiveStandardOutput(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                _emptyNewLineCount++;
            }
            else
            {
                _emptyNewLineCount = 0;
            }
            if (_emptyNewLineCount <= 1)
            {
                _output.AppendLine(data);
            }
        }

        public string? OnRequestStandardInputAtStartup()
        {
            return _input;
        }
    }
}
