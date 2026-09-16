namespace Redpoint.Uet.Patching.Runtime.Patches
{
    using HarmonyLib;
    using Redpoint.Uet.Patching.Runtime.Utilities;
    using System.Diagnostics;
    using System.Reflection;

    /// <summary>
    /// This is an attempt to fix a stall that we see on macOS when UBT is being run by BuildGraph. It doesn't happen when running from the terminal (even with I/O redirection), and even when we turn off capturing at the UET level, meaning the only conceivable place that it could be is inside Unreal's own process wrapping.
    /// </summary>
    internal class ProcessUtilsRunUetPatch : IUetPatch
    {
        private static IUetPatchLogging? _logging;
        private static Assembly? _utilsAssembly;

        public bool ShouldApplyPatch()
        {
            return
                Assembly.GetEntryAssembly()?.GetName()?.Name == "AutomationTool" &&
                OperatingSystem.IsMacOS();
        }

        public void ApplyPatch(IUetPatchLogging logging, Harmony harmony)
        {
            PatchUtilities.WaitForAssembly(
                logging,
                "AutomationUtils.Automation",
                utilsAssembly =>
                {
                    var runMethod = utilsAssembly!.GetType("AutomationTool.CommandUtils")!
                        .GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

                    _logging = logging;
                    _utilsAssembly = utilsAssembly;

                    logging.LogInfo($"Patching: {runMethod.GetType().FullName}.{runMethod.Name}");
                    try
                    {
                        harmony.Patch(runMethod, prefix: new HarmonyMethod(InterceptRunMethod));
                    }
                    catch (Exception ex)
                    {
                        logging.LogError(ex.ToString());
                    }
                });
        }

        static bool InterceptRunMethod(
            ref object? __result,
            string? App,
            string? CommandLine,
            string? Input,
            object? Options,
            Dictionary<string, string>? Env,
            object? SpewFilterCallback,
            object? Identifier,
            string? WorkingDir)
        {
            if (CommandLine != null &&
                CommandLine.Contains("UnrealBuildTool.dll", StringComparison.OrdinalIgnoreCase) &&
                CommandLine.Contains("-AllCores", StringComparison.OrdinalIgnoreCase) &&
                Options?.ToString() == "AllowSpew, NoStdOutCapture")
            {
                _logging?.LogInfo($"[patch] Replacing execution logic of UBT to mitigate stall on macOS: {App} {CommandLine} {Options}");

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = App,
                    Arguments = CommandLine,
                    CreateNoWindow = false,
                    RedirectStandardError = false,
                    RedirectStandardInput = false,
                    RedirectStandardOutput = false,
                };
                if (WorkingDir != null)
                {
                    processStartInfo.WorkingDirectory = WorkingDir;
                }
                if (Env != null)
                {
                    foreach (var kv in Env)
                    {
                        processStartInfo.Environment.Add(kv.Key, kv.Value);
                    }
                }

                var process = Process.Start(processStartInfo);
                process!.WaitForExit();

                var processResultType = _utilsAssembly!.GetType("AutomationTool.ProcessResult");

                var processResultConstructor = processResultType!
                    .GetConstructors(
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.Instance)
                    .First();

                var arguments = processResultConstructor.GetParameters().Select(x => x.DefaultValue).ToArray();
                arguments[0] = App;
                arguments[1] = process;
                arguments[2] = true;

                var processResult = processResultConstructor!.Invoke(arguments);

                processResultType
                    .GetProperty("ExitCode", BindingFlags.Public | BindingFlags.Instance)!
                    .GetSetMethod()!
                    .Invoke(processResult, new object?[] { process.ExitCode });

                __result = processResult;

                _logging?.LogInfo($"[patch] Replaced execution logic for UBT returned with exit code: {process.ExitCode}");

                return false;
            }

            return true;
        }
    }
}