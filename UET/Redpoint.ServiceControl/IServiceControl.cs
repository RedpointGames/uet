namespace Redpoint.ServiceControl
{
    using System.Threading.Tasks;

    /// <summary>
    /// Control and query services on the current system.
    /// </summary>
    public interface IServiceControl
    {
        /// <summary>
        /// Returns whether the current user has permission to install services on this system.
        /// </summary>
        bool HasPermissionToInstall { get; }

        /// <summary>
        /// Returns whether the current user has permission to start services on this system.
        /// </summary>
        bool HasPermissionToStart { get; }

        /// <summary>
        /// Returns whether or not a service with the given name is installed.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <returns>Whether or not the service is installed.</returns>
        Task<bool> IsServiceInstalled(string name);

        /// <summary>
        /// Returns the executable name and arguments that the service is configured for.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <returns>The executable name and arguments.</returns>
        Task<string> GetServiceExecutableAndArguments(string name);

        /// <summary>
        /// Returns whether or not a service with the given name is running.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <returns>Whether or not the service is running.</returns>
        Task<bool> IsServiceRunning(string name);

        /// <summary>
        /// Installs or updates the service with the given name.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <param name="description">The description of the service.</param>
        /// <param name="executableAndArguments">The executable and arguments to launch.</param>
        /// <returns>The awaitable task.</returns>
        Task InstallService(string name, string description, string executableAndArguments);

        /// <summary>
        /// Uninstalls the service.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <returns>The awaitable task.</returns>
        Task UninstallService(string name);

        /// <summary>
        /// Start the service.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <returns>The awaitable task.</returns>
        Task StartService(string name);

        /// <summary>
        /// Stop the service.
        /// </summary>
        /// <param name="name">The service name.</param>
        /// <returns>The awaitable task.</returns>
        Task StopService(string name);
    }

}
