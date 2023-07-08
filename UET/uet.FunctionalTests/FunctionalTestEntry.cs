namespace uet.FunctionalTests
{
    using System.Text.Json;
    using Xunit.Abstractions;

    public class FunctionalTestEntry : IXunitSerializable
    {
        public required FunctionalTestConfig Config { get; set; }

        public required string Name { get; set; }

        public required string Path { get; set; }

        public void Deserialize(IXunitSerializationInfo info)
        {
            Config = JsonSerializer.Deserialize<FunctionalTestConfig>(info.GetValue<string>("config"))!;
            Name = info.GetValue<string>("name");
            Path = info.GetValue<string>("path");
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("config", JsonSerializer.Serialize(Config));
            info.AddValue("name", Name);
            info.AddValue("path", Path);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}