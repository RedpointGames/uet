using Redpoint.CloudFramework.Models;

namespace Redpoint.CloudFramework.Tests.Models
{
    [Kind("cf_migrationModel"), SchemaVersion(4)]
    internal sealed class MigrationModel : Model<MigrationModel>
    {
        [Type(FieldType.String), Indexed, Default("")]
        public string stringField { get; set; } = string.Empty;
    }
}
