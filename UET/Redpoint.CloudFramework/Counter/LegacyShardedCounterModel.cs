using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Counter
{
    using System.Collections.Generic;
    using System.Globalization;

    [Obsolete]
    internal class LegacyShardedCounterModel : Model, IShardedCounterModel
    {
        public long? count { get; set; }

        public override string GetKind()
        {
            return "RCF-SharedCounter";
        }

        public override long GetSchemaVersion()
        {
            return 1;
        }

        public override Dictionary<string, FieldType> GetTypes()
        {
            return new Dictionary<string, FieldType> {
                { "count", FieldType.Integer }
            };
        }

        public override HashSet<string> GetIndexes()
        {
            return new HashSet<string>
            {
                "count",
            };
        }

        public string? GetTypeFieldName()
        {
            return null;
        }

        public string GetCountFieldName()
        {
            return "count";
        }

        public string FormatShardName(string name, int index)
        {
            return string.Format(CultureInfo.InvariantCulture, "shard-{0}-{1}", name, index);
        }
    }
}