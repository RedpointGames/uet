namespace Redpoint.CloudFramework.Startup
{
    using System.Collections.Generic;

    public record class DevelopmentDockerContainer
    {
        public required string Name { get; set; }
        public string? Context { get; set; }
        public string? Image { get; set; }
        public string Dockerfile { get; set; } = "Dockerfile";
        public IReadOnlyCollection<DeveloperDockerPort> Ports { get; set; } = Array.Empty<DeveloperDockerPort>();
        public IReadOnlyDictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();
        public IReadOnlyList<string> Arguments { get; set; } = Array.Empty<string>();
        internal string? ImageId { get; set; }
    }
}
