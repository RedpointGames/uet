namespace Redpoint.Uet.Configuration.Dynamic
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Text.Json.Serialization;
    using System.Text.Json;

    public abstract class BuildConfigDynamicConverter<TDistribution, TBaseClass> : JsonConverter<BuildConfigDynamic<TDistribution, TBaseClass>>
    {
        private readonly IDynamicProvider<TDistribution, TBaseClass>[] _providers;

        protected abstract string Noun { get; }

        private string UpperNoun => Noun[..1].ToUpperInvariant() + Noun[1..];

        protected BuildConfigDynamicConverter(IServiceProvider serviceProvider)
        {
            _providers = serviceProvider.GetServices<IDynamicProvider<TDistribution, TBaseClass>>().ToArray();
        }

        public override BuildConfigDynamic<TDistribution, TBaseClass>? Read(
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

            var result = new BuildConfigDynamic<TDistribution, TBaseClass>();
            BuildConfigDynamicConverterHelpers<TDistribution, TBaseClass>.ReadInto(
                result,
                provider,
                Noun,
                ref reader,
                typeToConvert,
                options,
                (BuildConfigDynamic<TDistribution, TBaseClass> _, ref Utf8JsonReader _, string? _) =>
                {
                    return false;
                });

            return result;
        }

        public override void Write(
            Utf8JsonWriter writer,
            BuildConfigDynamic<TDistribution, TBaseClass> value,
            JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            BuildConfigDynamicConverterHelpers<TDistribution, TBaseClass>.WriteInto(
                _providers,
                value,
                writer,
                options,
                (BuildConfigDynamic<TDistribution, TBaseClass> value, Utf8JsonWriter writer) =>
                {
                });
        }
    }
}
