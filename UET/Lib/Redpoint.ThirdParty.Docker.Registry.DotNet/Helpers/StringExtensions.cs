namespace Docker.Registry.DotNet.Helpers
{
    using System.Collections.Generic;
    using System.Linq;

    public static class StringExtensions
    {
        public static string ToDelimitedString(
            this IEnumerable<string> strings,
            string delimiter = "")
        {
            return string.Join(delimiter, strings.IfNullEmpty().ToArray());
        }
    }
}