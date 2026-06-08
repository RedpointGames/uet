namespace Redpoint.Uet.BuildPipeline.BuildGraph.Compile
{
    using Redpoint.Uet.Configuration.Dynamic;
    using System.Collections.Generic;
    using System.Xml;

    public delegate Task<string?> GetProductionCondition(CompilationProductionResult productionResult);

    public delegate Task ActOnProductionTag(IBuildGraphEmitContext context, XmlWriter writer, CompilationProductionResult productionResult);

    public class CompilationContext
    {
        /// <summary>
        /// The unique name for these compile steps in the BuildGraph.
        /// </summary>
        public required string UniqueName { get; set; }

        /// <summary>
        /// The project path under which stale precompiled headers should be removed. If not set, stale precompiled headers won't be checked for.
        /// </summary>
        public required string? ProjectPath { get; set; }

        /// <summary>
        /// If set, the path for which binary files will have their debug symbols stripped.
        /// </summary>
        public required string? StripPath { get; set; }

        /// <summary>
        /// If set, the name of the variable that contains the "dynamic-before-compile" macros to run before compilation.
        /// </summary>
        public required string? RunDynamicBeforeCompileMacrosVariable { get; set; }

        /// <summary>
        /// The list of requirements for the compilation target to run (typically the host project being generated for plugins).
        /// </summary>
        public required IReadOnlyList<string> Requires { get; set; }

        /// <summary>
        /// Returns an optional expression that is the condition for this compilation vector to be included.
        /// </summary>
        public required GetProductionCondition? ProductionCondition { get; set; }

        /// <summary>
        /// A callback to emit BuildGraph XML handling the build artifacts from the compilation. This is the callback to use when you're trying to
        /// get tags from the compile back into the rest of the BuildGraph.
        /// </summary>
        public required ActOnProductionTag ActOnProductionTag { get; set; }

        /// <summary>
        /// If set, this variable will have the name of nodes appended to it.
        /// </summary>
        public required string? BuildTasksVariable { get; set; }
    }
}
