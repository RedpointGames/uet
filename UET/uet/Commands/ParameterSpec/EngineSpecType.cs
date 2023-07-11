namespace UET.Commands.EngineSpec
{
    internal enum EngineSpecType
    {
        Path,

        Version,

        UEFSPackageTag,

        GitCommit,

        /// <summary>
        /// The engine itself is being built.
        /// </summary>
        SelfEngine,
    }
}
