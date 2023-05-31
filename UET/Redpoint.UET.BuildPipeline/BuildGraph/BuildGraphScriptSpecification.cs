namespace Redpoint.UET.BuildPipeline.BuildGraph
{
    public class BuildGraphScriptSpecification
    {
        internal string? _path { get; private set; }
        internal bool _forPlugin { get; private set; }
        internal bool _forProject { get; private set; }

        private BuildGraphScriptSpecification()
        {
        }

        public static BuildGraphScriptSpecification ForPlugin()
        {
            return new BuildGraphScriptSpecification { _forPlugin = true };
        }

        public static BuildGraphScriptSpecification ForProject()
        {
            return new BuildGraphScriptSpecification { _forProject = true };
        }

        public static BuildGraphScriptSpecification ForPath(string buildGraphScriptPath)
        {
            return new BuildGraphScriptSpecification { _path = buildGraphScriptPath };
        }

        public string ToReparsableString()
        {
            if (_forPlugin)
            {
                return "plugin";
            }
            else if (_forProject)
            {
                return "project";
            }
            else
            {
                return $"path:{_path}";
            }
        }

        public static BuildGraphScriptSpecification FromReparsableString(string input)
        {
            if (input == "plugin")
            {
                return ForPlugin();
            }
            else if (input == "project")
            {
                return ForProject();
            }
            else if (input.StartsWith("path:"))
            {
                return ForPath(input.Substring("path:".Length));
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
