namespace Redpoint.CloudFramework.Repository.Converters.Model
{
    using Google.Cloud.Datastore.V1;
    using Microsoft.Extensions.Logging;
    using NodaTime;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Timestamp;
    using Redpoint.CloudFramework.Repository.Converters.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Value = Google.Cloud.Datastore.V1.Value;

    internal class EntityModelConverter : IModelConverter<Entity>
    {
        private readonly ILogger<EntityModelConverter> _logger;
        private readonly IInstantTimestampConverter _instantTimestampConversion;
        private readonly IValueConverterProvider _valueConverterProvider;

        public EntityModelConverter(
            ILogger<EntityModelConverter> logger,
            IInstantTimestampConverter instantTimestampConversion,
            IValueConverterProvider valueConverterProvider)
        {
            _logger = logger;
            _instantTimestampConversion = instantTimestampConversion;
            _valueConverterProvider = valueConverterProvider;
        }

        public T From<T>(string @namespace, Entity entity) where T : Model, new()
        {
            var @ref = new T();
            @ref._originalData = new Dictionary<string, object?>();

            var delayedLoads = new List<Action<string>>();

            var conversionContext = new DatastoreValueConvertFromContext
            {
                ModelNamespace = @namespace,
            };

            var defaults = @ref.GetDefaultValues();
            var types = @ref.GetTypes();
            foreach (var kv in types)
            {
                var typeInfo = @ref.GetType();
                var propInfo = typeInfo.GetProperty(kv.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (propInfo == null)
                {
                    _logger.LogWarning($"Model {typeof(T).FullName} declares property {kv.Key} but is missing C# declaration");
                    continue;
                }

                var converter = _valueConverterProvider.GetConverter(kv.Value, propInfo.PropertyType);

                object? value;
                if (entity[kv.Key]?.IsNull ?? true)
                {
                    // Preserve null.
                    value = null;
                }
                else
                {
                    value = converter.ConvertFromDatastoreValue(
                        conversionContext,
                        kv.Key,
                        propInfo.PropertyType,
                        entity[kv.Key],
                        (callback) =>
                        {
                            delayedLoads.Add((localNamespace) =>
                            {
                                var delayedValue = callback(localNamespace);
                                propInfo.SetValue(@ref, delayedValue);
                                @ref._originalData[kv.Key] = delayedValue;
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

                propInfo.SetValue(@ref, value);

                @ref._originalData[kv.Key] = value;
            }

            @ref.dateCreatedUtc = _instantTimestampConversion.FromDatastoreValueToNodaTimeInstant(entity["dateCreatedUtc"]);
            @ref.dateModifiedUtc = _instantTimestampConversion.FromDatastoreValueToNodaTimeInstant(entity["dateModifiedUtc"]);
            @ref.Key = entity.Key;
            if (entity["schemaVersion"]?.IsNull ?? true || entity["schemaVersion"].ValueTypeCase != Value.ValueTypeOneofCase.IntegerValue)
            {
                @ref.schemaVersion = null;
            }
            else
            {
                @ref.schemaVersion = entity["schemaVersion"].IntegerValue;
            }

            // If we have any delayed local key assignments, run them now (before migrations, in case
            // migrations want to handle local-key properties).
            if (delayedLoads.Count > 0)
            {
                var localNamespace = @ref.GetDatastoreNamespaceForLocalKeys();
                foreach (var delayedLoad in delayedLoads)
                {
                    delayedLoad(localNamespace);
                }
            }

            return @ref;
        }

        public Entity To<T>(string @namespace, T? model, bool isCreateContext, Func<T, Key>? incompleteKeyFactory) where T : Model, new()
        {
            var entity = new Entity();

            ArgumentNullException.ThrowIfNull(model);

            var conversionContext = new DatastoreValueConvertToContext
            {
                ModelNamespace = @namespace,
                Model = model,
                Entity = entity,
            };

            var defaults = model.GetDefaultValues();
            var types = model.GetTypes();
            var indexes = model.GetIndexes();
            foreach (var kv in types)
            {
                var typeInfo = model.GetType();
                var propInfo = typeInfo.GetProperty(kv.Key, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (propInfo == null)
                {
                    throw new InvalidOperationException($"The property '{kv.Key}' could not be found on '{typeInfo.FullName}'. Ensure the datastore type declarations are correct.");
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

                entity[kv.Key] = converter.ConvertToDatastoreValue(
                    conversionContext,
                    kv.Key,
                    propInfo.PropertyType,
                    value,
                    indexes.Contains(kv.Key));
            }

            if (model.Key == null)
            {
                // @note: This used to call CreateIncompleteKey for the caller, but since the database context
                // isn't available here, it's now a callback instead.
                ArgumentNullException.ThrowIfNull(incompleteKeyFactory);
                entity.Key = incompleteKeyFactory(model);
            }
            else
            {
                entity.Key = model.Key;
            }

            var now = SystemClock.Instance.GetCurrentInstant();
            if (isCreateContext || model.dateCreatedUtc == null)
            {
                model.dateCreatedUtc = now;
            }

            model.dateModifiedUtc = now;
            model.schemaVersion = model.GetSchemaVersion();

            entity["dateCreatedUtc"] = _instantTimestampConversion.FromNodaTimeInstantToDatastoreValue(model.dateCreatedUtc, false);
            entity["dateModifiedUtc"] = _instantTimestampConversion.FromNodaTimeInstantToDatastoreValue(model.dateModifiedUtc, false);
            entity["schemaVersion"] = model.schemaVersion;

            // hasImplicitMigrationsApplied is only for runtime checks so application code can see
            // if an entity was implicitly modified by migrations.

            return entity;
        }
    }
}
