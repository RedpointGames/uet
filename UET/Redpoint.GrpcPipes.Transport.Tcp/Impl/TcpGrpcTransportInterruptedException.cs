namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    using System;
    using System.Diagnostics.CodeAnalysis;
#pragma warning disable CA1064 // This exception class should not escape to the public API surface.
    internal class TcpGrpcTransportInterruptedException : Exception
#pragma warning restore CA1064
    {
        public static void ThrowIf([DoesNotReturnIf(true)] bool condition)
        {
            if (condition)
            {
                throw new TcpGrpcTransportInterruptedException();
            }
        }
    }
}
