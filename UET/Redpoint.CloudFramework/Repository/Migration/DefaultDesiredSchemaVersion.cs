namespace Redpoint.CloudFramework.Repository.Migration
{
    internal class DefaultDesiredSchemaVersion<TMigrator> : IDesiredSchemaVersion<TMigrator>
    {
        public DefaultDesiredSchemaVersion(long schemaVersion)
        {
            SchemaVersion = schemaVersion;
        }

        public long SchemaVersion { get; }
    }
}
