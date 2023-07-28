namespace Redpoint.OpenGE.Executor.CompilerDb.PreprocessorScanner
{
    using Tenray.ZoneTree.Serializers;

    internal class PreprocessorScanResultSerializer : ISerializer<PreprocessorScanResult>
    {
        public PreprocessorScanResult Deserialize(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var utcTicks = reader.ReadInt64();
                    var includesCount = reader.ReadInt32();
                    var includes = new string[includesCount];
                    for (var i = 0; i < includesCount; i++)
                    {
                        includes[i] = reader.ReadString();
                    }
                    var systemIncludesCount = reader.ReadInt32();
                    var systemIncludes = new string[systemIncludesCount];
                    for (var i = 0; i < systemIncludesCount; i++)
                    {
                        systemIncludes[i] = reader.ReadString();
                    }
                    var compiledPlatformHeaderIncludesCount = reader.ReadInt32();
                    var compiledPlatformHeaderIncludes = new string[compiledPlatformHeaderIncludesCount];
                    for (var i = 0; i < compiledPlatformHeaderIncludesCount; i++)
                    {
                        compiledPlatformHeaderIncludes[i] = reader.ReadString();
                    }
                    return new PreprocessorScanResult
                    {
                        FileLastWriteTicks = utcTicks,
                        Includes = includes,
                        SystemIncludes = systemIncludes,
                        CompiledPlatformHeaderIncludes = compiledPlatformHeaderIncludes,
                    };
                }
            }
        }

        public byte[] Serialize(in PreprocessorScanResult entry)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(entry.FileLastWriteTicks);
                    writer.Write(entry.Includes.Length);
                    for (var i = 0; i < entry.Includes.Length; i++)
                    {
                        writer.Write(entry.Includes[i]);
                    }
                    writer.Write(entry.SystemIncludes.Length);
                    for (var i = 0; i < entry.SystemIncludes.Length; i++)
                    {
                        writer.Write(entry.SystemIncludes[i]);
                    }
                    writer.Write(entry.CompiledPlatformHeaderIncludes.Length);
                    for (var i = 0; i < entry.CompiledPlatformHeaderIncludes.Length; i++)
                    {
                        writer.Write(entry.CompiledPlatformHeaderIncludes[i]);
                    }
                }
                return stream.ToArray();
            }
        }
    }
}
