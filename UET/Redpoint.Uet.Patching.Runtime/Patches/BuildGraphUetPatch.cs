namespace Redpoint.Uet.Patching.Runtime.Patches
{
    using HarmonyLib;
    using Redpoint.Uet.Patching.Runtime.Utilities;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;

    internal class BuildGraphUetPatch : IUetPatch
    {
        public bool ShouldApplyPatch()
        {
            return Assembly.GetEntryAssembly()?.GetName()?.Name == "AutomationTool";
        }

        static MethodInfo? _getCommandEnvironmentLocalRoot;
        static IUetPatchLogging? _logging;

        public void ApplyPatch(IUetPatchLogging logging, Harmony harmony)
        {
            logging.LogInfo("BuildGraph patch is running for AutomationTool.");

            _logging = logging;

            PatchUtilities.WaitForAssembly(
                logging,
                "BuildGraph.Automation",
                buildGraphAssembly =>
                {
                    PatchUtilities.WaitForAssembly(
                        logging,
                        "AutomationUtils.Automation",
                        utilsAssembly =>
                        {
                            _getCommandEnvironmentLocalRoot = utilsAssembly!.GetType("AutomationTool.CommandEnvironment")!
                                .GetProperty("LocalRoot", BindingFlags.Public | BindingFlags.Instance)!
                                .GetGetMethod()!;

                            logging.LogInfo($"Located LocalRoot property getter at: {_getCommandEnvironmentLocalRoot.Name}");

                            var type = buildGraphAssembly.GetType("AutomationTool.BuildGraph")!;
                            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                            {
                                if (method.IsDeclaredMember() && method.Name != "BuildAllNodesAsync" /* This doesn't patch properly */)
                                {
                                    logging.LogInfo($"Patching: {type.FullName}.{method.Name}");
                                    try
                                    {
                                        harmony.PatchPossibleAsyncMethod(method, transpiler: new HarmonyMethod(ReplaceLocalRoot));
                                    }
                                    catch (Exception ex)
                                    {
                                        logging.LogError(ex.ToString());
                                    }
                                }
                            }
                        });
                });
        }

        static IEnumerable<CodeInstruction> ReplaceLocalRoot(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator,
            MethodBase original)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(_getCommandEnvironmentLocalRoot!))
                {
                    _logging!.LogInfo($"Found call to {_getCommandEnvironmentLocalRoot!.Name} in {original.Name}.");
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Call, _preferBuildGraphProjectRoot);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        static MethodInfo _preferBuildGraphProjectRoot = SymbolExtensions.GetMethodInfo<string, string>(fallback => PreferBuildGraphProjectRoot(fallback));

        static string PreferBuildGraphProjectRoot(string fallback)
        {
            _logging!.LogInfo($"Checking BUILD_GRAPH_PROJECT_ROOT at runtime!");
            return Environment.GetEnvironmentVariable("BUILD_GRAPH_PROJECT_ROOT") ?? fallback;
        }

        //
        // replace:
        //
        // if (modifiedFiles.Count > 0)
        // ->
        // if (modifiedFiles.Count > 0 && Environment.GetEnvironmentVariable("BUILD_GRAPH_ALLOW_MUTATION") != "true")
        //

        // @note: This patch should no longer be necessary, since FileReference.Combine properly handles absolute fragments.
        //
        // replace:
        //
        // FileReference fullScriptFile = FileReference.Combine(Unreal.RootDirectory, scriptFileName);
        // ->
        // FileReference fullScriptFile = FileReference.Combine(scriptFileName);
        //

        //
        // replace:
        //
        // if (!modifiedFiles.ContainsKey(file.RelativePath) && !ignoreModifiedFilter.Matches(file.ToFileReference(Unreal.RootDirectory).FullName) && !file.Compare(Unreal.RootDirectory, out message))
        // ->
        // if (!modifiedFiles.ContainsKey(file.RelativePath) && !ignoreModifiedFilter.Matches(file.ToFileReference(Environment.GetEnvironmentVariable(\"BUILD_GRAPH_PROJECT_ROOT\") != null ? new DirectoryReference(Environment.GetEnvironmentVariable(\"BUILD_GRAPH_PROJECT_ROOT\")!) : Unreal.RootDirectory).FullName) && !file.Compare(Environment.GetEnvironmentVariable(\"BUILD_GRAPH_PROJECT_ROOT\") != null ? new DirectoryReference(Environment.GetEnvironmentVariable(\"BUILD_GRAPH_PROJECT_ROOT\")!) : Unreal.RootDirectory, out message))
        //
    }
}
