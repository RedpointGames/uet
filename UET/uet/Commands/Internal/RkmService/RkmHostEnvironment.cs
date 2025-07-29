using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using UET.Services;

namespace UET.Commands.Internal.Rkm
{
    internal sealed class RkmHostEnvironment : IHostEnvironment
    {
        public RkmHostEnvironment(
            ISelfLocation selfLocation)
        {
            EnvironmentName = "Production";
            ApplicationName = "RKM";
            ContentRootPath = Path.GetDirectoryName(selfLocation.GetUetLocalLocation(true))!;
            ContentRootFileProvider = null!;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
