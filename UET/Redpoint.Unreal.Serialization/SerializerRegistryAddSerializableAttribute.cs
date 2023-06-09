namespace Redpoint.Unreal.Serialization
{
    using System;

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class SerializerRegistryAddSerializableAttribute : Attribute
    {
        public SerializerRegistryAddSerializableAttribute(Type _) { }
    }
}
