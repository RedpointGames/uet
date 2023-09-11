namespace Redpoint.Uefs.Daemon.Integration.Docker
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class EndpointAttribute : Attribute
    {
        public EndpointAttribute(string url)
        {
            Url = url;
        }

        public string Url { get; }
    }
}
