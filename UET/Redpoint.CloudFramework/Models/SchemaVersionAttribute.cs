namespace Redpoint.CloudFramework.Models
{
    using System;

    /// <summary>
    /// Overrides the schema version for this model. If you don't use this attribute, the schema
    /// version defaults to 1.
    /// 
    /// This attribute is also used to declare schema versions for nested JSON structures.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class SchemaVersionAttribute : Attribute
    {
        public SchemaVersionAttribute(long schemaVersion)
        {
            SchemaVersion = schemaVersion;
        }

        public long SchemaVersion { get; }
    }
}
