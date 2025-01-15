namespace Redpoint.CloudFramework.Models
{
    using System;

    /// <summary>
    /// Sets the type that this value will be indexed as in Datastore. If you don't 
    /// add this attribute to a property, it will be ignored by Datastore.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class TypeAttribute : Attribute
    {
        public TypeAttribute(FieldType type)
        {
            Type = type;
        }

        public FieldType Type { get; }
    }
}
