using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests
{
    [Kind("cf_badModel")]
    public sealed class BadModel : Model<BadModel>
    {
        [Type(FieldType.String), Indexed]
        public object? badField { get; set; }
    }
}
