namespace Redpoint.CloudFramework.Infrastructure
{
    using System;

    /// <summary>
    /// Associates a name with an enumeration value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class NamedEnumValueAttribute : Attribute
    {
        public NamedEnumValueAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// The name associated with the enumeration value.
        /// </summary>
        public string Name { get; private set; }
    }
}
