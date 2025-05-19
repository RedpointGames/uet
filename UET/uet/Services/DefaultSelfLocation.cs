namespace UET.Services
{
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    internal sealed class DefaultSelfLocation : ISelfLocation
    {
        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Auto-detection")]
        public string GetUetLocalLocation(bool versionIndependent)
        {
            string location;

            // Get the path to the executable.
            {
                var assembly = Assembly.GetEntryAssembly();
                if (string.IsNullOrWhiteSpace(assembly?.Location))
                {
#pragma warning disable CA1839 // Use 'Environment.ProcessPath'
                    location = Process.GetCurrentProcess().MainModule!.FileName;
#pragma warning restore CA1839 // Use 'Environment.ProcessPath'
                }
                else
                {
                    location = assembly.Location;
                    if (location.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        // When running via 'dotnet', the .dll file is returned instead of the .exe bootstrapper.
                        // We want to launch via the .exe instead.
                        location = location[..^4] + ".exe";
                    }
                }
            }

            if (versionIndependent)
            {
                // If requesting a version independent location, replace our version number with "Current".
                location = location.Replace(
                    $"{Path.DirectorySeparatorChar}{RedpointSelfVersion.GetInformationalVersion()}{Path.DirectorySeparatorChar}",
                    $"{Path.DirectorySeparatorChar}Current{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal);
            }

            return location;
        }
    }
}
