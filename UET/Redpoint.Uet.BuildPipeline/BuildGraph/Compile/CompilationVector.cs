namespace Redpoint.Uet.BuildPipeline.BuildGraph.Compile
{
    using System.Collections.Generic;

    public class CompilationVector
    {
        /// <summary>
        /// The platform to build (supports variants like 'GooglePlay' and 'MetaQuest').
        /// </summary>
        public required IReadOnlyList<string> Platforms { get; set; } = [];

        /// <summary>
        /// The name and type of the targets to build. The target type should be 'Editor', 'Game', 'Client' or 'Server'.
        /// </summary>
        public required IReadOnlyList<CompilationVectorTarget> Targets { get; set; } = [];

        /// <summary>
        /// The configuration to build.
        /// </summary>
        public required IReadOnlyList<string> Configurations { get; set; } = [];

        /// <summary>
        /// Additional command line arguments for the compilation.
        /// </summary>
        public required IReadOnlyList<string> Arguments { get; set; } = [];

        /// <summary>
        /// The prefix to apply to the output tags, in this format:
        /// - #{PREFIX}_$(Target)_$(Platform)_$(Configuration)
        /// </summary>
        public required string TagPrefix { get; set; } = string.Empty;
    }
}
