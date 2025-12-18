namespace UET.Commands.EngineSpec
{
    public enum EngineSpecType
    {
        Path,

        Version,

        UEFSPackageTag,

        GitCommit,

        /// <summary>
        /// The engine itself is being built. This can never be returned by <see cref="EngineSpec.TryParseEngineSpecExact"/>, 
        /// and is only ever returned by <see cref="EngineSpec.ParseEngineSpec(System.CommandLine.Option{PathSpec}, System.CommandLine.Option{DistributionSpec?}?)"/> when an engine distribution is supplied.
        /// 
        /// Commands like <c>ci-build</c> will always receive a value like <see cref="GitCommit"/> or <see cref="CurrentWorkspace"/> instead.
        /// </summary>
        SelfEngineByBuildConfig,

        SESNetworkShare,

        RemoteZfs,

        /// <summary>
        /// The engine is in the same workspace as the "project" (i.e. the engine is what is being built). This is the engine spec type when an engine is being built without an external source repository specified.
        /// </summary>
        CurrentWorkspace,
    }
}
