namespace Redpoint.Uet.BuildPipeline.BuildGraph.Patching
{
    using System.Threading.Tasks;

    public interface IBuildGraphPatcher
    {
        Task PatchBuildGraphAsync(string enginePath, bool isEngineBuild);
    }
}
