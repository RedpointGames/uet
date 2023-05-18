namespace Redpoint.UET.Workspace.Credential
{
    using System.Linq;
    using System.Threading.Tasks;
    using Uefs;

    internal interface ICredentialManager
    {
        GitCredential GetGitCredentialForRepositoryUrl(string repositoryUrl);

        RegistryCredential GetRegistryCredentialForTag(string tag);
    }
}
