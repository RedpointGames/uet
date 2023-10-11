namespace Redpoint.ProcessExecution
{
    /// <summary>
    /// Represents an object that can receive data from output streams and send data to input streams for a running process. You typically don't want to implement this interface; instead use the predefined static values on <see cref="CaptureSpecification"/> to create instances of this interface.
    /// </summary>
    public interface ICaptureSpecification
    {
        /// <summary>
        /// If true, the standard input stream of the process should be redirected so that data can be sent into it.
        /// </summary>
        bool InterceptStandardInput { get; }

        /// <summary>
        /// If true, the standard output stream of the process should be redirected so that data can be read from it.
        /// </summary>
        bool InterceptStandardOutput { get; }

        /// <summary>
        /// If true, the standard error stream of the process should be redirected so that data can be read from it.
        /// </summary>
        bool InterceptStandardError { get; }

        /// <summary>
        /// If <see cref="InterceptStandardInput"/> is true and this function returns a non-null value, the returned value is sent to the process's standard input stream after the process starts, before the standard input stream is closed.
        /// </summary>
        /// <returns>The data that should be sent to the process's standard input stream after the process starts.</returns>
        string? OnRequestStandardInputAtStartup();

        /// <summary>
        /// Called by the process executor when data arrives from the process's standard output stream.
        /// </summary>
        /// <param name="data">The received data. This value is never null.</param>
        void OnReceiveStandardOutput(string data);

        /// <summary>
        /// Called by the process executor when data arrives from the process's standard error stream.
        /// </summary>
        /// <param name="data">The received data. This value is never null.</param>
        void OnReceiveStandardError(string data);
    }
}
