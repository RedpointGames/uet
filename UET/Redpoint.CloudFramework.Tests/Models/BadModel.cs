using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests.Models
{
    [Kind("cf_badModel")]
    public sealed class BadModel : Model<BadModel>
    {
        [Type(FieldType.String), Indexed]
        public object? badField { get; set; }
    }
}
