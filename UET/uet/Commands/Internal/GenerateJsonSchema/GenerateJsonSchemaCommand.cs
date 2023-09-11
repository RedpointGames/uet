namespace UET.Commands.Internal.GenerateJsonSchema
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class GenerateJsonSchemaCommand
    {
        public sealed class Options
        {
            public Option<FileInfo?> OutputPath = new Option<FileInfo?>("--output-path");
        }

        public static Command CreateGenerateJsonSchemaCommand()
        {
            var options = new Options();
            var command = new Command("generate-json-schema");
            command.AddAllOptions(options);
            command.AddCommonHandler<GenerateJsonSchemaCommandInstance>(options, services =>
            {
                services.AddSingleton<IJsonSchemaGenerator, DefaultJsonSchemaGenerator>();
            });
            return command;
        }

        private sealed class GenerateJsonSchemaCommandInstance : ICommandInstance
        {
            private readonly ILogger<GenerateJsonSchemaCommandInstance> _logger;
            private readonly IJsonSchemaGenerator _jsonSchemaGenerator;
            private readonly Options _options;

            public GenerateJsonSchemaCommandInstance(
                ILogger<GenerateJsonSchemaCommandInstance> logger,
                IJsonSchemaGenerator jsonSchemaGenerator,
                Options options)
            {
                _logger = logger;
                _jsonSchemaGenerator = jsonSchemaGenerator;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var outputPath = context.ParseResult.GetValueForOption(_options.OutputPath)!;
                if (outputPath?.DirectoryName != null)
                {
                    Directory.CreateDirectory(outputPath.DirectoryName);
                }

                if (outputPath != null)
                {
                    using (var stream = new FileStream(outputPath.FullName, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    {
                        await _jsonSchemaGenerator.GenerateAsync(stream).ConfigureAwait(false);
                    }
                    _logger.LogInformation($"Successfully emitted JSON schema for BuildConfig.json to: {outputPath.FullName}");
                }
                else
                {
                    using (var stream = new MemoryStream())
                    {
                        await _jsonSchemaGenerator.GenerateAsync(stream).ConfigureAwait(false);
                        var bytes = new byte[stream.Position];
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.Read(bytes);
                        Console.WriteLine(Encoding.UTF8.GetString(bytes));
                    }
                }

                return 0;
            }
        }
    }
}
