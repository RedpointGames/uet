namespace Redpoint.CloudFramework.CLI
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using System.CommandLine;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class GenerateStronglyTypedLanguageFiles
    {
        internal class Options
        {
            public Option<FileInfo> JsonPath = new Option<FileInfo>(
                "--json",
                "The path to the JSON file that contains the language keys.");

            public Option<string> Namespace = new Option<string>(
                "--namespace",
                "The namespace to put C# classes in.");

            public Option<FileInfo> CsPath = new Option<FileInfo>(
                "--cs-path",
                "The path to emit the C# code.");

            public Option<FileInfo> TsPath = new Option<FileInfo>(
                "--ts-path",
                "The path to emit the TypeScript code.");
        }

        public static Command CreateCommand(ICommandBuilder builder)
        {
            return new Command("generate-strongly-typed-language", "Generates files for a strongly-typed language system.");
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

            private static readonly Regex _braceRegex = new Regex("\\{[0-9]\\}");
            private static readonly Regex _htmlRegex = new Regex("\\<[a-z]+\\>");

            public Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var jsonPath = context.ParseResult.GetValueForOption(_options.JsonPath)!;
                var ns = context.ParseResult.GetValueForOption(_options.Namespace)!;
                var csPath = context.ParseResult.GetValueForOption(_options.CsPath)!;
                var tsPath = context.ParseResult.GetValueForOption(_options.TsPath)!;

                var languageDict = JsonSerializer.Deserialize(File.ReadAllText(jsonPath.FullName), LanguageJsonSerializerContext.Default.LanguageDictionary)!;
                if (!languageDict.IsSorted())
                {
                    _logger.LogWarning("Automatically sorting language dictionary keys...");
                    languageDict.SortKeys();
                    File.WriteAllText(
                        jsonPath.FullName,
                        JsonSerializer.Serialize(
                            languageDict,
                            new LanguageJsonSerializerContext(new JsonSerializerOptions
                            {
                                WriteIndented = true,
                            }).LanguageDictionary));
                }

                _logger.LogInformation($"Generating {languageDict.Count} entries for strongly-typed language files...");

                using (var writer = new StreamWriter(csPath.FullName))
                {
                    writer.WriteLine(
                        $$"""
                        namespace {{ns}};

                        using Microsoft.AspNetCore.Html;
                        
                        #pragma warning disable CA1707

                        public interface IHtmlLanguageService
                        {
                        """);
                    foreach (var kv in languageDict)
                    {
                        if (_braceRegex.IsMatch(kv.Value))
                        {
                            writer.WriteLine(
                                $$"""
                                    IHtmlContent {{kv.Key}}(params object[] arguments);
                                """);
                        }
                        else
                        {
                            writer.WriteLine(
                                $$"""
                                    IHtmlContent {{kv.Key}} { get; }
                                """);
                        }
                    }
                    writer.WriteLine(
                        $$"""
                        }

                        public interface ITextLanguageService
                        {
                        """);
                    foreach (var kv in languageDict)
                    {
                        if (_braceRegex.IsMatch(kv.Value))
                        {
                            writer.WriteLine(
                                $$"""
                                    string {{kv.Key}}(params object[] arguments);
                                """);
                        }
                        else
                        {
                            writer.WriteLine(
                                $$"""
                                    string {{kv.Key}} { get; }
                                """);
                        }
                    }
                    writer.WriteLine(
                        $$"""
                        }
                        
                        internal class DefaultLanguageService : IHtmlLanguageService, ITextLanguageService
                        {
                            private readonly ITranslationService _translationService;

                            public DefaultLanguageService(ITranslationService translationService)
                            {
                                _translationService = translationService;
                            }

                        """);
                    foreach (var kv in languageDict)
                    {
                        if (_braceRegex.IsMatch(kv.Value))
                        {
                            writer.WriteLine(
                                $$"""
                                    string ITextLanguageService.{{kv.Key}}(params object[] arguments)
                                    {
                                        return _translationService.TX("{{kv.Key}}", arguments);
                                    }

                                    IHtmlContent IHtmlLanguageService.{{kv.Key}}(params object[] arguments)
                                    {
                                        return _translationService.T("{{kv.Key}}", arguments);
                                    }
                                """);
                        }
                        else
                        {
                            writer.WriteLine(
                                $$"""
                                    string ITextLanguageService.{{kv.Key}} => _translationService.TX("{{kv.Key}}");
                                    IHtmlContent IHtmlLanguageService.{{kv.Key}} => _translationService.T("{{kv.Key}}");
                                """);
                        }
                    }
                    writer.WriteLine(
                        $$"""
                        }
                        
                        public static class TK
                        {
                        """);
                    foreach (var kv in languageDict)
                    {
                        writer.WriteLine(
                            $$"""
                                public const string {{kv.Key}} = "{{kv.Key}}";
                            """);
                    }
                    writer.WriteLine(
                        $$"""
                        }
                        
                        #pragma warning restore CA1707
                        """);
                }

                using (var writer = new StreamWriter(tsPath.FullName))
                {
                    // @note: Keys that use HTML in their values are not exposed to TypeScript, as they won't
                    // work correctly in React.
                    var nonHtmlLanguageDict = new Dictionary<string, string>();
                    foreach (var kv in languageDict.Where(x => !_htmlRegex.IsMatch(x.Value)))
                    {
                        nonHtmlLanguageDict.Add(kv.Key, kv.Value);
                    }
                    if (nonHtmlLanguageDict.Count != languageDict.Count)
                    {
                        _logger.LogWarning($"{languageDict.Count - nonHtmlLanguageDict.Count} entries were excluded from TypeScript because their values contain HTML. HTML support for language values is deprecated.");
                    }

                    var nonParameterKeys = nonHtmlLanguageDict.Keys.Where(x => !_braceRegex.IsMatch(languageDict[x]));
                    var parameterKeys = nonHtmlLanguageDict.Keys.Where(x => _braceRegex.IsMatch(languageDict[x]));

                    writer.WriteLine(
                        $$"""
                        export type NonParameterizedLanguageKey = {{string.Join(" |\n  ", nonParameterKeys.Select(x => $"\"{x}\""))}};
                        export type ParameterizedLanguageKey = {{string.Join(" |\n  ", parameterKeys.Select(x => $"\"{x}\""))}};
                        export const TK: (NonParameterizedLanguageKey | ParameterizedLanguageKey)[] = [{{string.Join(",\n  ", nonHtmlLanguageDict.Keys.Select(x => $"\"{x}\""))}}];
                        """);
                    foreach (var key in nonParameterKeys)
                    {
                        writer.WriteLine($"export const {key}: NonParameterizedLanguageKey = \"{key}\";");
                    }
                    foreach (var key in parameterKeys)
                    {
                        writer.WriteLine($"export const {key}: ParameterizedLanguageKey = \"{key}\";");
                    }
                }

                return Task.FromResult(0);
            }
        }
    }
}
