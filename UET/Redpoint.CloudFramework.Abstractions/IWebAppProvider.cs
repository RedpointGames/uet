namespace Redpoint.CloudFramework.Abstractions
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;
    using System.Threading.Tasks;

    public interface IWebAppProvider
    {
        static abstract ValueTask<IHost> GetHostAsync();
    }
}
