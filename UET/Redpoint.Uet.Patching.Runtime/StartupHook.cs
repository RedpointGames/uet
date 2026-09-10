using System.Reflection;
using System.Runtime.Loader;

internal class StartupHook
{
    public static void Initialize()
    {
        // Set our assembly resolver for Mono.Cecil and other things.
        var ourDirectoryPath = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName!;
        AssemblyLoadContext.Default.Resolving += (AssemblyLoadContext context, AssemblyName name) =>
        {
            var targetAssembly = Path.Combine(ourDirectoryPath, name.Name + ".dll");
            if (File.Exists(targetAssembly))
            {
                return Assembly.LoadFrom(targetAssembly);
            }
            return null;
        };

        // Then call our real code.
        Redpoint.Uet.Patching.Runtime.UetStartupHook.Initialize();
    }
}
