namespace Redpoint.Uet.BuildPipeline.BuildGraph.Compile
{
    public readonly record struct CompilationProductionResult
    {
        /// <summary>
        /// The binaries tag associated with this production.
        /// </summary>
        public required string BinariesTag { get; init; }

        /// <summary>
        /// The receipts tag associated with this production.
        /// </summary>
        public required string ReceiptsTag { get; init; }

        /// <summary>
        /// The expression which evaluates to the target type from the vector at runtime.
        /// </summary>
        public required string TargetTypeExpression { get; init; }

        /// <summary>
        /// The expression which evaluates to the target name from the vector at runtime.
        /// </summary>
        public required string TargetNameExpression { get; init; }

        /// <summary>
        /// The expression which evaluates to the target platform from the vector at runtime.
        /// </summary>
        public required string TargetPlatformExpression { get; init; }

        /// <summary>
        /// The expression which evaluates to the target configuration from the vector at runtime.
        /// </summary>
        public required string TargetConfigurationExpression { get; init; }

        /// <summary>
        /// The tag prefix from the vector.
        /// </summary>
        public required string TagPrefix { get; init; }

        /// <summary>
        /// The host platform.
        /// </summary>
        public required string HostPlatform { get; init; }
    }
}
