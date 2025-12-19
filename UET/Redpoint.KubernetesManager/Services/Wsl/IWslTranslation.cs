namespace Redpoint.KubernetesManager.Services.Wsl
{
    using System.Net;

    internal interface IWslTranslation
    {
        string TranslatePath(string path);

        Task<IPAddress> GetTranslatedIPAddress(CancellationToken cancellationToken);

        string GetTranslatedControllerHostname();
    }
}
