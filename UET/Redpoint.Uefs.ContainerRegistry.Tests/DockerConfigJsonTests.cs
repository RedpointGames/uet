namespace Redpoint.Uefs.ContainerRegistry.Tests
{
    using System.Text.Json;

    public class DockerConfigJsonTests
    {
        [Fact]
        public void CanReadCredsStore()
        {
            var content = "{\n  \"credsStore\": \"wincred\"\n}\n";
            var config = JsonSerializer.Deserialize(
                content,
                UefsRegistryJsonSerializerContext.Default.DockerConfigJson);
            Assert.NotNull(config);
            Assert.Equal("wincred", config.CredsStore);
        }
    }
}