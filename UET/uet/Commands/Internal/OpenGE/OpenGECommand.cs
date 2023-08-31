namespace UET.Commands.Internal.OpenGE
{
    using Redpoint.OpenGE;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.OpenGE;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal class OpenGECommand
    {
        internal class Options
        {
            public Option<FileInfo> TaskFile;
            public Option<bool> Forever;

            public Options()
            {
                TaskFile = new Option<FileInfo>("--task-file") { IsRequired = true };
                Forever = new Option<bool>("--forever") { IsHidden = true };
            }
        }

        public static Command CreateOpenGECommand()
        {
            var options = new Options();
            var command = new Command("openge");
            command.AddAllOptions(options);
            command.AddCommonHandler<OpenGECommandInstance>(options);
            return command;
        }

        private class OpenGECommandInstance : ICommandInstance
        {
            private readonly IProcessExecutor _processExecutor;
            private readonly Options _options;

            public OpenGECommandInstance(
                IProcessExecutor processExecutor,
                Options options)
            {
                _processExecutor = processExecutor;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var taskFile = context.ParseResult.GetValueForOption(_options.TaskFile)!;
                var forever = context.ParseResult.GetValueForOption(_options.Forever);

                int exitCode;
                do
                {
                    try
                    {
                        exitCode = await _processExecutor.ExecuteAsync(
                            new OpenGEProcessSpecification
                            {
                                FilePath = "__openge__",
                                Arguments = new[]
                                {
                                taskFile.FullName,
                                "/Rebuild",
                                "/NoLogo",
                                "/ShowAgent",
                                "/ShowTime",
                                "/Title=UET OpenGE"
                                },
                                DisableOpenGE = false,
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken());
                    }
                    catch (OperationCanceledException) when (context.GetCancellationToken().IsCancellationRequested)
                    {
                        return 1;
                    }
                } while (forever && !context.GetCancellationToken().IsCancellationRequested);
                return exitCode;
            }
        }
    }
}
