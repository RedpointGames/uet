namespace Redpoint.Uet.Patching.Runtime.Utilities
{
    using HarmonyLib;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    internal static class PatchUtilities
    {
        public static void WaitForAssembly(
            IUetPatchLogging logging,
            string assemblyName,
            Action<Assembly> callbackForAssembly)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName != null &&
                    new AssemblyName(assembly.FullName).Name == assemblyName)
                {
                    callbackForAssembly(assembly);
                    return;
                }
            }

            // The assembly isn't loaded yet.
            logging.LogInfo($"Waiting for assembly '{assemblyName}' to load before continuing to patch...");
            _ = new AssemblyLoadListener(
                logging,
                assemblyName,
                callbackForAssembly);
        }

        private class AssemblyLoadListener
        {
            private readonly IUetPatchLogging _logging;
            private readonly string _assemblyName;
            private readonly Action<Assembly> _callbackForAssembly;

            public AssemblyLoadListener(
                IUetPatchLogging logging,
                string assemblyName,
                Action<Assembly> callbackForAssembly)
            {
                _logging = logging;
                _assemblyName = assemblyName;
                _callbackForAssembly = callbackForAssembly;

                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            }

            void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
            {
                if (args.LoadedAssembly.FullName != null &&
                    new AssemblyName(args.LoadedAssembly.FullName).Name == _assemblyName)
                {
                    AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;

                    _logging.LogInfo($"Assembly '{args.LoadedAssembly.FullName}' is now loaded.");
                    _callbackForAssembly(args.LoadedAssembly);
                    return;
                }
            }
        }

        public static void PatchPossibleAsyncMethod(
            this Harmony harmony,
            MethodBase target,
            HarmonyMethod? prefix = null,
            HarmonyMethod? postfix = null,
            HarmonyMethod? transpiler = null,
            HarmonyMethod? finalizer = null)
        {
            var stateMachineAttr = target.GetCustomAttribute<AsyncStateMachineAttribute>();

            if (stateMachineAttr is null)
            {
                harmony.Patch(target, prefix, postfix, transpiler, finalizer);
            }
            else
            {
                var stateMachineType = stateMachineAttr.StateMachineType;
                var moveNextMethod = stateMachineType.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance);

                if (moveNextMethod is null)
                {
                    throw new ArgumentException(
                        $"The method '{target.Name}' is not an asynchronous method with a state machine");
                }

                harmony.Patch(moveNextMethod, prefix, postfix, transpiler, finalizer);
            }
        }
    }
}
