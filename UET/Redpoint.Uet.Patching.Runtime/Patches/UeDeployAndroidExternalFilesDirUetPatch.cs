namespace Redpoint.Uet.Patching.Runtime.Patches
{
    using HarmonyLib;
    using System.Reflection;
    using System.Runtime.Loader;

    internal class UeDeployAndroidExternalFilesDirUetPatch : IUetPatch
    {
        private static IUetPatchLogging? _logging;

        public bool ShouldApplyPatch()
        {
            return Assembly.GetEntryAssembly()?.GetName()?.Name == "UnrealBuildTool";
        }

        public void ApplyPatch(IUetPatchLogging logging, Harmony harmony)
        {
            // Get the UnrealBuildTool.dll assembly.
            var loadedUbtAssembly = AssemblyLoadContext.Default.Assemblies.First(x => x.GetName().Name == "UnrealBuildTool");

            // Find the UseExternalFilesDir method.
            var method = loadedUbtAssembly.GetType("UnrealBuildTool.UEDeployAndroid")?.GetDeclaredMethods()
                .Where(x => x.Name == "UseExternalFilesDir")
                .FirstOrDefault();
            if (method == null)
            {
                throw new InvalidOperationException("Unable to find UnrealBuildTool.UEDeployAndroid.UseExternalFilesDir method!");
            }

            // Store the logger so we can emit logs when overriding the disallow parameter.
            _logging = logging;

            // Apply the prefix patch to always set 'bDisallowExternalFilesDir' to false.
            var prefix = GetType().GetMethod(nameof(OverrideDisallowExternalFilesDir), AccessTools.all);
            harmony.Patch(method, prefix: new HarmonyMethod(prefix));
        }

        public static void OverrideDisallowExternalFilesDir(ref bool bDisallowExternalFilesDir)
        {
            _logging?.LogInfo("Intercepting 'UseExternalFilesDir' method and setting 'bDisallowExternalFilesDir' to false.");
            bDisallowExternalFilesDir = false;
        }
    }
}
