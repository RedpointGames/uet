namespace Redpoint.CloudFramework.Models
{
    using System;

    /// <summary>
    /// Indicates that this property is indexed in Datastore.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class IndexedAttribute : Attribute
    {
    }
}
