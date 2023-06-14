namespace Docker.Registry.DotNet.Authentication
{
    internal class ParsedAuthentication
    {
        public string Realm { get; }

        public string Service { get; }

        public string Scope { get; }

        public ParsedAuthentication(string realm, string service, string scope)
        {
            Realm = realm;
            Service = service;
            Scope = scope;
        }
    }
}