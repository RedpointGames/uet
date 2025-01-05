using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests
{
    [Kind<BadModel>("cf_badModel")]
    public class BadModel : AttributedModel
    {
        [Type(FieldType.String), Indexed]
        public object? badField { get; set; }
    }
}
