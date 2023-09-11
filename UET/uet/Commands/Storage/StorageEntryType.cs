namespace UET.Commands.Storage
{
    internal enum StorageEntryType
    {
        Generic,
        WriteScratchLayer,
        ExtractedConsoleZip,
        UefsGitSharedBlobs,
        UefsGitSharedDependencies,
        UefsGitSharedIndexCache,
        UefsGitSharedRepository,
        UefsHostPackagesCache,
    }
}
