namespace UET.Commands.Internal.UpdateCopyrightHeadersForMarketplace
{
    using Grpc.Core.Logging;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    internal class UpdateCopyrightHeadersForMarketplaceCommand
    {
        internal class Options
        {
            public Option<string> Path;
            public Option<string> CopyrightHeader;
            public Option<string> CopyrightExcludes;

            public Options()
            {
                Path = new Option<string>("--path");
                CopyrightHeader = new Option<string>("--copyright-header");
                CopyrightExcludes = new Option<string>("--copyright-excludes");
            }
        }

        public static Command CreateUpdateCopyrightHeadersForMarketplaceCommand()
        {
            var options = new Options();
            var command = new Command("update-copyright-headers-for-marketplace");
            command.AddAllOptions(options);
            command.AddCommonHandler<UpdateCopyrightHeadersForMarketplaceCommandInstance>(options);
            return command;
        }

        private class UpdateCopyrightHeadersForMarketplaceCommandInstance : ICommandInstance
        {
            private readonly ILogger<UpdateCopyrightHeadersForMarketplaceCommandInstance> _logger;
            private readonly Options _options;

            public UpdateCopyrightHeadersForMarketplaceCommandInstance(
                ILogger<UpdateCopyrightHeadersForMarketplaceCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var path = context.ParseResult.GetValueForOption(_options.Path)!;
                var copyrightHeader = context.ParseResult.GetValueForOption(_options.CopyrightHeader)!;
                var copyrightExcludes = context.ParseResult.GetValueForOption(_options.CopyrightExcludes)!.Split(";").Select(x =>
                {
                    return Path.Combine(path, x).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                });

                bool IsFileExcluded(string targetPath)
                {
                    foreach (var excludePath in copyrightExcludes)
                    {
                        if (targetPath.StartsWith(excludePath))
                        {
                            return true;
                        }
                    }
                    return false;
                }

                foreach (var file in new DirectoryInfo(path)
                    .GetFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = true })
                    .Where(x =>
                        x != null &&
                        (
                            x.Name.EndsWith(".h") ||
                            x.Name.EndsWith(".hpp") ||
                            x.Name.EndsWith(".cs") ||
                            x.Name.EndsWith(".c") ||
                            x.Name.EndsWith(".cpp")
                        ) &&
                        !(
                            x.FullName.Substring(path.Length).Contains("Binaries") ||
                            x.FullName.Substring(path.Length).Contains("Intermediate") ||
                            x.FullName.Substring(path.Length).Contains("BuildScripts") ||
                            x.FullName.Substring(path.Length).Contains("Plugins") ||
                            IsFileExcluded(x.FullName)
                        )))
                {
                    _logger.LogInformation($"Updating {file.Name}...");

                    var content = (await File.ReadAllLinesAsync(file.FullName)).ToList();
                    while (content.Count > 0 &&
                        (content[0].StartsWith("// ") || string.IsNullOrWhiteSpace(content[0])))
                    {
                        content.RemoveAt(0);
                    }
                    content.Insert(0, "");
                    content.Insert(0, $"// {copyrightHeader}");
                    while (content.Count > 0 && string.IsNullOrWhiteSpace(content[content.Count - 1]))
                    {
                        content.RemoveAt(content.Count - 1);
                    }
                    for (int i = 0; i < content.Count; i++)
                    {
                        content[i].TrimEnd();
                    }
                    var joinedContent = string.Join("\n", content);

                    try
                    {
                        await File.WriteAllTextAsync(file.FullName, joinedContent);
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                return 0;
            }
        }
    }
}
