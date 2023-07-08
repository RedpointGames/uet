namespace Redpoint.Uet.BuildPipeline.Providers.Prepare
{
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Plugin.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.DownloadPlugin;
    using System.Text.Json.Serialization;

    [RuntimeJsonProvider(typeof(PrepareProviderSourceGenerationContext))]
    [JsonSerializable(typeof(BuildConfigPluginPrepareCustom))]
    [JsonSerializable(typeof(BuildConfigProjectPrepareCustom))]
    [JsonSerializable(typeof(BuildConfigProjectPrepareDownloadPlugin))]
    internal partial class PrepareProviderRuntimeJson
    {
    }
}