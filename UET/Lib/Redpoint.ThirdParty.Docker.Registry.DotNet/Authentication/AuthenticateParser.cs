namespace Docker.Registry.DotNet.Authentication
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Docker.Registry.DotNet.Helpers;

    internal static class AuthenticateParser
    {
        public static IDictionary<string, string> Parse(string value)
        {
            //https://stackoverflow.com/questions/45516717/extracting-and-parsing-the-www-authenticate-header-from-httpresponsemessage-in/45516809#45516809            
            return SplitWWWAuthenticateHeader(value).ToDictionary(GetKey, GetValue);
        }

        private static IEnumerable<string> SplitWWWAuthenticateHeader(string value)
        {
            var builder = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < value.Length; i++)
            {
                var charI = value[i];
                switch (charI)
                {
                    case '\"':
                        if (inQuotes)
                        {
                            yield return builder.ToString();
                            builder.Clear();
                            inQuotes = false;
                        }
                        else
                        {
                            inQuotes = true;
                        }
                        break;

                    case ',':
                        if (inQuotes)
                        {
                            builder.Append(charI);
                        }
                        else
                        {
                            if (builder.Length > 0)
                            {
                                yield return builder.ToString();
                                builder.Clear();
                            }
                        }
                        break;

                    default:
                        builder.Append(charI);
                        break;
                }
            }
            if (builder.Length > 0) yield return builder.ToString();
        }

        public static ParsedAuthentication ParseTyped(string value)
        {
            var parsed = Parse(value);

            return new ParsedAuthentication(
                parsed.GetValueOrDefault("realm"),
                parsed.GetValueOrDefault("service"),
                parsed.GetValueOrDefault("scope"));
        }

        private static string GetKey(string pair)
        {
            int equalPos = pair.IndexOf("=", StringComparison.Ordinal);

            if (equalPos < 1)
                throw new FormatException("No '=' found.");

            return pair[..equalPos];
        }

        private static string GetValue(string pair)
        {
            int equalPos = pair.IndexOf("=", StringComparison.Ordinal);

            if (equalPos < 1)
                throw new FormatException("No '=' found.");

            string value = pair[(equalPos + 1)..].Trim();

            //Trim quotes
            if (value.StartsWith("\"") && value.EndsWith("\""))
            {
                value = value[1..^1];
            }

            return value;
        }
    }
}