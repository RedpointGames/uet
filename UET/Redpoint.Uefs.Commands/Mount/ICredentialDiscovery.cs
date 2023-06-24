namespace Redpoint.Uefs.Commands.Mount
{
    internal interface ICredentialDiscovery
    {
        Protocol.RegistryCredential GetRegistryCredential(string packageTag);

        Protocol.GitCredential GetGitCredential(string gitUrl);
    }
}
