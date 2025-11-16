namespace Redpoint.Uet.Database.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;

    internal static class UetModelInfoRegistry
    {
        private static Dictionary<Type, UetModelInfo> _cachedInfo = [];

        public static UetModelInfo InitModel<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(UetModel<T> model) where T : UetModel<T>
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));
            if (typeof(T) != model.GetType() &&
                // Technically having a derived type where the derived type has no
                // additional properties is safe, but we only permit it in the test project.
                !(typeof(T).Namespace ?? string.Empty).StartsWith("Redpoint.CloudFramework.Tests", StringComparison.Ordinal))
            {
                throw new ArgumentException("The model value must have the exact same type as T.", nameof(model));
            }

            var modelInfo = GetModelInfo<T>();

            return modelInfo;
        }

        public static UetModelInfo GetModelInfo<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>()
        {
            if (!_cachedInfo.ContainsKey(typeof(T)))
            {
                lock (_cachedInfo)
                {
                    var kindAttribute = typeof(T).GetCustomAttributes(typeof(UetKindAttribute), false).Cast<UetKindAttribute>().FirstOrDefault()
                        ?? throw new InvalidOperationException($"Missing [Kind(\"...\")] attribute on {typeof(T).FullName} class.");
                    string kind = kindAttribute?.Kind!;
                    if (string.IsNullOrWhiteSpace(kind))
                    {
                        throw new InvalidOperationException($"Attribute [Kind(\"...\")] on {typeof(T).FullName} has an invalid value.");
                    }

                    var types = new Dictionary<string, Type>();
                    foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        var isField = property.GetCustomAttributes(typeof(UetFieldAttribute), false).Length > 0;
                        if (!isField)
                        {
                            continue;
                        }

                        if (property.PropertyType == typeof(string) ||
                            property.PropertyType == typeof(long) ||
                            property.PropertyType == typeof(double))
                        {
                            types.Add(property.Name, property.PropertyType);
                        }
                    }

                    _cachedInfo[typeof(T)] = new UetModelInfo()
                    {
                        _kind = kind,
                        _types = types,
                        _propertyInfos = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                        _propertyInfoByName = typeof(T)
                            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .ToDictionary(k => k.Name, v => v),
                    };
                }
            }

            return _cachedInfo[typeof(T)];
        }
    }
}
