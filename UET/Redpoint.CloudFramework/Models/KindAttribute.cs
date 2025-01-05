namespace Redpoint.CloudFramework.Models
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Sets the kind of the entity when this model is stored in Datastore.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class KindAttribute<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T> : Attribute, IKindAttribute
    {
        public KindAttribute(string kind)
        {
            Kind = kind;
        }

        public string Kind { get; }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
        public Type Type => typeof(T);
    }

    internal interface IKindAttribute
    {
        string Kind { get; }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
        Type Type { get; }
    }
}
