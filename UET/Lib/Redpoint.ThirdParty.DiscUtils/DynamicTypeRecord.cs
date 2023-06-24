namespace DiscUtils
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    internal record class DynamicTypeRecord
    {
        public DynamicTypeRecord([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        {
            Type = type;
        }

        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        public Type Type { get; set; }
    }
}
