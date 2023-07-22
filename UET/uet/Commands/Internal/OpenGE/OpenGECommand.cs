namespace UET.Commands.Internal.OpenGE
{
    using Redpoint.OpenGE;
    using Redpoint.ProcessExecution;
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

            public Options()
            {
                TaskFile = new Option<FileInfo>("--task-file") { IsRequired = true };
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

            public Task<int> ExecuteAsync(InvocationContext context)
            {
                var taskFile = context.ParseResult.GetValueForOption(_options.TaskFile)!;

                return _processExecutor.ExecuteAsync(
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
                            "/Title=\"UET OpenGE\""
                        },
                        DisableOpenGE = false,
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());
            }
        }
    }
}
