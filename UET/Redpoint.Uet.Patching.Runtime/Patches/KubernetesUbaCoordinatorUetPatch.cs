namespace Redpoint.Uet.Patching.Runtime.Patches
{
    using HarmonyLib;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Text;
    using Microsoft.CodeAnalysis;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading.Tasks;
    using Redpoint.Uet.Patching.Runtime;
    using System.Runtime.InteropServices;
    using System.Collections;

    internal class KubernetesUbaCoordinatorUetPatch : IUetPatch
    {
        private static Assembly? _ubtUetAssembly;

        public bool ShouldApplyPatch()
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            return
                entryAssembly != null &&
                entryAssembly.GetName()?.Name == "UnrealBuildTool" &&
                entryAssembly.GetType("UnrealBuildTool.UBAExecutor") != null &&
                entryAssembly.GetType("UnrealBuildTool.IUBAAgentCoordinator") != null;
        }

        public void ApplyPatch(IUetPatchLogging logging, Harmony harmony)
        {
            // Create our working directory for compilation.
            var tempPath = Path.Combine(Path.GetTempPath(), $"uetstartuphook-{Environment.ProcessId}");
            Directory.CreateDirectory(tempPath);

            // Determine the location of UnrealBuildTool.dll.
            var loadedUbtAssembly = AssemblyLoadContext.Default.Assemblies.First(x => x.GetName().Name == "UnrealBuildTool");

            // Compile our Kubernetes coordinator type that we will hook into.
            logging.LogInfo($"Compiling the Kubernetes UBA coordinator...");
            {
                string sourceText;
                using (var sourceStream = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Redpoint.Uet.Patching.Runtime.Patches.KubernetesUbaCoordinatorPatchCode.cs")!))
                {
                    sourceText = sourceStream.ReadToEnd();
                }
                var source = SourceText.From(sourceText);
                var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
                var diagnostics = tree.GetDiagnostics();
                if (diagnostics.Any())
                {
                    Console.WriteLine("UET hook parse failure!");
                    foreach (var diagnostic in diagnostics)
                    {
                        Console.WriteLine(diagnostic);
                    }
                    return;
                }
                var referencedAssemblies = new List<MetadataReference>
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("System.Linq").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("EpicGames.Core").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("EpicGames.UBA").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("EpicGames.Build").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("EpicGames.IoHash").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("Microsoft.Extensions.Logging").Location),
                    MetadataReference.CreateFromFile(Assembly.Load("Microsoft.Extensions.Logging.Abstractions").Location),
                    MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Timer).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Type).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(RuntimeInformation).Assembly.Location),
                    MetadataReference.CreateFromFile(loadedUbtAssembly.Location)
                };
                var compilationOptions = new CSharpCompilationOptions(
                    outputKind: OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Debug,
                    warningLevel: 4,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default,
                    reportSuppressedDiagnostics: true);
                var compilation = CSharpCompilation.Create(
                    // @note: Very intentional! Allows this assembly to access the internals.
                    assemblyName: "UnrealBuildTool.Tests",
                    syntaxTrees: new List<SyntaxTree> { tree },
                    references: referencedAssemblies,
                    options: compilationOptions);
                using var ubtUetAssemblyMemory = new MemoryStream();
                var emitResult = compilation.Emit(ubtUetAssemblyMemory);
                if (!emitResult.Success)
                {
                    Console.WriteLine("UET hook compilation failure!");
                    foreach (var diagnostic in emitResult.Diagnostics)
                    {
                        Console.WriteLine(diagnostic);
                    }
                    return;
                }
                ubtUetAssemblyMemory.Seek(0, SeekOrigin.Begin);

                logging.LogInfo($"Loading the Kubernetes UBA coordinator assembly...");
                _ubtUetAssembly = Assembly.Load(ubtUetAssemblyMemory.ToArray());

                logging.LogInfo($"Loaded the Kubernetes UBA coordinator assembly!");
            }

            // Use Harmony to patch the constructor of UBAExecutor.
            logging.LogInfo($"Adding the Kubernetes coordinator to UBAExecutor constructor...");
            {
                var constructor = loadedUbtAssembly.GetType("UnrealBuildTool.UBAExecutor")?.GetDeclaredConstructors(false).FirstOrDefault();
                if (constructor == null)
                {
                    throw new InvalidOperationException("Unable to find UnrealBuildTool.UBAExecutor constructor!");
                }
                var postfix = GetType().GetMethod(nameof(UbaConstructorInjection), AccessTools.all);
                harmony.Patch(constructor, null, new HarmonyMethod(postfix));
            }

            // Use Harmony to patch the constructor of UBAExecutor.
            logging.LogInfo($"Patching XmlConfig.FindConfigurableTypes...");
            {
                var findConfigurableTypes = loadedUbtAssembly.GetType("UnrealBuildTool.XmlConfig")
                    ?.GetMethod("FindConfigurableTypes", BindingFlags.NonPublic | BindingFlags.Static);
                if (findConfigurableTypes == null)
                {
                    throw new InvalidOperationException("Unable to find UnrealBuildTool.UBAExecutor constructor!");
                }
                var postfix = GetType().GetMethod(nameof(XmlFindConfigurableTypesInjection), AccessTools.all);
                harmony.Patch(findConfigurableTypes, null, new HarmonyMethod(postfix));
            }
        }

        public static void UbaConstructorInjection(ref object __instance, object logger, object additionalArguments)
        {
            var coordinators = (IList)__instance.GetType()
                .GetField("_agentCoordinators", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(__instance)!;

            // Remove other coordinators.
            coordinators.Clear();

            // Add our Kubernetes coordinator.
            var config = __instance.GetType()
                .GetProperty("UBAConfig", BindingFlags.Public | BindingFlags.Instance)!
                .GetValue(__instance);
            var coordinator = _ubtUetAssembly!.GetType("UnrealBuildTool.UBAAgentCoordinatorKubernetesConstructor")!
                .GetMethod("Construct", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { logger, config!, additionalArguments });
            coordinators.Add(coordinator);
        }

        public static void XmlFindConfigurableTypesInjection(ref List<Type> __result)
        {
            Console.WriteLine("Handling XmlConfig.FindConfigurableTypes");
            var type = _ubtUetAssembly!.GetType("UnrealBuildTool.UnrealBuildAcceleratorKubernetesConfig");
            if (type == null)
            {
                throw new InvalidOperationException("Unable to find type 'UnrealBuildTool.UnrealBuildAcceleratorKubernetesConfig'!");
            }
            __result.Insert(0, type);
        }
    }
}
