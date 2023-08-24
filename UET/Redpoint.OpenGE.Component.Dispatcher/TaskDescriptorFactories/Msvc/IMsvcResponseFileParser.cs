namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Msvc
{
    internal interface IMsvcResponseFileParser
    {
        Task<MsvcParsedResponseFile?> ParseResponseFileAsync(
            string responseFilePath,
            string workingDirectory,
            bool guaranteedToExecuteLocally,
            long buildStartTicks,
            IReadOnlyDictionary<string, string>? extraGlobalDefinitions,
            CancellationToken cancellationToken);
    }
}
