namespace Redpoint.Unreal.TcpMessaging
{
    public static class MessageScope
    {
        public const byte Thread = 0;
        public const byte Process = 1;
        public const byte Network = 2;
        public const byte All = 3;
    }
}
