﻿namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using Newtonsoft.Json.Linq;
    using System;
    using Type = System.Type;
    using Value = Google.Cloud.Datastore.V1.Value;
    using NodaTime;
    using Redpoint.CloudFramework.Repository.Converters.Timestamp;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;

    internal class TimestampValueConverter : IValueConverter
    {
        private readonly IInstantTimestampConverter _instantTimestampConverter;
        private readonly IInstantTimestampJsonConverter _instantTimestampJsonConverter;

        public TimestampValueConverter(
            IInstantTimestampConverter instantTimestampConverter,
            IInstantTimestampJsonConverter instantTimestampJsonConverter)
        {
            _instantTimestampConverter = instantTimestampConverter;
            _instantTimestampJsonConverter = instantTimestampJsonConverter;
        }

        public FieldType GetFieldType()
        {
            return FieldType.Timestamp;
        }

        public bool IsConverterForClrType(Type clrType)
        {
            return clrType == typeof(Instant?);
        }

        public object? ConvertFromClrDefaultValue(
            ClrValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            object propertyClrDefaultValue)
        {
            throw new InvalidOperationException("FieldType.Timestamp does not support default values. These property must be nullable and omit [Default].");
        }

        public object? ConvertFromDatastoreValue(
            DatastoreValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            Value propertyNonNullDatastoreValue,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            return _instantTimestampConverter.FromDatastoreValueToNodaTimeInstant(propertyNonNullDatastoreValue);
        }

        public Value ConvertToDatastoreValue(
            DatastoreValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object? propertyClrValue,
            bool propertyIndexed)
        {
            var instantNullable = (Instant?)propertyClrValue;
            return _instantTimestampConverter.FromNodaTimeInstantToDatastoreValue(
                instantNullable,
                !propertyIndexed);
        }

        public object? ConvertFromJsonToken(
            JsonValueConvertFromContext context,
            string propertyName,
            Type propertyClrType,
            JToken propertyJsonToken,
            AddConvertFromDelayedLoad addConvertFromDelayedLoad)
        {
            return _instantTimestampJsonConverter.FromJsonCacheToNodaTimeInstant(propertyJsonToken);
        }

        public JToken ConvertToJsonToken(
            JsonValueConvertToContext context,
            string propertyName,
            Type propertyClrType,
            object propertyNonNullClrValue)
        {
            return _instantTimestampJsonConverter.FromNodaTimeInstantToJsonCache((Instant?)propertyNonNullClrValue);
        }
    }
}
