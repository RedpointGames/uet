namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Newtonsoft.Json.Linq;
    using Type = System.Type;
    using Google.Protobuf.WellKnownTypes;
    using Value = Google.Cloud.Datastore.V1.Value;
    using Google.Type;
    using Redpoint.CloudFramework.Repository.Geographic;
    using System.Globalization;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;

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
            JToken propertyNonNullJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            if (propertyNonNullJsonToken == null || propertyNonNullJsonToken?.Type == JTokenType.Null)
            {
                return null;
            }

            return new LatLng
            {
                Latitude = propertyNonNullJsonToken!["lat"]?.Value<double>() ?? 0,
                Longitude = propertyNonNullJsonToken!["long"]?.Value<double>() ?? 0,
            };
        }

        public JToken ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            var geopoint = (LatLng)propertyNonNullClrValue;

            var obj = new JObject();
            obj["lat"] = (double)geopoint.Latitude;
            obj["long"] = (double)geopoint.Longitude;
            return obj;
        }
    }
}
