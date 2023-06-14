namespace Docker.Registry.DotNet.Helpers
{
    using System.Collections.Generic;
    using System.Linq;

    public static class EnumerableExtensions
    {
        public static IEnumerable<T> IfNullEmpty<T>(this IEnumerable<T> enumerable)
        {
            return enumerable ?? Enumerable.Empty<T>();
        }
    }
}