namespace UET.Commands.Internal.Runback
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(RunbackJson))]
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
