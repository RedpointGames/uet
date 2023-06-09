namespace Redpoint.Unreal.Serialization.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [SerializerRegistry]
    [SerializerRegistryAddSerializable(typeof(ArchiveArray<int, UnrealString>))]
    [SerializerRegistryAddSerializable(typeof(ArchiveArray<int, int>))]
    [SerializerRegistryAddSerializable(typeof(ArchiveMap<int, UnrealString, UnrealString>))]
    [SerializerRegistryAddSerializable(typeof(ArchiveMap<int, UnrealString, int>))]
    [SerializerRegistryAddSerializable(typeof(ArchiveMap<int, int, UnrealString>))]
    [SerializerRegistryAddSerializable(typeof(ArchiveMap<int, int, int>))]
    internal partial class TestSerializerRegistry : ISerializerRegistry
    {
    }
}
