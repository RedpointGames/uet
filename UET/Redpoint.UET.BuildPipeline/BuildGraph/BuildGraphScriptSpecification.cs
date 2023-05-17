namespace Redpoint.UET.BuildPipeline.BuildGraph
{
    internal class BuildGraphScriptSpecification
    {
        internal string? _path { get; private set; }
        internal bool _forPlugin { get; private set; }
        internal bool _forProject { get; private set; }

        private BuildGraphScriptSpecification()
        {
        }

        internal static BuildGraphScriptSpecification ForPlugin()
        {
            return new BuildGraphScriptSpecification { _forPlugin = true };
        }

        internal static BuildGraphScriptSpecification ForProject()
        {
            return new BuildGraphScriptSpecification { _forProject = true };
        }

        internal static BuildGraphScriptSpecification ForPath(string buildGraphScriptPath)
        {
            return new BuildGraphScriptSpecification { _path = buildGraphScriptPath };
        }
    }
}
