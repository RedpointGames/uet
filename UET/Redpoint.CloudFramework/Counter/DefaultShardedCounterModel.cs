namespace Redpoint.CloudFramework.Counter
{
    using Redpoint.CloudFramework.Models;
    using System;

    [Kind<DefaultShardedCounterModel>("_rcfShardedCounter")]
    internal class DefaultShardedCounterModel : AttributedModel
    {
        /// <summary>
        /// The counter name shared amongst all shards of this counter.
        /// </summary>
        [Type(FieldType.String), Indexed, Default("")]
        public string name { get; set; } = string.Empty;

        /// <summary>
        /// The shard index.
        /// </summary>
        [Type(FieldType.Integer), Indexed, Default(0)]
        public long index { get; set; } = 0;

        /// <summary>
        /// The shard value.
        /// </summary>
        [Type(FieldType.Integer), Indexed, Default(0)]
        public long value { get; set; } = 0;
    }
}
