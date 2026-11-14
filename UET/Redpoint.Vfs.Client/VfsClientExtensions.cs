namespace Redpoint.Vfs.Client
{
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Collections.Generic;
    using System.Runtime.Versioning;
    using System.Text;

    /// <summary>
    /// Registers the <see cref="IVfsClient"/> implementation with the dependency injection service collection.
    /// </summary>
    public static class VfsClientExtensions
    {
        /// <summary>
        /// Registers the <see cref="IVfsClient"/> implementation with the dependency injection service collection.
        /// </summary>
        /// <param name="services">The service collection to register the implementation with.</param>
        public static void AddVfsClient(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                services.AddSingleton<IVfsClient, WindowsVfsClient>();
            }
            else if (OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IVfsClient, MacVfsClient>();
            }
            else
            {
                services.AddSingleton<IVfsClient, NullVfsClient>();
            }
        }
    }
}
