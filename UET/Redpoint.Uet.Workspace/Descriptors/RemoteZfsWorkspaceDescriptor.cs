namespace Redpoint.Uet.Workspace.Descriptors
{
    using System.Globalization;

    public record class RemoteZfsWorkspaceDescriptor : IWorkspaceDescriptor
    {
        public required string Hostname { get; set; }

        public required int Port { get; set; }

        public required string TemplateId { get; set; }

        public required string Subpath { get; set; }

        public static RemoteZfsWorkspaceDescriptor Parse(string spec)
        {
            ArgumentNullException.ThrowIfNull(spec);

            var components = spec.Replace('\\', '/').Split('/', 3);
            if (components.Length < 2)
            {
                throw new ArgumentException("Invalid remote ZFS spec; expected 'host:port/template-id/optional-subpath'.", nameof(spec));
            }

            var hostPort = components[0].Split(':');
            var templateId = components[1];
            var subpath = components.Length > 2 ? components[2] : string.Empty;

            var host = hostPort[0];
            var port = hostPort.Length > 1 ? int.Parse(hostPort[1], CultureInfo.InvariantCulture) : 9000;

            return new RemoteZfsWorkspaceDescriptor
            {
                Hostname = host,
                Port = port,
                TemplateId = templateId,
                Subpath = subpath,
            };
        }
    }
}
