namespace Redpoint.CloudFramework.Startup
{
    public record struct DeveloperDockerPort
    {
        public DeveloperDockerPort(ushort containerPort, ushort hostPort)
        {
            ContainerPort = containerPort;
            HostPort = hostPort;
        }

        public ushort ContainerPort { get; set; }

        public ushort HostPort { get; set; }

        public static implicit operator DeveloperDockerPort(ushort d) => new DeveloperDockerPort(d, d);

        public static DeveloperDockerPort FromUInt16(ushort d) => new DeveloperDockerPort(d, d);
    }
}
