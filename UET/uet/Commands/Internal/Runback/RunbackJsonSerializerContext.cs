namespace UET.Commands.Internal.Runback
{
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Plugin.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.DownloadPlugin;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Automation;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Commandlet;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Downstream;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Gauntlet;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Automation;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Commandlet;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Gauntlet;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(RunbackJson))]
    [JsonSerializable(typeof(BuildConfigPluginPrepareCustom))]
    [JsonSerializable(typeof(BuildConfigProjectPrepareCustom))]
    [JsonSerializable(typeof(BuildConfigProjectPrepareDownloadPlugin))]
    [JsonSerializable(typeof(BuildConfigPluginTestAutomation))]
    [JsonSerializable(typeof(BuildConfigPluginTestCommandlet))]
    [JsonSerializable(typeof(BuildConfigPluginTestCustom))]
    [JsonSerializable(typeof(BuildConfigPluginTestGauntlet))]
    [JsonSerializable(typeof(BuildConfigPluginTestDownstream))]
    [JsonSerializable(typeof(BuildConfigProjectTestAutomation))]
    [JsonSerializable(typeof(BuildConfigProjectTestCustom))]
    [JsonSerializable(typeof(BuildConfigProjectTestGauntlet))]
    [JsonSerializable(typeof(BuildConfigProjectTestCommandlet))]
    [JsonSerializable(typeof(BuildConfigPluginDeploymentBackblazeB2))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentCustom))]
    [JsonSerializable(typeof(BuildConfigProjectDeploymentSteam))]
    internal partial class RunbackJsonSerializerContext : JsonSerializerContext
    {
        public static RunbackJsonSerializerContext Create(IServiceProvider serviceProvider)
        {
            return new RunbackJsonSerializerContext(new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonStringEnumConverter(),
                    new BuildConfigPrepareConverter<BuildConfigPluginDistribution>(serviceProvider),
                    new BuildConfigPrepareConverter<BuildConfigProjectDistribution>(serviceProvider),
                }
            });
        }
    }
}
