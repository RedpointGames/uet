namespace Redpoint.Uba
{
    /// <summary>
    /// A factory interface that constructs instances of <see cref="IUbaServer"/>.
    /// </summary>
    public interface IUbaServerFactory
    {
        /// <summary>
        /// Creates a UBA server for running processes on remote machines. The returned instance must be disposed when no longer needed.
        /// </summary>
        /// <param name="rootStorageDirectoryPath">The fully qualified directory under which UBA will cache/store files as needed.</param>
        /// <param name="ubaTraceFilePath">The fully qualified path that UBA should write the trace file to.</param>
        /// <param name="maxWorkers">The maximum number of remote agents this UBA server can connect to.</param>
        /// <param name="sendSize">The maximum message size that this server can send to remote agents.</param>
        /// <param name="receiveTimeoutSeconds">The timeout in seconds after which a remote agent will be considered disconnected.</param>
        /// <param name="useQuic">If true, use QUIC instead of TCP to connect to remote agents.</param>
        /// <returns>An instance of <see cref="IUbaServer"/>.</returns>
        IUbaServer CreateServer(
            string rootStorageDirectoryPath,
            string ubaTraceFilePath,
            int maxWorkers = 192,
            int sendSize = 256 * 1024,
            int receiveTimeoutSeconds = 60,
            bool useQuic = false);
    }
}
