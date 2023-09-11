namespace UET.Services
{
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    internal class DefaultSelfLocation : ISelfLocation
    {
        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Auto-detection")]
        public string GetUETLocalLocation()
        {
            var assembly = Assembly.GetEntryAssembly();
            if (string.IsNullOrWhiteSpace(assembly?.Location))
            {
                return Process.GetCurrentProcess().MainModule!.FileName;
            }
            else
            {
                var location = assembly.Location;
                if (location.EndsWith(".dll"))
                {
                    // When running via 'dotnet', the .dll file is returned instead of the .exe bootstrapper.
                    // We want to launch via the .exe instead.
                    location = location[..^4] + ".exe";
                }
                return location;
            }
        }
    }
}
