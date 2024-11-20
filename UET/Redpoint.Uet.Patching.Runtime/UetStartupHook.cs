namespace Redpoint.Uet.Patching.Runtime
{
    using HarmonyLib;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Text;
    using Redpoint.Uet.Patching.Runtime.Patches;
    using System;
    using System.Reflection;
    using System.Runtime.Loader;

    public static class UetStartupHook
    {
        public static void Initialize()
        {
            // Prevent MSBuild from re-using nodes, since we're injected into all .NET processes (including MSBuild).
            Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

            // Our list of patches.
            var patches = new IUetPatch[]
            {
                new UeDeployAndroidExternalFilesDirUetPatch(),
            };

            // Determine if we have any patches to apply. If we have none, we're done.
            if (!patches.Any(x => x.ShouldApplyPatch()))
            {
                return;
            }

            // Create our logging instance.
            IUetPatchLogging logging = Environment.GetEnvironmentVariable("UET_RUNTIME_PATCHING_ENABLE_LOGGING") == "1"
                ? new ConsoleUetPatchLogging()
                : new NullUetPatchLogging();

            // Create our Harmony instance.
            var harmony = new Harmony("games.redpoint.uet");

            // Go through our patches and apply the ones that are relevant.
            foreach (var patch in patches)
            {
                if (patch.ShouldApplyPatch())
                {
                    patch.ApplyPatch(logging, harmony);
                }
            }
        }
    }
}
