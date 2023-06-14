namespace Docker.Registry.DotNet.Helpers
{
    using System;
    using System.Reflection;

    using Docker.Registry.DotNet.QueryParameters;

    using JetBrains.Annotations;

    internal static class QueryStringExtensions
    {
        /// <summary>
        /// Adds query parameters using reflection. Object must have [QueryParameter] attributes
        /// on it's properties for it to map properly.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="queryString"></param>
        /// <param name="instance"></param>
        internal static void AddFromObjectWithQueryParameters<T>(this QueryString queryString, [NotNull] T instance)
            where T: class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var propertyInfos = instance.GetType().GetProperties();

            foreach (var p in propertyInfos)
            {
                var attribute = p.GetCustomAttribute<QueryParameterAttribute>();
                if (attribute != null)
                {
                    // TODO: Use a nuget like FastMember to improve performance here or switch to static delegate generation
                    var value = p.GetValue(instance, null);
                    if (value != null)
                    {
                        queryString.Add(attribute.Key, value.ToString());
                    }
                }
            }
        }

        /// <summary>
        ///     Adds the value to the query string if it's not null.
        /// </summary>
        /// <param name="queryString"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        internal static void AddIfNotNull<T>(this QueryString queryString, string key, T? value)
            where T : struct
        {
            if (value != null) queryString.Add(key, $"{value.Value}");
        }

        /// <summary>
        /// </summary>
        /// <param name="queryString"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        internal static void AddIfNotEmpty(this QueryString queryString, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) queryString.Add(key, value);
        }
    }
}