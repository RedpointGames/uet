using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests
{
    [Kind<DefaultedBypassModel>("cf_defaultedModel")]
    public class DefaultedBypassModel : AttributedModel
    {
        [Type(FieldType.String), Indexed]
        public string? myString { get; set; }

        [Type(FieldType.Boolean), Indexed]
        public bool? myBool { get; set; }

        [Type(FieldType.Integer), Indexed]
        public long? myInteger { get; set; }
    }
}
