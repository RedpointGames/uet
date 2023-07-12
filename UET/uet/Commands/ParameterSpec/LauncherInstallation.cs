namespace UET.Commands.ParameterSpec
{
    using System.Text.Json.Serialization;

    internal class LauncherInstallation
    {
        [JsonPropertyName("InstallLocation")]
        public string? InstallLocation { get; set; }

        [JsonPropertyName("AppName")]
        public string? AppName { get; set; }
    }
}
