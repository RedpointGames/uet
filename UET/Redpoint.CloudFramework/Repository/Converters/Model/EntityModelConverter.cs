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

        public T From<T>(string @namespace, Entity entity) where T : class, IModel, new()
        {
            var referenceModel = ReferenceModelCache.Get<T>();

            var model = referenceModel.ConstructNewModel();
            model._originalData = new Dictionary<string, object?>();

            var delayedLoads = new List<Action<string>>();

            var conversionContext = new DatastoreValueConvertFromContext
            {
                ModelNamespace = @namespace,
            };

            var defaults = referenceModel.DefaultValues;
            var types = referenceModel.Types;
            foreach (var kv in types)
            {
                var propInfo = referenceModel.GetPropertyInfo(kv.Key);
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

            model.dateCreatedUtc = _instantTimestampConversion.FromDatastoreValueToNodaTimeInstant(entity["dateCreatedUtc"]);
            model.dateModifiedUtc = _instantTimestampConversion.FromDatastoreValueToNodaTimeInstant(entity["dateModifiedUtc"]);
            referenceModel.SetDatastoreKey(model, entity.Key);
            if (entity["schemaVersion"]?.IsNull ?? true || entity["schemaVersion"].ValueTypeCase != Value.ValueTypeOneofCase.IntegerValue)
            {
                model.schemaVersion = null;
            }
            else
            {
                model.schemaVersion = entity["schemaVersion"].IntegerValue;
            }

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

        public Entity To<T>(string @namespace, T? model, bool isCreateContext, Func<T, Key<T>>? incompleteKeyFactory) where T : class, IModel, new()
        {
            ArgumentNullException.ThrowIfNull(model);

            var referenceModel = ReferenceModelCache.Get(model);

            var entity = new Entity();

            var conversionContext = new DatastoreValueConvertToContext
            {
                ModelNamespace = @namespace,
                Model = model,
                ReferenceModel = referenceModel,
                Entity = entity,
            };

            var defaults = referenceModel.DefaultValues;
            var types = referenceModel.Types;
            var indexes = referenceModel.Indexes;
            foreach (var kv in types)
            {
                var propInfo = referenceModel.GetPropertyInfo(kv.Key);
                if (propInfo == null)
                {
                    throw new InvalidOperationException($"The property '{kv.Key}' could not be found on '{model.GetType().FullName}'. Ensure the datastore type declarations are correct.");
                }
                var value = propInfo.GetValue(model);

                IValueConverter converter;
                try
                {
                    converter = _valueConverterProvider.GetConverter(kv.Value, propInfo.PropertyType);
                }
                catch (NotSupportedException)
                {
                    throw new NotSupportedException($"Model field type '{kv.Value}' and property CLR type '{propInfo.PropertyType}' has no matching value converter for field named '{propInfo.Name}' on model type '{referenceModel.CSharpTypeName}'!");
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

                entity[kv.Key] = converter.ConvertToDatastoreValue(
                    conversionContext,
                    kv.Key,
                    propInfo.PropertyType,
                    value,
                    indexes.Contains(kv.Key));
            }

            if (!referenceModel.HasKey(model))
            {
                // @note: This used to call CreateIncompleteKey for the caller, but since the database context
                // isn't available here, it's now a callback instead.
                ArgumentNullException.ThrowIfNull(incompleteKeyFactory);
                entity.Key = incompleteKeyFactory(model).__InternalDatastoreKey__;
            }
            else
            {
                entity.Key = referenceModel.GetDatastoreKey(model);
            }

            var now = SystemClock.Instance.GetCurrentInstant();
            if (isCreateContext || model.dateCreatedUtc == null)
            {
                model.dateCreatedUtc = now;
            }

            model.dateModifiedUtc = now;

            // @note: We changed this behaviour so that the schema version is only automatically set on creation. This ensures that when migrators update models, the models don't skip to the latest schema version - they must always get to the latest version via a migrator.
            if (isCreateContext || model.schemaVersion == null)
            {
                model.schemaVersion = referenceModel.SchemaVersion;
            }

            entity["dateCreatedUtc"] = _instantTimestampConversion.FromNodaTimeInstantToDatastoreValue(model.dateCreatedUtc, false);
            entity["dateModifiedUtc"] = _instantTimestampConversion.FromNodaTimeInstantToDatastoreValue(model.dateModifiedUtc, false);
            entity["schemaVersion"] = model.schemaVersion;

            // hasImplicitMigrationsApplied is only for runtime checks so application code can see
            // if an entity was implicitly modified by migrations.

            return entity;
        }
    }
}
