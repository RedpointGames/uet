namespace Redpoint.Unreal.Serialization
{
    using System;

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class SerializerRegistryAddTopLevelAssetPathAttribute : Attribute
    {
        public SerializerRegistryAddTopLevelAssetPathAttribute(Type _) { }
    }
}
