namespace Redpoint.CloudFramework.CLI
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.OpenApi.Writers;
    using Redpoint.CloudFramework.Abstractions;
    using Redpoint.CommandLine;
    using Swashbuckle.AspNetCore.Swagger;
    using System;
    using System.CommandLine;
    using System.Globalization;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading.Tasks;

    internal class GenerateOpenApiJson
    {
        internal class Options
        {
            public Option<FileInfo> AssemblyPath = new Option<FileInfo>(
                "--assembly-path",
                "The path to the built .NET assembly.");

            public Option<FileInfo> OutputPath = new Option<FileInfo>(
                "--output-path",
                "The path to output the OpenAPI JSON file to.");

            public Option<string> Version = new Option<string>(
                "--version",
                () => "v1",
                "The document version to generate for.");
        }

        public static Command CreateCommand(ICommandBuilder builder)
        {
            return new Command("generate-openapi-json", "Generates an OpenAPI JSON file from the .NET assembly.");
        }

        internal class CommandInstance : ICommandInstance
        {
            private readonly ILogger<CommandInstance> _logger;
            private readonly Options _options;

            public CommandInstance(
                ILogger<CommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL2075 // 'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.
            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var assemblyPath = context.ParseResult.GetValueForOption(_options.AssemblyPath);
                if (assemblyPath == null || !assemblyPath.Exists)
                {
                    _logger.LogError("The input assembly for generating the OpenAPI JSON (--assembly-path) must exist.");
                    return 1;
                }
                var outputPath = context.ParseResult.GetValueForOption(_options.OutputPath);
                if (outputPath == null)
                {
                    _logger.LogError("The output path for generating the OpenAPI JSON (--output-path) must be specified.");
                    return 1;
                }
                var version = context.ParseResult.GetValueForOption(_options.Version);

                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
                Environment.SetEnvironmentVariable("CLOUDFRAMEWORK_IS_CLIENT_API_GENERATION", "true");

                AppDomain.CurrentDomain.AssemblyResolve += (sender, ev) =>
                {
                    var name = new AssemblyName(ev.Name);
                    var baseDir = Path.GetDirectoryName(assemblyPath.FullName);
                    var targetFile = Path.Combine(baseDir!, name.Name + ".dll");
                    if (File.Exists(targetFile))
                    {
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(targetFile);
                    }
                    if (!targetFile.EndsWith(".resources.dll", StringComparison.InvariantCultureIgnoreCase))
                    {
                        _logger.LogError($"Unable to find assembly at: {targetFile}");
                    }
                    return null;
                };

                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath.FullName);
                if (assembly == null)
                {
                    _logger.LogError($"Unable to load the assembly located at '{assemblyPath.FullName}'.");
                    return 1;
                }

                ICloudFrameworkWebHost? app;
                var providerType = assembly.GetExportedTypes()
                    .FirstOrDefault(x => typeof(IWebAppProvider).IsAssignableFrom(x));
                if (providerType == null)
                {
                    _logger.LogError("There is no class that implements IWebAppProvider. Your main Program class should implement it.");
                    return 1;
                }
                else
                {
                    var interfaceMap = providerType.GetInterfaceMap(typeof(IWebAppProvider));
                    MethodInfo? targetMethod = null;
                    for (var i = 0; i < interfaceMap.InterfaceMethods.Length; i++)
                    {
                        var interfaceMethod = interfaceMap.InterfaceMethods[i];
                        if (interfaceMethod.Name == "GetHostAsync" &&
                            interfaceMethod.ReturnType == typeof(ValueTask<ICloudFrameworkWebHost>))
                        {
                            targetMethod = interfaceMap.TargetMethods[i];
                        }
                    }
                    if (targetMethod == null)
                    {
                        _logger.LogError($"The '{providerType.FullName}' class does not correctly implement the IWebAppProvider interface.");
                        return 1;
                    }
                    var taskObject = targetMethod?.Invoke(null, Array.Empty<object>());
                    if (taskObject == null)
                    {
                        _logger.LogError($"The '{providerType.FullName}' class somehow returned a null value from GetWebHostAsync, even though it's return type should be a value type.");
                        return 1;
                    }
                    var task = (ValueTask<ICloudFrameworkWebHost>)taskObject;
                    app = await task.ConfigureAwait(false);
                }

                _logger.LogInformation("Getting ISwaggerProvider...");
                var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();

                _logger.LogInformation("Generating OpenAPI document...");
                try
                {
                    var swagger = swaggerProvider.GetSwagger(
                        documentName: string.IsNullOrWhiteSpace(version) ? "v1" : version,
                        host: null,
                        basePath: null);

                    _logger.LogInformation("Writing OpenAPI document to output path...");
                    using (var textWriter = new StringWriter(CultureInfo.InvariantCulture))
                    {
                        var jsonWriter = new OpenApiJsonWriter(textWriter);

                        swagger.SerializeAsV3(jsonWriter);

                        if (outputPath.DirectoryName != null)
                        {
                            Directory.CreateDirectory(outputPath.DirectoryName);
                        }
                        await File.WriteAllTextAsync(outputPath.FullName, textWriter.ToString()).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                    return 1;
                }

                _logger.LogInformation("OpenAPI generation complete.");
                return 0;
            }
#pragma warning restore IL2026
#pragma warning restore IL2075
        }
    }
}
