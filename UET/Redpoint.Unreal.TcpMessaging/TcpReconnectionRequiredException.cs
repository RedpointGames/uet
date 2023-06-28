namespace Redpoint.Unreal.TcpMessaging
{
    using System;

    internal class TcpReconnectionRequiredException : Exception
    {
        public TcpReconnectionRequiredException() : base("This stream must have ReconnectAsync() called on it to be used again.")
        {
        }
    }
}
