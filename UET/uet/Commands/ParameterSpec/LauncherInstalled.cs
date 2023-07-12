namespace UET.Commands.ParameterSpec
{
    using System.Text.Json.Serialization;

    internal class LauncherInstalled
    {
        [JsonPropertyName("InstallationList")]
        public LauncherInstallation[]? InstallationList { get; set; }
    }
}
