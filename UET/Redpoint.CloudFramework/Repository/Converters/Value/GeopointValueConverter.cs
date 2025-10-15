namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Google.Protobuf.WellKnownTypes;
    using Google.Type;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using Redpoint.CloudFramework.Repository.Geographic;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using static Google.Cloud.Datastore.V1.Value;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;

    internal class GeopointValueConverter : IValueConverter
    {
        public FieldType GetFieldType()
        {
            return FieldType.Geopoint;
        }

        public bool IsConverterForClrType(Type clrType)
        {
            return clrType == typeof(LatLng);
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
            return propertyNonNullDatastoreValue.GeoPointValue;
        }

        public Value ConvertToDatastoreValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object? propertyClrValue,
            bool propertyIndexed)
        {
            var geopoint = (LatLng?)propertyClrValue;
            Value result;
            if (geopoint == null)
            {
                result = new Value
                {
                    NullValue = NullValue.NullValue,
                    ExcludeFromIndexes = !propertyIndexed,
                };

                if (propertyIndexed)
                {
                    var geomodel = context.Model as IGeoModel;
                    if (geomodel != null)
                    {
                        var geopointFieldLengths = geomodel.GetHashKeyLengthsForGeopointFields();
                        if (geopointFieldLengths.ContainsKey(propertyName))
                        {
                            context.Entity[propertyName + GeoConstants.GeoHashPropertySuffix] = new Value
                            {
                                NullValue = NullValue.NullValue,
                                ExcludeFromIndexes = false,
                            };
                            context.Entity[propertyName + GeoConstants.HashKeyPropertySuffix] = new Value
                            {
                                NullValue = NullValue.NullValue,
                                ExcludeFromIndexes = false,
                            };
                        }
                    }
                }
            }
            else
            {
                result = new Value
                {
                    GeoPointValue = geopoint,
                    ExcludeFromIndexes = !propertyIndexed,
                };

                if (propertyIndexed)
                {
                    var geomodel = context.Model as IGeoModel;
                    if (geomodel != null)
                    {
                        var geopointFieldLengths = geomodel.GetHashKeyLengthsForGeopointFields();
                        if (geopointFieldLengths.TryGetValue(propertyName, out ushort geopointKeyLength))
                        {
                            var geohash = S2Manager.GenerateGeohash(geopoint);
                            var geohashkey = S2Manager.GenerateGeohashKey(geohash, geopointKeyLength);

                            context.Entity[propertyName + GeoConstants.GeoHashPropertySuffix] = new Value
                            {
                                StringValue = geohash.ToString(CultureInfo.InvariantCulture),
                                ExcludeFromIndexes = false,
                            };
                            context.Entity[propertyName + GeoConstants.HashKeyPropertySuffix] = new Value
                            {
                                IntegerValue = (long)geohashkey,
                                ExcludeFromIndexes = false,
                            };
                        }
                    }
                }
            }
            return result;
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

            return new LatLng
            {
                Latitude = propertyNonNullJsonToken.AsObject()["lat"]?.GetValue<double>() ?? 0,
                Longitude = propertyNonNullJsonToken.AsObject()["long"]?.GetValue<double>() ?? 0,
            };
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

            if (propertyNonNullClrValue is not LatLng)
            {
                throw new RuntimeValueWasIncorrectTypeException(propertyName, propertyNonNullClrValue, typeof(LatLng));
            }

            var geopoint = (LatLng)propertyNonNullClrValue;

            var obj = new JsonObject
            {
                { "lat", JsonValue.Create((double)geopoint.Latitude) },
                { "long", JsonValue.Create((double)geopoint.Longitude) }
            };
            return obj;
        }
    }
}
