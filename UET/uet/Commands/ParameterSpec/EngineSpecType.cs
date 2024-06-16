namespace UET.Commands.EngineSpec
{
    internal enum EngineSpecType
    {
        Path,

        Version,

        UEFSPackageTag,

        GitCommit,

        /// <summary>
        /// The engine itself is being built. This can never be returned by <see cref="EngineSpec.TryParseEngineSpecExact"/>, 
        /// and is only ever returned by <see cref="EngineSpec.ParseEngineSpec(System.CommandLine.Option{PathSpec}, System.CommandLine.Option{DistributionSpec?}?)"/> when an engine distribution is supplied.
        /// 
        /// Commands like <c>ci-build</c> will always receive a value like <see cref="GitCommit"/> instead.
        /// </summary>
        SelfEngineByBuildConfig,

        SESNetworkShare,
    }
}
