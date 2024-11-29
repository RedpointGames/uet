namespace Redpoint.Uet.Patching.Runtime.Patches
{
    using HarmonyLib;
    using System.Reflection;

    internal class BuildGraphProjectRootUetPatch : IUetPatch
    {
        public bool ShouldApplyPatch()
        {
            return Assembly.GetEntryAssembly()?.GetName()?.Name == "AutomationTool";
        }

        public void ApplyPatch(IUetPatchLogging logging, Harmony harmony)
        {
        }
    }
}
