namespace Redpoint.Uet.BuildPipeline.BuildGraph.Patching
{
    using Redpoint.PathResolution;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public interface IDotnetLocator
    {
        Task<string?> TryLocateDotNetWithinEngine(string enginePath);
    }
}
