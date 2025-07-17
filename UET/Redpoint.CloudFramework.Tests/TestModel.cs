using Google.Cloud.Datastore.V1;
using NodaTime;
using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests
{
    // This ensures the tests pass when using properties on base classes.

    [Kind("cf_testModel")]
    public sealed class TestModel : Model<TestModel>
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
#pragma warning disable CS0628 // New protected member declared in sealed type
        protected string? protectedString1 { get; set; }
#pragma warning restore CS0628 // New protected member declared in sealed type

        [Type(FieldType.String), Indexed]
        private string? privateString1 { get; set; }

        [Type(FieldType.String), Indexed]
        internal string? internalString1 { get; set; }

        [Type(FieldType.StringArray), Indexed, Default(new string[0])]
        public string[] stringArray { get; set; } = [];
    }
}
