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

        private async Task OnReceiveStreamsLoopAsync(Stream standardInput, Stream standardOutput, CancellationToken cancellationToken)
        {
            try
            {
                var memoryBuffer = new byte[128];
                var lineBuffer = new List<byte>();
                while (!cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await standardOutput.ReadAsync(memoryBuffer, cancellationToken);
                    if (bytesRead == 0)
                    {
                        // End of stream.
                        return;
                    }

                    for (int i = 0; i < bytesRead; i++)
                    {
                        if (memoryBuffer[i] == '\n')
                        {
                            // This is the end of a line. Flush the buffer.
                            Console.WriteLine(Encoding.UTF8.GetString(lineBuffer.ToArray()));
                            lineBuffer.Clear();
                        }
                        else if (memoryBuffer[i] == '\r')
                        {
                            // Ignore this character.
                        }
                        else
                        {
                            lineBuffer.Add(memoryBuffer[i]);
                        }
                    }

                    if (lineBuffer.Count > 0)
                    {
                        // Check if the line buffer matches any of our prompt responses.
                        var currentLine = Encoding.UTF8.GetString(lineBuffer.ToArray());
                        foreach (var kv in _promptResponse._responses)
                        {
                            if (kv.Key.IsMatch(currentLine))
                            {
                                // This is a prompt we're responding to. Flush the buffer.
                                Console.WriteLine(Encoding.UTF8.GetString(lineBuffer.ToArray()));
                                lineBuffer.Clear();

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

        public void OnReceiveStreams(Stream? standardInput, Stream? standardOutput, Stream? standardError, CancellationToken cancellationToken)
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
