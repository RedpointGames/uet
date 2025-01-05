namespace Redpoint.CloudFramework.Repository.Converters.Value
{
    using Redpoint.CloudFramework.Models;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    internal class DefaultValueConverterProvider : IValueConverterProvider
    {
        private readonly Dictionary<FieldType, List<IValueConverter>> _converters;
        private readonly ConcurrentDictionary<(FieldType, Type), IValueConverter> _converterLookupCache;

        public DefaultValueConverterProvider(
            IEnumerable<IValueConverter> valueConverters)
        {
            _converters = new Dictionary<FieldType, List<IValueConverter>>();
            foreach (var valueConverter in valueConverters)
            {
                var fieldType = valueConverter.GetFieldType();
                if (!_converters.TryGetValue(fieldType, out List<IValueConverter>? matchedConverters))
                {
                    matchedConverters = new List<IValueConverter>();
                    _converters.Add(fieldType, matchedConverters);
                }
                matchedConverters.Add(valueConverter);
            }
            _converterLookupCache = new ConcurrentDictionary<(FieldType, Type), IValueConverter>();
        }

        public IValueConverter GetConverter(FieldType fieldType, Type propertyClrType)
        {
            if (_converterLookupCache.TryGetValue((fieldType, propertyClrType), out var converter))
            {
                return converter;
            }
            if (_converters.TryGetValue(fieldType, out var matchedConverters))
            {
                foreach (var matchedConverter in matchedConverters)
                {
                    if (matchedConverter.IsConverterForClrType(propertyClrType))
                    {
                        _converterLookupCache.TryAdd((fieldType, propertyClrType), matchedConverter);
                        return matchedConverter;
                    }
                }
            }
            throw new NotSupportedException($"Model field type '{fieldType}' and property CLR type '{propertyClrType}' has no matching value converter!");
        }
    }
}
