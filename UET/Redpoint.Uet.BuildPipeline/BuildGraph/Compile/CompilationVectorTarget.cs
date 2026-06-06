namespace Redpoint.Uet.BuildPipeline.BuildGraph.Compile
{
    public readonly record struct CompilationVectorTarget(string TargetName, string TargetType, string? ConditionalIf = null);
}
