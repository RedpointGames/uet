namespace Redpoint.CloudFramework.Repository.Migration
{
    public interface IDesiredSchemaVersion<TMigrator>
    { 
        public long SchemaVersion { get; }
    }
}
