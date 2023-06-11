namespace Redpoint.Unreal.Serialization
{
    [SerializerRegistry]
    [SerializerRegistryAddSerializable(typeof(MessageAddress))]
    [SerializerRegistryAddSerializable(typeof(UnrealString))]
    [SerializerRegistryAddSerializable(typeof(Name))]
    [SerializerRegistryAddSerializable(typeof(TopLevelAssetPath))]
    internal partial class BuiltinUnrealSerializerRegistry : ISerializerRegistry
    {
    }
}
