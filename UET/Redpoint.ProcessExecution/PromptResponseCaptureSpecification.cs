namespace Redpoint.ProcessExecution
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class PromptResponseCaptureSpecification : ICaptureSpecification
    {
        private readonly CaptureSpecificationPromptResponse _promptResponse;
        private readonly StringBuilder? _stringBuilder;
        private Task? _promptResponseTask = null;

        public PromptResponseCaptureSpecification(CaptureSpecificationPromptResponse promptResponse, StringBuilder? stringBuilder)
        {
            _promptResponse = promptResponse;
            _stringBuilder = stringBuilder;
        }

        private async Task OnReceiveStreamsLoopAsync(StreamWriter standardInput, StreamReader standardOutput, CancellationToken cancellationToken)
        {
            try
            {
                var stringBuilder = new StringBuilder();
                while (!cancellationToken.IsCancellationRequested)
                {
                    var charRead = standardOutput.Read();
                    if (charRead == -1)
                    {
                        // End of stream.
                        return;
                    }

                    var @char = (char)charRead;
                    _stringBuilder?.Append(@char);
                    Console.Write(@char);

                    if (@char == '\n')
                    {
                        // This is the end of a line.
                        stringBuilder.Clear();
                    }
                    else if (@char == '\r')
                    {
                        // Ignore this character.
                    }
                    else
                    {
                        stringBuilder.Append(@char);
                    }

                    if (stringBuilder.Length > 0)
                    {
                        // Check if the line buffer matches any of our prompt responses.
                        var currentLine = stringBuilder.ToString();
                        foreach (var kv in _promptResponse._responses)
                        {
                            if (kv.Key.IsMatch(currentLine))
                            {
                                // This is a prompt we're responding to. Flush the buffer.
                                stringBuilder.Clear();

                                // Let the responder push the input.
                                await kv.Value(standardInput);
                            }
                        }
                    }
                }
            }
            finally
            {
                standardInput.Close();
            }
        }

        public bool InterceptRawStreams => true;

        public bool InterceptStandardInput => true;

        public bool InterceptStandardOutput => true;

        public bool InterceptStandardError => false;

        public void OnReceiveStreams(StreamWriter? standardInput, StreamReader? standardOutput, StreamReader? standardError, CancellationToken cancellationToken)
        {
            if (_promptResponseTask != null)
            {
                throw new InvalidOperationException("PromptResponseCaptureSpecification must not be re-used between executions.");
            }

            _promptResponseTask = Task.Run(
                async () => await OnReceiveStreamsLoopAsync(standardInput!, standardOutput!, cancellationToken),
                cancellationToken);
        }

        public void OnReceiveStandardError(string data)
        {
            throw new NotSupportedException();
        }

        public void OnReceiveStandardOutput(string data)
        {
            throw new NotSupportedException();
        }

        public string? OnRequestStandardInputAtStartup()
        {
            return null;
        }
    }
}
