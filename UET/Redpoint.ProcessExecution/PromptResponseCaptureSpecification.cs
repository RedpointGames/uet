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
        private Task? _promptResponseTask = null;

        public PromptResponseCaptureSpecification(CaptureSpecificationPromptResponse promptResponse)
        {
            _promptResponse = promptResponse;
        }

        private async Task OnReceiveStreamsLoopAsync(StreamWriter standardInput, StreamReader standardOutput, CancellationToken cancellationToken)
        {
            try
            {
                var memoryBuffer = new char[128];
                var stringBuilder = new StringBuilder();
                while (!cancellationToken.IsCancellationRequested)
                {
                    var charsRead = await standardOutput.ReadAsync(memoryBuffer, cancellationToken);
                    if (charsRead == 0)
                    {
                        // End of stream.
                        return;
                    }

                    for (int i = 0; i < charsRead; i++)
                    {
                        if (memoryBuffer[i] == '\n')
                        {
                            // This is the end of a line. Flush the buffer.
                            Console.WriteLine(stringBuilder.ToString());
                            stringBuilder.Clear();
                        }
                        else if (memoryBuffer[i] == '\r')
                        {
                            // Ignore this character.
                        }
                        else
                        {
                            stringBuilder.Append(memoryBuffer[i]);
                        }
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
                                Console.WriteLine(stringBuilder.ToString());
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
