namespace Redpoint.Uet.BuildPipeline.BuildGraph.Compile
{
    using Redpoint.Uet.Configuration.Dynamic;
    using System.Collections.Generic;
    using System.Xml;

    public interface IBuildGraphCompileGraphNodesGenerator
    {
        Task WriteBuildGraphNodesToCompileAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            CompilationContext compilation,
            IReadOnlyList<CompilationVector> vectors);
    }
}
