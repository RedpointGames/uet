namespace Redpoint.Unreal.TcpMessaging
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    [SuppressMessage("Design", "CA1064:Exceptions should be public", Justification = "This exception is only thrown and caught internally.")]
    internal class TcpReconnectionRequiredException : Exception
    {
        public TcpReconnectionRequiredException() : base("This stream must have ReconnectAsync() called on it to be used again.")
        {
        }
    }
}
