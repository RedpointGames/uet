namespace Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Msvc
{
    using Redpoint.OpenGE.Protocol;

    internal record class MsvcParsedResponseFile
    {
        public required string ResponseFilePath { get; init; }
        public required bool IsCreatingPch { get; init; }
        public required FileInfo? PchCacheFile { get; init; }
        public required PreprocessorResolutionResultWithTimingMetadata DependentFiles { get; init; }
        public required FileInfo InputFile { get; init; }
        public required FileInfo OutputFile { get; init; }
        public required FileInfo? SourceDependencies { get; init; }
        public required FileInfo? ClangDepfile { get; init; }
        public required FileInfo? PchOriginalHeaderFile { get; init; }
    }
}
