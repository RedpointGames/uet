namespace Redpoint.CloudFramework.Startup
{
    using Microsoft.AspNetCore.Hosting;
    using System.Threading.Tasks;

    public interface IWebAppProvider
    {
        static abstract ValueTask<IWebHost> GetWebHostAsync();
    }
}
