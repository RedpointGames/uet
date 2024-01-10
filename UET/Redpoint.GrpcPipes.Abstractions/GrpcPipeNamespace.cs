namespace Redpoint.GrpcPipes
{
    /// <summary>
    /// The namespace of the gRPC pipe.
    /// </summary>
    public enum GrpcPipeNamespace
    {
        /// <summary>
        /// This pipe is available only to the user that created the pipe server.
        /// </summary>
        User,

        /// <summary>
        /// This pipe is available system-wide.
        /// </summary>
        Computer,
    }
}