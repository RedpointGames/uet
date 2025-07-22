using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests.Models
{
    [Kind("cf_defaultedModel")]
    public sealed class DefaultedBypassModel : Model<DefaultedBypassModel>
    {
        [Type(FieldType.String), Indexed]
        public string? myString { get; set; }

        [Type(FieldType.Boolean), Indexed]
        public bool? myBool { get; set; }

        [Type(FieldType.Integer), Indexed]
        public long? myInteger { get; set; }
    }
}
