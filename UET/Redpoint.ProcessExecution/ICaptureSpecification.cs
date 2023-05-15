namespace Redpoint.ProcessExecution
{
    public interface ICaptureSpecification
    {
        bool InterceptStandardInput { get; }

        bool InterceptStandardOutput { get; }

        bool InterceptStandardError { get; }

        string? OnRequestStandardInputAtStartup();

        void OnReceiveStandardOutput(string data);

        void OnReceiveStandardError(string data);
    }
}
