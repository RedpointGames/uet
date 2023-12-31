﻿namespace Redpoint.Uet.Workspace.Credential
{
    using Redpoint.Uefs.Protocol;

    internal interface ICredentialManager
    {
        GitCredential GetGitCredentialForRepositoryUrl(string repositoryUrl);

        RegistryCredential GetRegistryCredentialForTag(string tag);
    }
}
