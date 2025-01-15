namespace Redpoint.CloudFramework.Models
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Sets the kind of the entity when this model is stored in Datastore.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class KindAttribute : Attribute
    {
        public KindAttribute(string kind)
        {
            Kind = kind;
        }

        public string Kind { get; }
    }
}
