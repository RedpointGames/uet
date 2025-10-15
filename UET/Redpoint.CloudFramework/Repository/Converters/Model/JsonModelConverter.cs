namespace Redpoint.CloudFramework.Repository.Converters.Model
{
    using Redpoint.CloudFramework.Models;
    using System;
    using System.Collections.Generic;
    using Google.Cloud.Datastore.V1;
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Prefix;
    using System.Reflection;
    using Redpoint.CloudFramework.Repository.Converters.Timestamp;
    using Redpoint.CloudFramework.Repository.Converters.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System.Text.Json.Nodes;
    using System.Text.Json;

    internal class JsonModelConverter : IModelConverter<string>
    {
        private readonly IGlobalPrefix _globalPrefix;
        private readonly ILogger<JsonModelConverter> _logger;
        private readonly IInstantTimestampJsonConverter _instantTimestampJsonConverter;
        private readonly IValueConverterProvider _valueConverterProvider;

        public JsonModelConverter(
            IGlobalPrefix globalPrefix,
            ILogger<JsonModelConverter> logger,
            IInstantTimestampJsonConverter instantTimestampJsonConverter,
            IValueConverterProvider valueConverterProvider)
        {
            _globalPrefix = globalPrefix;
            _logger = logger;
            _instantTimestampJsonConverter = instantTimestampJsonConverter;
            _valueConverterProvider = valueConverterProvider;
        }

        public T? From<T>(string @namespace, string jsonCache) where T : class, IModel, new()
        {
            var model = new T();
            model._originalData = new Dictionary<string, object?>();

            var hashsetValue = JsonObject.Parse(jsonCache);
            if (hashsetValue == null ||
                hashsetValue.GetValueKind() != JsonValueKind.Object ||
                (hashsetValue.AsObject()["_isnull"]?.GetValue<bool>() ?? false))
            {
                // The object does not exist (and we've cached the non-existence of it
                // during a previous load).
                return null;
            }

            var hashset = hashsetValue.AsObject();

            var delayedLoads = new List<Action<string>>();

            var conversionContext = new JsonValueConvertFromContext
            {
                ModelNamespace = @namespace,
            };

            var defaults = model.GetDefaultValues();
            var types = model.GetTypes();
            foreach (var kv in types)
            {
                var propInfo = model.GetPropertyInfo(kv.Key);
                if (propInfo == null)
                {
                    _logger.LogWarning($"Model {typeof(T).FullName} declares property {kv.Key} but is missing C# declaration");
                    continue;
                }

                var converter = _valueConverterProvider.GetConverter(kv.Value, propInfo.PropertyType);

                object? value;
                if (!hashset.ContainsKey(kv.Key) ||
                    hashset[kv.Key] == null)
                {
                    // Preserve null.
                    value = null;
                }
                else
                {
                    value = converter.ConvertFromJsonToken(
                        conversionContext,
                        kv.Key,
                        propInfo.PropertyType,
                        hashset[kv.Key]!,
                        (callback) =>
                        {
                            delayedLoads.Add((localNamespace) =>
                            {
                                var delayedValue = callback(localNamespace);
                                propInfo.SetValue(model, delayedValue);
                                model._originalData[kv.Key] = delayedValue;
                            });
                        });
                }

                if (value == null &&
                    defaults != null &&
                    defaults.TryGetValue(kv.Key, out object? defaultValue))
                {
                    value = converter.ConvertFromClrDefaultValue(
                        conversionContext,
                        kv.Key,
                        propInfo.PropertyType,
                        defaultValue);
                }

                propInfo.SetValue(model, value);

                model._originalData[kv.Key] = value;
            }

            var keyStr = hashset["_key"]?.GetValue<string>();
            if (keyStr == null)
            {
                throw new InvalidOperationException("JSON entity in cache has incorrect _key property!");
            }
            model.Key = _globalPrefix.ParseInternal(@namespace, keyStr);
            model.dateCreatedUtc = _instantTimestampJsonConverter.FromJsonCacheToNodaTimeInstant(hashset["_dateCreatedUtc"]);
            model.dateModifiedUtc = _instantTimestampJsonConverter.FromJsonCacheToNodaTimeInstant(hashset["_dateModifiedUtc"]);
            model.schemaVersion = hashset["_schemaVersion"]?.GetValue<int>();

            // If we have any delayed local key assignments, run them now (before migrations, in case
            // migrations want to handle local-key properties).
            if (delayedLoads.Count > 0)
            {
                var localNamespace = model.GetDatastoreNamespaceForLocalKeys();
                foreach (var delayedLoad in delayedLoads)
                {
                    delayedLoad(localNamespace);
                }
            }

            return model;
        }

        public string To<T>(string @namespace, T? model, bool isCreateContext, Func<T, Key>? incompleteKeyFactory) where T : class, IModel, new()
        {
            var hashset = new JsonObject();

            if (model == null)
            {
                hashset.Add("_isnull", true);
            }
            else
            {
                var conversionContext = new JsonValueConvertToContext
                {
                    ModelNamespace = @namespace,
                    Model = model,
                };

                var defaults = model.GetDefaultValues();
                var types = model.GetTypes();
                foreach (var kv in types)
                {
                    var propInfo = model.GetPropertyInfo(kv.Key);
                    if (propInfo == null)
                    {
                        _logger.LogWarning($"Model {typeof(T).FullName} declares property {kv.Key} but is missing C# declaration");
                        continue;
                    }

                    var value = propInfo.GetValue(model);

                    var converter = _valueConverterProvider.GetConverter(kv.Value, propInfo.PropertyType);

                    if (value == null &&
                        defaults != null &&
                        defaults.TryGetValue(kv.Key, out object? defaultValue))
                    {
                        value = converter.ConvertFromClrDefaultValue(
                            conversionContext,
                            kv.Key,
                            propInfo.PropertyType,
                            defaultValue);
                    }

                    if (value == null)
                    {
                        hashset.Add(kv.Key, null);
                    }
                    else
                    {
                        hashset.Add(
                            kv.Key,
                            converter.ConvertToJsonToken(
                                conversionContext,
                                kv.Key,
                                propInfo.PropertyType,
                                value));
                    }
                }

                hashset.Add("_key", _globalPrefix.CreateInternal(model.Key));
                hashset.Add("_dateCreatedUtc", _instantTimestampJsonConverter.FromNodaTimeInstantToJsonCache(model.dateCreatedUtc));
                hashset.Add("_dateModifiedUtc", _instantTimestampJsonConverter.FromNodaTimeInstantToJsonCache(model.dateModifiedUtc));
                hashset.Add("_schemaVersion", model.schemaVersion);
            }

            return hashset.ToString();
        }

    }
}
