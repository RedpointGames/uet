namespace Redpoint.Uefs.Commands
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.ProgressMonitor;
    using Redpoint.Uefs.Commands.Hash;
    using Redpoint.Uefs.Commands.Mount;
    using Redpoint.Uefs.Package;
    using Redpoint.Uefs.Package.SparseImage;
    using Redpoint.Uefs.Package.Vhd;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Logging.SingleLine;
    using System;
    using System.CommandLine;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using Redpoint.ProcessTree;
    using Redpoint.CredentialDiscovery;

    internal static class CommandExtensions
    {
        internal static void AddAllOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TOptions>(this Command command, TOptions options)
        {
            foreach (var option in typeof(TOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.PropertyType.IsAssignableTo(typeof(Option)))
                .Select(x => (Option)x.GetValue(options)!))
            {
                command.AddOption(option);
            }

            foreach (var option in typeof(TOptions).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.FieldType.IsAssignableTo(typeof(Option)))
                .Select(x => (Option)x.GetValue(options)!))
            {
                command.AddOption(option);
            }
        }

        internal static void AddCommonHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(this Command command, object options, Action<IServiceCollection>? extraServices = null) where TCommand : class, ICommandInstance
        {
            command.SetHandler(async (context) =>
            {
                var services = new ServiceCollection();
                services.AddLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Information);
                    logging.AddSingleLineConsoleFormatter();
                    logging.AddSingleLineConsole();
                });
                services.AddSingleton(sp => context);
                services.AddSingleton(options.GetType(), sp => options);
                services.AddSingleton<TCommand>();
                services.AddProgressMonitor();
                if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
                {
                    services.AddProcessTree();
                }
                services.AddUefs();
                services.AddGrpcPipes();
                services.AddUefsPackage();
                services.AddUefsPackageVhd();
                services.AddUefsPackageSparseImage();
                services.AddCredentialDiscovery();
                services.AddSingleton<IFileHasher, DefaultFileHasher>();

                var sp = services.BuildServiceProvider();
                var instance = sp.GetRequiredService<TCommand>();

                context.ExitCode = await instance.ExecuteAsync(context).ConfigureAwait(false);
            });
        }
    }
}
