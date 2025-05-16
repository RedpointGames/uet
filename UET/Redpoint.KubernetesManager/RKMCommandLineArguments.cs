namespace Redpoint.KubernetesManager
{
    internal class RKMCommandLineArguments
    {
        public string[] Arguments { get; }

        public RKMCommandLineArguments(string[] arguments)
        {
            Arguments = arguments;
        }
    }
}
