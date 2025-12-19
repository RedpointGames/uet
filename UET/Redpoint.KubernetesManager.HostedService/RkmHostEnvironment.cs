using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Redpoint.KubernetesManager.HostedService
{
    public sealed class RkmHostEnvironment : IHostEnvironment
    {
        public RkmHostEnvironment(
            string applicationName)
        {
            EnvironmentName = "Production";
            ApplicationName = applicationName;
            ContentRootPath = Environment.CurrentDirectory;
            ContentRootFileProvider = null!;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
