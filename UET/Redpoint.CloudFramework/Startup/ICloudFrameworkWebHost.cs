namespace Redpoint.CloudFramework.Startup
{
    using Microsoft.AspNetCore.Hosting;
    using Redpoint.CloudFramework.Abstractions;
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class DefaultCloudFrameworkWebHost : ICloudFrameworkWebHost
    {
        public required IWebHost WebHost { get; set; }

        public IServiceProvider Services => WebHost.Services;

        public Task RunAsync()
        {
            return WebHost.RunAsync();
        }
    }
}
