namespace Redpoint.KubernetesManager.Configuration.Json
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    /// <summary>
    /// From Kubernetes C# client library. This converter is not exposed by the library, but we need it so we can store dates in a compatible format.
    /// </summary>
    public sealed class KubernetesDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        private const string _rfc3339MicroFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.ffffffZ";
        private const string _rfc3339NanoFormat = "yyyy-MM-dd'T'HH':'mm':'ss.fffffffZ";
        private const string _rfc3339Format = "yyyy'-'MM'-'dd'T'HH':'mm':'ssZ";

        private const string _rfc3339MicroWithOffsetFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.ffffffK";
        private const string _rfc3339NanoWithOffsetFormat = "yyyy-MM-dd'T'HH':'mm':'ss.fffffffK";
        private const string _rfc3339WithOffsetFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ssK";

        private static readonly string[] _standardFormats = { _rfc3339Format, _rfc3339MicroFormat, _rfc3339WithOffsetFormat, _rfc3339MicroWithOffsetFormat };
        private static readonly string[] _nanoFormats = { _rfc3339NanoFormat, _rfc3339NanoWithOffsetFormat };

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var str = reader.GetString() ?? string.Empty;

            // Try standard formats first
            if (DateTimeOffset.TryParseExact(str, _standardFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            {
                return result;
            }

            // Try RFC3339NanoLenient by trimming 1-9 digits to 7 digits
            var originalstr = str;
            str = Regex.Replace(str, @"\.\d+", m => (m.Value + "000000000").Substring(0, 7 + 1)); // 7 digits + 1 for the dot
            if (DateTimeOffset.TryParseExact(str, _nanoFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                return result;
            }

            throw new FormatException($"Unable to parse {originalstr} as RFC3339 RFC3339Micro or RFC3339Nano");
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            // Output as RFC3339Micro
            var date = value.ToUniversalTime();

            // Check if there are any fractional seconds
            var ticks = date.Ticks % TimeSpan.TicksPerSecond;
            if (ticks == 0)
            {
                // No fractional seconds - use format without fractional part
                var basePart = date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
                writer.WriteStringValue(basePart + "Z");
            }
            else
            {
                // Has fractional seconds - always use exactly 6 decimal places
                var formatted = date.ToString(_rfc3339MicroFormat, CultureInfo.InvariantCulture);
                writer.WriteStringValue(formatted);
            }
        }
    }
}
