namespace UET.Commands.Internal.OpenGELexer
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CppPreprocessor.Lexing;
    using Redpoint.Lexer;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Diagnostics;
    using System.IO.MemoryMappedFiles;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class OpenGELexerCommand
    {
        internal sealed class Options
        {
            public Option<FileInfo> SourceFile = new Option<FileInfo>("--file") { IsRequired = true };
        }

        public static Command CreateOpenGELexerCommand()
        {
            var options = new Options();
            var command = new Command("openge-lexer");
            command.AddAllOptions(options);
            command.AddCommonHandler<OpenGELexerCommandInstance>(options);
            return command;
        }

        private sealed class OpenGELexerCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<OpenGELexerCommandInstance> _logger;

            public OpenGELexerCommandInstance(
                Options options,
                ILogger<OpenGELexerCommandInstance> logger)
            {
                _options = options;
                _logger = logger;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                var stopwatch = Stopwatch.StartNew();

                _logger.LogInformation($"Start: {stopwatch}");

                using var reader = new StreamReader(
                    context.ParseResult.GetValueForOption(_options.SourceFile)!.FullName);
                var content = reader.ReadToEnd();

                _logger.LogInformation($"File read: {stopwatch}");

                var original = content.AsSpan();
                var range = original;
                LexerCursor cursor = default;
                var line = 0;
                do
                {
                    var result = LexingHelpers.GetNextDirective(
                        ref range,
                        in original,
                        ref cursor);
                    if (result.Found)
                    {
                        line += cursor.NewlinesConsumed;
                        var directive = original.Slice(result.Directive.Start, result.Directive.Length);
                        var arguments = result.Arguments.Length > 0 ? original.Slice(result.Arguments.Start, result.Arguments.Length) : default;

                        //_logger.LogInformation($"(line {line}) #{directive} {arguments}");
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
                while (true);

                _logger.LogInformation($"File lexed: {stopwatch}");

                return Task.FromResult(0);


            }
        }
    }
}
