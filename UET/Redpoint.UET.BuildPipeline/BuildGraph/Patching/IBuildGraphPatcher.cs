namespace Redpoint.UET.BuildPipeline.BuildGraph.Patching
{
    using System.Threading.Tasks;

    internal interface IBuildGraphPatcher
    {
        Task PatchBuildGraphAsync(string enginePath);
    }
}
