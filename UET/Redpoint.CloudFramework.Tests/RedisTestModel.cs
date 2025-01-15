using Google.Cloud.Datastore.V1;
using NodaTime;
using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests
{
    [Kind("cf_redisTest")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Cloud Framework", "CloudFrameworkModelAnalyzerSealed:Types implementing Model<T> should be sealed", Justification = "Internal testing.")]
    public class RedisTestModel : Model<RedisTestModel>
    {
        [Type(FieldType.String), Indexed]
        public string? forTest { get; set; }

        [Type(FieldType.String), Indexed]
        public string? string1 { get; set; }

        [Type(FieldType.Integer), Indexed]
        public long? number1 { get; set; }

        [Type(FieldType.Integer), Indexed]
        public long? number2 { get; set; }

        [Type(FieldType.Timestamp), Indexed]
        public Instant? timestamp { get; set; }

        [Type(FieldType.Key)]
        public Key? keyValue { get; set; }

        public TestModel? untracked { get; set; }

        [Type(FieldType.String), Indexed]
        protected string? protectedString1 { get; set; }

        [Type(FieldType.String), Indexed]
        private string? privateString1 { get; set; }

        [Type(FieldType.String), Indexed]
        internal string? internalString1 { get; set; }
    }
}
