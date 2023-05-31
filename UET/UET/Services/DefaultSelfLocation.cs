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
                return assembly.Location;
            }
        }
    }
}
