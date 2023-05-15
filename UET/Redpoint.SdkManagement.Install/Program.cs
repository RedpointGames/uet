using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Redpoint.ProcessExecution;
using Redpoint.SdkManagement;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;

var enginePathOption = new Option<DirectoryInfo>(
    name: "--engine-path",
    description: "The path to the Unreal Engine installation.")
{
    IsRequired = true,
};
var packagePathOption = new Option<DirectoryInfo>(
    name: "--sdk-package-path",
    description: "The path to store generated SDK packages.")
{
    IsRequired = true,
};
var platformOption = new Option<string>(
    name: "--platform",
    description: "The platform to install SDKs for. Defaults to the host platform.");

var rootCommand = new RootCommand("Installs the required SDKs for Unreal Engine to build a given platform, and emits the environment variables that would be set during a build. This tool is mostly for testing; UES will automatically run this process when it is executing a build on a machine before the build starts.");
rootCommand.AddOption(enginePathOption);
rootCommand.AddOption(packagePathOption);
rootCommand.AddOption(platformOption);

rootCommand.SetHandler(async (InvocationContext context) =>
{
    var enginePath = context.ParseResult.GetValueForOption(enginePathOption);
    var packagePath = context.ParseResult.GetValueForOption(packagePathOption);
    var platform = context.ParseResult.GetValueForOption(platformOption);

    if (enginePath == null || !enginePath.Exists)
    {
        Console.Error.WriteLine("error: --engine-path must be set and exist.");
        Environment.ExitCode = 1;
        return;
    }
    if (packagePath == null)
    {
        Console.Error.WriteLine("error: --sdk-package-path must be set.");
        Environment.ExitCode = 1;
        return;
    }
    if (!packagePath.Exists)
    {
        packagePath.Create();
    }
    if (string.IsNullOrWhiteSpace(platform))
    {
        if (OperatingSystem.IsWindows())
        {
            platform = "Windows";
        }
        else if (OperatingSystem.IsMacOS())
        {
            platform = "Mac";
        }
        else if (OperatingSystem.IsLinux())
        {
            platform = "Linux";
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    var services = new ServiceCollection();
    services.AddLogging(configure => configure.AddConsole());
    services.AddExecutors();
    if (OperatingSystem.IsMacOS())
    {
        services.AddSingleton<MacSdkSetup, MacSdkSetup>();
    }
    if (OperatingSystem.IsWindows())
    {
        services.AddSingleton<AndroidSdkSetup, AndroidSdkSetup>();
        services.AddSingleton<WindowsSdkSetup, WindowsSdkSetup>();
    }

    var serviceProvider = services.BuildServiceProvider();

    ISdkSetup? setup = null;
    switch (platform)
    {
        case "Mac":
            if (OperatingSystem.IsMacOS())
            {
                setup = serviceProvider.GetRequiredService<MacSdkSetup>();
            }
            break;
        case "Android":
            if (OperatingSystem.IsWindows())
            {
                setup = serviceProvider.GetRequiredService<AndroidSdkSetup>();
            }
            break;
        case "Windows":
            if (OperatingSystem.IsWindows())
            {
                setup = serviceProvider.GetRequiredService<WindowsSdkSetup>();
            }
            break;
        default:
            throw new NotSupportedException();
    }

    if (setup == null)
    {
        Console.Error.WriteLine("error: the specified target platform can not be installed on this platform.");
        Environment.ExitCode = 1;
        return;
    }

    var packageId = $"{platform}-{await setup.ComputeSdkPackageId(enginePath.FullName, context.GetCancellationToken())}";
    var packageTargetPath = Path.Combine(packagePath.FullName, packageId);
    var packageWorkingPath = Path.Combine(packagePath.FullName, $"{packageId}-tmp-{Process.GetCurrentProcess().Id}");
    var packageOldPath = Path.Combine(packagePath.FullName, $"{packageId}-old-{Process.GetCurrentProcess().Id}");
    if (!Directory.Exists(packageTargetPath))
    {
        if (Directory.Exists(packageWorkingPath))
        {
            Directory.Delete(packageWorkingPath, true);
        }
        if (Directory.Exists(packageOldPath))
        {
            Directory.Delete(packageOldPath, true);
        }
        Directory.CreateDirectory(packageWorkingPath);
        await setup.GenerateSdkPackage(enginePath.FullName, packageWorkingPath, context.GetCancellationToken());
        try
        {
            if (Directory.Exists(packageTargetPath))
            {
                Directory.Move(packageTargetPath, packageOldPath);
            }
            Directory.Move(packageWorkingPath, packageTargetPath);
        }
        catch
        {
            if (!Directory.Exists(packageTargetPath) &&
                Directory.Exists(packageOldPath))
            {
                Directory.Move(packageOldPath, packageTargetPath);
            }
        }
        finally
        {
            if (Directory.Exists(packageOldPath))
            {
                Directory.Delete(packageOldPath);
            }
        }
    }
    var env = await setup.EnsureSdkPackage(packageTargetPath, context.GetCancellationToken());

    Console.WriteLine("The following environment variables would be set:");
    foreach (var kv in env.EnvironmentVariables)
    {
        Console.WriteLine($"  {kv.Key} = {kv.Value}");
    }
    Environment.ExitCode = 0;
    return;
});

return await rootCommand.InvokeAsync(args);