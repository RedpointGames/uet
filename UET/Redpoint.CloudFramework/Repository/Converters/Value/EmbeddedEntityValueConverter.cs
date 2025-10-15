namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Google.Cloud.Datastore.V1;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Google.Type;
    using Microsoft.Extensions.DependencyInjection;
    using Newtonsoft.Json.Linq;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Repository.Converters.Timestamp;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using System;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using static Google.Cloud.Datastore.V1.Value;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;

    internal class EmbeddedEntityValueConverter : IValueConverter
    {
        private readonly IGlobalPrefix _globalPrefix;
        private readonly IInstantTimestampConverter _instantTimestampConverter;
        private readonly IInstantTimestampJsonConverter _instantTimestampJsonConverter;
        private readonly IServiceProvider _serviceProvider;

        public EmbeddedEntityValueConverter(
            IGlobalPrefix globalPrefix,
            IInstantTimestampConverter instantTimestampConverter,
            IInstantTimestampJsonConverter instantTimestampJsonConverter,
            IServiceProvider serviceProvider)
        {
            _globalPrefix = globalPrefix;
            _instantTimestampConverter = instantTimestampConverter;
            _instantTimestampJsonConverter = instantTimestampJsonConverter;
            _serviceProvider = serviceProvider;
        }

        public FieldType GetFieldType()
        {
            return FieldType.EmbeddedEntity;
        }

        public bool IsConverterForClrType(Type clrType)
        {
            return clrType == typeof(Entity);
        }

        public object? ConvertFromClrDefaultValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            object propertyClrDefaultValue)
        {
            return propertyClrDefaultValue;
        }

        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            return propertyNonNullDatastoreValue.EntityValue;
        }

        public Value ConvertToDatastoreValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object? propertyClrValue,
            bool propertyIndexed)
        {
            if (propertyClrValue == null)
            {
                return new Value
                {
                    NullValue = NullValue.NullValue,
                    ExcludeFromIndexes = !propertyIndexed,
                };
            }
            else
            {
                return new Value
                {
                    EntityValue = (Entity)propertyClrValue,
                    ExcludeFromIndexes = !propertyIndexed,
                };
            }
        }

        public object? ConvertFromJsonToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            JsonNode propertyNonNullJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            if (propertyNonNullJsonToken == null || propertyNonNullJsonToken.GetValueKind() == JsonValueKind.Null)
            {
                throw new JsonValueWasNullException(propertyName);
            }

            if (propertyNonNullJsonToken.GetValueKind() != JsonValueKind.Object)
            {
                throw new JsonValueWasIncorrectKindException(propertyName, propertyNonNullJsonToken.GetValueKind(), JsonValueKind.Object);
            }

            return FromJsonCacheToEmbeddedEntity(propertyNonNullJsonToken);
        }

        public JsonNode ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            if (propertyNonNullClrValue == null)
            {
                throw new RuntimeValueWasNullException(propertyName);
            }

            if (propertyNonNullClrValue is not Entity)
            {
                throw new RuntimeValueWasIncorrectTypeException(propertyName, propertyNonNullClrValue, typeof(Entity));
            }

            return FromEmbeddedEntityToJsonCache((Entity)propertyNonNullClrValue)!;
        }

        private Value FromJsonCacheToEmbeddedEntityValue(JsonNode? input)
        {
            if (input == null ||
                input.GetValueKind() == JsonValueKind.Null)
            {
                return Value.ForNull();
            }

            if (input.GetValueKind() == JsonValueKind.Array)
            {
                var arrayValue = new ArrayValue();
                foreach (var element in input.AsArray())
                {
                    arrayValue.Values.Add(FromJsonCacheToEmbeddedEntityValue(element));
                }
                return new Value(arrayValue);
            }
            else if (input.GetValueKind() == JsonValueKind.Object)
            {
                if (!input.AsObject().TryGetPropertyValue("type", out var typeNode) ||
                    typeNode == null ||
                    typeNode.GetValueKind() != JsonValueKind.String ||
                    !input.AsObject().TryGetPropertyValue("value", out var valueNode) ||
                    valueNode == null)
                {
                    return Value.ForNull();
                }

                switch (typeNode.GetValue<string>())
                {
                    case "boolean":
                        return new Value(valueNode.GetValue<bool>());
                    case "string":
                        return new Value(valueNode.GetValue<string>());
                    case "integer":
                        return new Value(valueNode.GetValue<long>());
                    case "double":
                        return new Value(valueNode.GetValue<double>());
                    case "blob":
                        return new Value(Convert.FromBase64String(valueNode.GetValue<string>()));
                    case "entity":
                        return new Value(FromJsonCacheToEmbeddedEntity(valueNode));
                    case "geopoint":
                        return new Value(new LatLng
                        {
                            Latitude = valueNode.AsObject()["latitude"]?.GetValue<double>() ?? 0,
                            Longitude = valueNode.AsObject()["longitude"]?.GetValue<double>() ?? 0
                        });
                    case "key":
                        return new Value(_globalPrefix.ParseInternal(
                            valueNode.AsObject()["ns"]?.GetValue<string>() ?? string.Empty,
                            valueNode.AsObject()["value"]?.GetValue<string>() ?? string.Empty));
                    case "timestamp":
                        return new Value(_instantTimestampConverter.FromNodaTimeInstantToDatastoreValue(_instantTimestampJsonConverter.FromJsonCacheToNodaTimeInstant(valueNode), false));
                    default:
                        return Value.ForNull();
                }
            }
            else
            {
                return Value.ForNull();
            }
        }

        private Entity? FromJsonCacheToEmbeddedEntity(JsonNode? obj)
        {
            if (obj == null || obj.GetValueKind() != JsonValueKind.Object)
            {
                return null;
            }

            var entity = new Entity();
            foreach (var kv in obj.AsObject())
            {
                entity.Properties.Add(kv.Key, FromJsonCacheToEmbeddedEntityValue(kv.Value));
            }
            return entity;
        }

        private JsonNode? FromEmbeddedEntityValueToJsonCache(Value value)
        {
            switch (value.ValueTypeCase)
            {
                case Value.ValueTypeOneofCase.None:
                case Value.ValueTypeOneofCase.NullValue:
                    return null;
                case Value.ValueTypeOneofCase.BooleanValue:
                    return new JsonObject
                    {
                        { "type", "boolean" },
                        { "value", JsonValue.Create(value.BooleanValue) }
                    };
                case Value.ValueTypeOneofCase.StringValue:
                    return new JsonObject
                    {
                        { "type", "string" },
                        { "value", JsonValue.Create(value.StringValue) }
                    };
                case Value.ValueTypeOneofCase.IntegerValue:
                    return new JsonObject
                    {
                        { "type", "integer" },
                        { "value", JsonValue.Create(value.IntegerValue) }
                    };
                case Value.ValueTypeOneofCase.DoubleValue:
                    return new JsonObject
                    {
                        { "type", "double" },
                        { "value", JsonValue.Create(value.DoubleValue) }
                    };
                case Value.ValueTypeOneofCase.ArrayValue:
                    var array = new JsonArray();
                    foreach (var element in value.ArrayValue.Values)
                    {
                        array.Add(FromEmbeddedEntityValueToJsonCache(element)!);
                    }
                    return array;
                case Value.ValueTypeOneofCase.BlobValue:
                    return new JsonObject
                    {
                        { "type", "blob" },
                        { "value", JsonValue.Create(value.BlobValue.ToBase64()) }
                    };
                case Value.ValueTypeOneofCase.EntityValue:
                    return new JsonObject
                    {
                        { "type", "entity" },
                        { "value", FromEmbeddedEntityToJsonCache(value.EntityValue) }
                    };
                case Value.ValueTypeOneofCase.GeoPointValue:
                    return new JsonObject
                    {
                        { "type", "geopoint" },
                        { 
                            "value", 
                            new JsonObject
                            {
                                { "latitude", value.GeoPointValue.Latitude },
                                { "longitude", value.GeoPointValue.Longitude }
                            }
                        }
                    };
                case Value.ValueTypeOneofCase.KeyValue:
                    return new JsonObject
                    {
                        { "type", "key" },
                        {
                            "value",
                            new JsonObject
                            {
                                { "ns", value.KeyValue.PartitionId.NamespaceId },
                                { "value", _globalPrefix.CreateInternal(value.KeyValue) }
                            }
                        }
                    };
                case Value.ValueTypeOneofCase.TimestampValue:
                    return new JsonObject
                    {
                        { "type", "timestamp" },
                        { "value", _instantTimestampJsonConverter.FromNodaTimeInstantToJsonCache(_instantTimestampConverter.FromDatastoreValueToNodaTimeInstant(value.TimestampValue)) }
                    };
                default:
                    throw new InvalidOperationException("Unsupported property type on embedded entity value.");
            }
        }

        private JsonNode? FromEmbeddedEntityToJsonCache(Entity entity)
        {
            if (entity == null)
            {
                return null;
            }

            var obj = new JsonObject();
            foreach (var prop in entity.Properties)
            {
                obj[prop.Key] = FromEmbeddedEntityValueToJsonCache(prop.Value);
            }
            return obj;
        }
    }
}
