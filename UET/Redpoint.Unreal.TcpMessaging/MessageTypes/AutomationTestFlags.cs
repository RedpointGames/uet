namespace Redpoint.Unreal.TcpMessaging.MessageTypes
{
    public static class AutomationTestFlags
    {
        public const uint None = 0x00000000;
        public const uint EditorContext = 0x00000001;
        public const uint ClientContext = 0x00000002;
        public const uint ServerContext = 0x00000004;
        public const uint CommandletContext = 0x00000008;
        public const uint NonNullRHI = 0x00000100;
        public const uint RequiresUser = 0x00000200;
        public const uint Disabled = 0x00010000;
        public const uint CriticalPriority = 0x00100000;
        public const uint HighPriority = 0x00200000;
        public const uint MediumPriority = 0x00400000;
        public const uint LowPriority = 0x00800000;
        public const uint SmokeFilter = 0x01000000;
        public const uint EngineFilter = 0x02000000;
        public const uint ProductFilter = 0x04000000;
        public const uint PerfFilter = 0x08000000;
        public const uint StressFilter = 0x10000000;
        public const uint NegativeFilter = 0x20000000;
    }
}
