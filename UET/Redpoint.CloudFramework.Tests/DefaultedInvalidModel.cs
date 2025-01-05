using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests
{
    [Kind<DefaultedInvalidModel>("cf_defaultedModel")]
    public class DefaultedInvalidModel : AttributedModel
    {
        [Type(FieldType.String), Indexed]
        public string? myString { get; set; }

        [Type(FieldType.Boolean), Indexed]
        public bool myBool { get; set; }

        [Type(FieldType.Integer), Indexed]
        public long myInteger { get; set; }
    }
}
