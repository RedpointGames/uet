namespace UET.Commands.Internal.OpenGEPreprocessorCache
{
    using System.CommandLine;

    internal class OpenGEPreprocessorCacheClientCommand
    {
        public static Command CreateOpenGEPreprocessorCacheClientCommand()
        {
            var command = new Command("openge-preprocessor-cache-client");
            command.AddCommand(OpenGEPreprocessorCacheClientUnresolvedCommand.CreateUnresolvedCommand());
            command.AddCommand(OpenGEPreprocessorCacheClientResolvedCommand.CreateResolvedCommand());
            return command;
        }
    }
}
