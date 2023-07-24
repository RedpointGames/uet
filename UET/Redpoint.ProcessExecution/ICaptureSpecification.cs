namespace Redpoint.ProcessExecution
{
    public interface ICaptureSpecification
    {
        bool InterceptRawStreams => false;

        void OnReceiveStreams(Stream? standardInput, Stream? standardOutput, Stream? standardError, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        bool InterceptStandardInput { get; }

        bool InterceptStandardOutput { get; }

        bool InterceptStandardError { get; }

        string? OnRequestStandardInputAtStartup();

        void OnReceiveStandardOutput(string data);

        void OnReceiveStandardError(string data);
    }
}
