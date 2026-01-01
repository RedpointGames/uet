namespace Redpoint.Tpm.Negotiate
{
    using System.Text.Json.Serialization;

    internal sealed class NegotiateCertificateResponseBundle
    {
        /// <remarks>
        /// An attacker that MITMs the negotiation handshake over HTTP could generate and provide their own certificate authority to the client, but this doesn't actually give them any advantage:
        /// 
        /// - They can't impersonate the client to the real server, because even if they pass the original EK and AIK public bytes to the real server on the client's behalf, the real server would encrypt them such that only the original client would be able to decrypt the client certificate signed for the real server.
        /// - If the attacker passes their own EK and AIK public bytes to the real server, then the real server will only give them a certificate signed with a common name that matches their AIK, preventing them from impersonating the client to the real server.
        /// - The client has no guarantee they're speaking with the real server, so an attacker could theoretically tell the client to perform dangerous provisioning scripts. However, this doesn't matter in practice because if you can MITM the handshake, you can also just MITM the PXE network boot over TFTP, and that's a more practical method of taking control of the client machine.
        /// 
        /// Technically the latter could be mitigated with Secure Boot. This would require:
        /// 
        /// - The TFTP/PXE boot server to be able to build a custom iPXE image, signed with a key that is trusted by Secure Boot on the machines.
        /// - The custom iPXE image to set a CA, and be configured with the following script:
        ///   ```
        ///   #!ipxe
        ///   imgtrust --permanent
        ///   dhcp
        ///   imgfetch --name autoexec.ipxe autoexec.ipxe
        ///   imgverify autoexec.ipxe autoexec.ipxe.sig
        ///   imgexec autoexec.ipxe
        ///   ```
        /// 
        /// For more information: https://ipxe.org/crypto https://ipxe.org/cmd/imgverify
        /// 
        /// If we ever support that Secure Boot / custom iPXE scenario, then we should set the CA thumbprint during autoexec.ipxe and pass it down in such a way that the certificate authority here must have a matching thumbprint.
        /// </remarks>
        [JsonPropertyName("certificateAuthorityPem")]
        public required string CertificateAuthorityPem { get; set; }

        [JsonPropertyName("clientSignedPem")]
        public required string ClientSignedPem { get; set; }
    }
}
