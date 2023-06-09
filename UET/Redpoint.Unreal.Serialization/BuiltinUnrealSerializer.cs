namespace Redpoint.Unreal.Serialization
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [SerializerRegistry]
    [SerializerRegistryAddSerializable(typeof(MessageAddress))]
    [SerializerRegistryAddSerializable(typeof(UnrealString))]
    [SerializerRegistryAddSerializable(typeof(Name))]
    [SerializerRegistryAddSerializable(typeof(TopLevelAssetPath))]
    internal partial class BuiltinUnrealSerializerRegistry : ISerializerRegistry
    {
    }
}
