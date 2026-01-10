namespace Redpoint.Tpm
{
    using System.Security.Cryptography.X509Certificates;

    internal class DefaultTpmSecuredHttpClientFactory : ITpmSecuredHttpClientFactory
    {
        private readonly X509Certificate2 _certificateAuthority;
        private readonly X509Certificate2 _clientCertificate;
        private readonly string _aikPublicPem;

        public DefaultTpmSecuredHttpClientFactory(
            X509Certificate2 certificateAuthority,
            X509Certificate2 clientCertificate,
            string aikPublicPem)
        {
            _certificateAuthority = certificateAuthority;
            _clientCertificate = clientCertificate;
            _aikPublicPem = aikPublicPem;
        }

        public HttpClient Create()
        {
            return Create(new HttpClientHandler());
        }

        public HttpClient Create(HttpClientHandler handler)
        {
            handler.ClientCertificates.Add(DefaultTpmSecuredHttp.ReexportForWindows(_clientCertificate));
            handler.ServerCertificateCustomValidationCallback = (request, serverCertificate, _, policyErrors) =>
            {
                if (serverCertificate == null)
                {
                    return false;
                }

                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(_certificateAuthority);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                return chain.Build(serverCertificate);
            };
            handler.CheckCertificateRevocationList = true;

            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add(
                "RKM-AIK-PEM",
                _aikPublicPem.Replace('\n', '|'));
            return client;
        }
    }
}
