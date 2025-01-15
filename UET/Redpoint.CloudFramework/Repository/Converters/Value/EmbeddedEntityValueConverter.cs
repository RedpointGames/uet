namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Newtonsoft.Json.Linq;
    using System;
    using Type = System.Type;
    using Google.Protobuf.WellKnownTypes;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Google.Cloud.Datastore.V1;
    using Google.Protobuf;
    using Google.Type;
    using Redpoint.CloudFramework.Prefix;
    using Redpoint.CloudFramework.Repository.Converters.Timestamp;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;

    internal class EmbeddedEntityValueConverter : IValueConverter
    {
        private readonly IGlobalPrefix _globalPrefix;
        private readonly IInstantTimestampConverter _instantTimestampConverter;
        private readonly IInstantTimestampJsonConverter _instantTimestampJsonConverter;

        public EmbeddedEntityValueConverter(
            IGlobalPrefix globalPrefix,
            IInstantTimestampConverter instantTimestampConverter,
            IInstantTimestampJsonConverter instantTimestampJsonConverter)
        {
            _globalPrefix = globalPrefix;
            _instantTimestampConverter = instantTimestampConverter;
            _instantTimestampJsonConverter = instantTimestampJsonConverter;
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
            JToken propertyNonNullJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            return FromJsonCacheToEmbeddedEntity(propertyNonNullJsonToken);
        }

        public JToken ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            return FromEmbeddedEntityToJsonCache((Entity)propertyNonNullClrValue);
        }

        private Value FromJsonCacheToEmbeddedEntityValue(JToken input)
        {
            if (input == null)
            {
                return Value.ForNull();
            }

            switch (input.Type)
            {
                case JTokenType.None:
                case JTokenType.Null:
                    return Value.ForNull();
                case JTokenType.Boolean:
                    return new Value(input.Value<bool>());
                case JTokenType.String:
                    return new Value(input.Value<string>());
                case JTokenType.Integer:
                    return new Value(input.Value<long>());
                case JTokenType.Float:
                    return new Value(input.Value<double>());
                case JTokenType.Object:
                    JObject? obj = input.Value<JObject>();
                    if (obj == null)
                    {
                        return Value.ForNull();
                    }
                    string? objType = (string?)obj["type"];
                    JToken? objValue = obj["value"];
                    if (objType == null)
                    {
                        return Value.ForNull();
                    }
                    switch (objType)
                    {
                        case "blob":
                            if (objValue == null)
                            {
                                return Value.ForNull();
                            }
                            return new Value(ByteString.FromBase64(objValue.Value<string>()));
                        case "entity":
                            return new Value(FromJsonCacheToEmbeddedEntity(objValue));
                        case "geopoint":
                            JToken? objLatitude = obj["latitude"];
                            JToken? objLongitude = obj["longitude"];
                            if (objLatitude == null || objLongitude == null)
                            {
                                return Value.ForNull();
                            }
                            return new Value(new LatLng
                            {
                                Latitude = objLatitude.Value<double>(),
                                Longitude = objLongitude.Value<double>(),
                            });
                        case "key":
                            JToken? objNs = obj["ns"];
                            if (objNs == null || objValue == null)
                            {
                                return Value.ForNull();
                            }
                            string? strNs = objNs.Value<string>();
                            string? strValue = objValue.Value<string>();
                            if (strNs == null || strValue == null)
                            {
                                return Value.ForNull();
                            }
                            return new Value(_globalPrefix.ParseInternal(strNs, strValue));
                        case "timestamp":
                            return new Value(_instantTimestampConverter.FromNodaTimeInstantToDatastoreValue(_instantTimestampJsonConverter.FromJsonCacheToNodaTimeInstant(objValue), false));
                        default:
                            throw new InvalidOperationException("Unsupported serialized entity type.");
                    }
                case JTokenType.Array:
                    var arr = new ArrayValue();
                    var jArray = input.Value<JArray>();
                    if (jArray != null)
                    {
                        foreach (var elem in jArray)
                        {
                            arr.Values.Add(FromJsonCacheToEmbeddedEntityValue(elem));
                        }
                    }
                    return new Value(arr);
                default:
                    throw new InvalidOperationException("Unsupported serialized entity type.");
            }
        }

        private Entity? FromJsonCacheToEmbeddedEntity(JToken? obj)
        {
            if (obj == null || obj?.Type == JTokenType.Null || obj?.Type != JTokenType.Object)
            {
                return null;
            }

            var entity = new Entity();
            foreach (var kv in obj.Cast<JProperty>())
            {
                entity.Properties.Add(kv.Name, FromJsonCacheToEmbeddedEntityValue(kv.Value));
            }
            return entity;
        }

        private JToken? FromEmbeddedEntityValueToJsonCache(Value value)
        {
            switch (value.ValueTypeCase)
            {
                case Value.ValueTypeOneofCase.None:
                    return null;
                case Value.ValueTypeOneofCase.NullValue:
                    return JValue.CreateNull();
                case Value.ValueTypeOneofCase.BooleanValue:
                    return new JValue(value.BooleanValue);
                case Value.ValueTypeOneofCase.StringValue:
                    return new JValue(value.StringValue);
                case Value.ValueTypeOneofCase.IntegerValue:
                    return new JValue(value.IntegerValue);
                case Value.ValueTypeOneofCase.DoubleValue:
                    return new JValue(value.DoubleValue);
                case Value.ValueTypeOneofCase.ArrayValue:
                    var array = new JArray();
                    foreach (var element in value.ArrayValue.Values)
                    {
                        array.Add(FromEmbeddedEntityValueToJsonCache(element)!);
                    }
                    return array;
                case Value.ValueTypeOneofCase.BlobValue:
                    var blob = new JObject();
                    blob["type"] = "blob";
                    blob["value"] = value.BlobValue.ToBase64();
                    return blob;
                case Value.ValueTypeOneofCase.EntityValue:
                    var nestedEntity = new JObject();
                    nestedEntity["type"] = "entity";
                    nestedEntity["value"] = FromEmbeddedEntityToJsonCache(value.EntityValue);
                    return nestedEntity;
                case Value.ValueTypeOneofCase.GeoPointValue:
                    var geo = new JObject();
                    geo["type"] = "geopoint";
                    geo["latitude"] = value.GeoPointValue.Latitude;
                    geo["longitude"] = value.GeoPointValue.Longitude;
                    return geo;
                case Value.ValueTypeOneofCase.KeyValue:
                    var key = new JObject();
                    key["type"] = "key";
                    key["ns"] = value.KeyValue.PartitionId.NamespaceId;
                    key["value"] = _globalPrefix.CreateInternal(value.KeyValue);
                    return key;
                case Value.ValueTypeOneofCase.TimestampValue:
                    var ts = new JObject();
                    ts["type"] = "timestamp";
                    ts["value"] = _instantTimestampJsonConverter.FromNodaTimeInstantToJsonCache(_instantTimestampConverter.FromDatastoreValueToNodaTimeInstant(value.TimestampValue));
                    return ts;
                default:
                    throw new InvalidOperationException("Unsupported property type on embedded entity value.");
            }
        }

        private JToken FromEmbeddedEntityToJsonCache(Entity entity)
        {
            if (entity == null)
            {
                return JValue.CreateNull();
            }

            var obj = new JObject();
            foreach (var prop in entity.Properties)
            {
                obj[prop.Key] = FromEmbeddedEntityValueToJsonCache(prop.Value);
            }
            return obj;
        }
    }
}
