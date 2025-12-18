namespace Redpoint.Uet.Commands.ParameterSpec
{
    using System.Text.Json.Serialization;

    internal sealed class LauncherInstalled
    {
        [JsonPropertyName("InstallationList")]
        public LauncherInstallation[]? InstallationList { get; set; }
    }
}
