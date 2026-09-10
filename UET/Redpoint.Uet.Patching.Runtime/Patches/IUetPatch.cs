namespace Redpoint.Uet.Patching.Runtime.Patches
{
    using HarmonyLib;
    using Redpoint.Uet.Patching.Runtime;

    internal interface IUetPatch
    {
        bool ShouldApplyPatch();

        void ApplyPatch(IUetPatchLogging logging, Harmony harmony);
    }
}
