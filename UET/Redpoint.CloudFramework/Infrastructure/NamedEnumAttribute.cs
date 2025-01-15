namespace Redpoint.CloudFramework.Infrastructure
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Indicates that an enumeration contains named values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum)]
    public sealed class NamedEnumAttribute<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T> : Attribute, INamedEnum
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
        Type INamedEnum.EnumType => typeof(T);
    }
}
