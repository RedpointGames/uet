namespace Redpoint.CloudFramework.Infrastructure
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Internal interface for accessing enumeration type with fields.
    /// </summary>
    internal interface INamedEnum
    {
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
        Type EnumType { get; }
    }
}
