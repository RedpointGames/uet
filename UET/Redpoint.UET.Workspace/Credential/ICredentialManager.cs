namespace Redpoint.UET.Workspace.Credential
{
    using Uefs;

    internal interface ICredentialManager
    {
        GitCredential GetGitCredentialForRepositoryUrl(string repositoryUrl);

        RegistryCredential GetRegistryCredentialForTag(string tag);
    }
}
