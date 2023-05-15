namespace BuildRunner.Services
{
    using BuildRunner.Configuration;
    using BuildRunner.Configuration.Engine;
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal class DefaultBuildConfigProvider : IBuildConfigProvider
    {
        private readonly IPathProvider _pathProvider;
        private readonly Lazy<BuildConfig> _buildConfig;

        public DefaultBuildConfigProvider(
            IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
            _buildConfig = new Lazy<BuildConfig>(ResolveBuildConfig);
        }

        public BuildConfig GetBuildConfig()
        {
            return _buildConfig.Value;
        }

        private BuildConfig ResolveBuildConfig()
        {
            var serializerOptions = new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            };

            var buildConfigPath = Path.Combine(_pathProvider.RepositoryRoot, "BuildConfig.json");
            var buildConfigContent = File.ReadAllText(buildConfigPath);
            var baseBuildConfig = JsonSerializer.Deserialize<BuildConfig>(buildConfigContent, serializerOptions);

            switch (baseBuildConfig?.Type)
            {
                case BuildConfigType.Engine:
                    return JsonSerializer.Deserialize<BuildConfigEngine>(buildConfigContent, serializerOptions)!;
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
