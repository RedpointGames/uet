namespace Redpoint.Uet.Configuration.Dynamic
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Text.Json.Serialization;
    using System.Text.Json;

    public abstract class BuildConfigPredefinedDynamicConverter<TDistribution, TBaseClass, TDependencies> : JsonConverter<BuildConfigPredefinedDynamic<TDistribution, TBaseClass, TDependencies>>
    {
        private readonly IDynamicProvider<TDistribution, TBaseClass>[] _providers;

        protected abstract string Noun { get; }

        private string UpperNoun => Noun[..1].ToUpperInvariant() + Noun[1..];

        protected BuildConfigPredefinedDynamicConverter(IServiceProvider serviceProvider)
        {
            _providers = serviceProvider.GetServices<IDynamicProvider<TDistribution, TBaseClass>>().ToArray();
        }

        public override BuildConfigPredefinedDynamic<TDistribution, TBaseClass, TDependencies>? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            IDynamicProvider<TDistribution, TBaseClass>? provider;
            {
                var readerClone = reader;
                provider = BuildConfigDynamicConverterHelpers<TDistribution, TBaseClass>.ReadAndGetProvider(
                    _providers,
                    Noun,
                    UpperNoun,
                    ref readerClone,
                    options);
            }

            var result = new BuildConfigPredefinedDynamic<TDistribution, TBaseClass, TDependencies>();
            BuildConfigDynamicConverterHelpers<TDistribution, TBaseClass>.ReadInto(
                result,
                provider,
                Noun,
                ref reader,
                typeToConvert,
                options,
                (BuildConfigPredefinedDynamic<TDistribution, TBaseClass, TDependencies> result, ref Utf8JsonReader reader, string? propertyName) =>
                {
                    switch (propertyName)
                    {
                        case "ShortName":
                            var shortName = reader.GetString();
                            if (shortName == null)
                            {
                                throw new JsonException($"Expected {Noun} entry to have a non-null name.");
                            }
                            result.ShortName = shortName;
                            return true;
                        case "Dependencies":
                            result.Dependencies = (TDependencies?)JsonSerializer.Deserialize(ref reader, options.GetTypeInfo(typeof(TDependencies)));
                            return true;
                        default:
                            return false;
                    }
                });

            return result;
        }

        public override void Write(
            Utf8JsonWriter writer,
            BuildConfigPredefinedDynamic<TDistribution, TBaseClass, TDependencies> value,
            JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            BuildConfigDynamicConverterHelpers<TDistribution, TBaseClass>.WriteInto(
                _providers,
                value,
                writer,
                options,
                (BuildConfigPredefinedDynamic<TDistribution, TBaseClass, TDependencies> value, Utf8JsonWriter writer) =>
                {
                    if (!string.IsNullOrWhiteSpace(value.ShortName))
                    {
                        writer.WriteString("ShortName", value.ShortName);
                    }

                    if (value.Dependencies != null)
                    {
                        writer.WritePropertyName("Dependencies");
                        JsonSerializer.Serialize(writer, value.Dependencies, options.GetTypeInfo(typeof(TDependencies)));
                    }
                });
        }
    }
}
