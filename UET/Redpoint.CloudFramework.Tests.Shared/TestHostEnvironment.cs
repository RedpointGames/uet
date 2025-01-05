namespace Redpoint.CloudFramework.Tests
{
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Hosting;

    internal class TestHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = null!;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = null!;
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
