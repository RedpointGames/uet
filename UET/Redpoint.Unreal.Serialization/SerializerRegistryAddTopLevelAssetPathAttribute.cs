namespace Redpoint.Unreal.Serialization
{
    using System;

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class SerializerRegistryAddTopLevelAssetPathAttribute : Attribute
    {
        public SerializerRegistryAddTopLevelAssetPathAttribute(Type _) { }
    }
}
