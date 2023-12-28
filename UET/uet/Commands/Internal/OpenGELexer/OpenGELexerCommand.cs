namespace UET.Commands.Internal.OpenGELexer
{
    using Redpoint.CppPreprocessor.Lexing;
    using Redpoint.Lexer;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
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

            public OpenGELexerCommandInstance(
                Options options)
            {
                _options = options;
            }

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                // @note: Before we can use MemoryMappedFile, we need to change the lexer to use ReadOnlySpan<byte> and Rune to read files as UTF-8 instead of assuming UTF-16 encoding.

                using var reader = new StreamReader(
                    context.ParseResult.GetValueForOption(_options.SourceFile)!.FullName);
                var content = reader.ReadToEnd();

                var original = content.AsSpan();
                Console.WriteLine($"lexing span of {original.Length} length");
                Console.WriteLine(original.ToString());
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

                        Console.WriteLine($"(line {line}) #{directive} {arguments}");
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
                while (true);
                return Task.FromResult(0);

                /*
                using var file = MemoryMappedFile.CreateFromFile(context.ParseResult.GetValueForOption(_options.SourceFile)!.FullName, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                using var accessor = file.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                byte* mappedStart = null;
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref mappedStart);
                try
                {
                    
                }
                finally
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
                */
            }
        }
    }
}
