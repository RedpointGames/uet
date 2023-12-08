namespace Redpoint.Uet.BuildPipeline.BuildGraph
{
    public class BuildGraphScriptSpecification
    {
        internal bool _forPlugin { get; private set; }
        internal bool _forProject { get; private set; }
        internal bool _forEngine { get; private set; }

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

        public static BuildGraphScriptSpecification ForEngine()
        {
            return new BuildGraphScriptSpecification { _forEngine = true };
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
                return $"engine";
            }
        }

        public static BuildGraphScriptSpecification FromReparsableString(string input)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (input == "plugin")
            {
                return ForPlugin();
            }
            else if (input == "project")
            {
                return ForProject();
            }
            else if (input.StartsWith("engine", StringComparison.Ordinal))
            {
                return ForEngine();
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
