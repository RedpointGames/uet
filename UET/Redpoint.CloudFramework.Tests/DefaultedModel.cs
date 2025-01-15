using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests
{
    [Kind("cf_defaultedModel")]
    public sealed class DefaultedModel : Model<DefaultedModel>
    {
        // This model exists to ensure AttributedModel initializes
        // properties to their default values.
#pragma warning disable CS8618
        [Type(FieldType.String), Indexed, Default("test")]
        public string myString { get; set; }
#pragma warning restore CS8618

        [Type(FieldType.Boolean), Indexed, Default(true)]
        public bool myBool { get; set; }

        [Type(FieldType.Integer), Indexed, Default(10)]
        public long myInteger { get; set; }
    }
}
