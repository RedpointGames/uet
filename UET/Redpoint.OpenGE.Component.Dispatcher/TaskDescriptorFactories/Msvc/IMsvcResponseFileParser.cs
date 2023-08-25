namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Msvc
{
    using Redpoint.OpenGE.Protocol;

    internal interface IMsvcResponseFileParser
    {
        Task<MsvcParsedResponseFile?> ParseResponseFileAsync(
            string responseFilePath,
            string workingDirectory,
            bool guaranteedToExecuteLocally,
            long buildStartTicks,
            CompilerArchitype architype,
            CancellationToken cancellationToken);
    }
}
