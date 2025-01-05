using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Redpoint.CloudFramework.Tests")]
[assembly: InternalsVisibleTo("Redpoint.CloudFramework.Tests.Shared")]

namespace Redpoint.CloudFramework
{
    using Redpoint.CloudFramework.Startup;

#pragma warning disable CA1724
    public static class CloudFramework
#pragma warning restore CA1724
    {
        /// <summary>
        /// Build a web application.
        /// </summary>
        public readonly static IWebAppConfigurator WebApp = new DefaultWebAppConfigurator();

        /// <summary>
        /// Build a service application, which runs in the background and processes either event-based or timing-based tasks.
        /// </summary>
        public readonly static IServiceAppConfigurator ServiceApp = new DefaultServiceAppConfigurator();

#if ENABLE_UNSUPPORTED
        /// <summary>
        /// Build an interactive console application. Used for command line tools that need to interact with your main app.
        /// </summary>
        public readonly static IInteractiveConsoleAppConfigurator InteractiveConsoleApp = new InteractiveConsoleAppConfigurator();
#endif
    }
}
