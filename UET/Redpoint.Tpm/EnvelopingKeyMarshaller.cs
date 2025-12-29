namespace Redpoint.Tpm
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Tpm2Lib;

    public static class EnvelopingKeyMarshaller
    {
        public static byte[] EnvelopingKeyToBytes(this IdObject certInfo, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(certInfo);

            var envelopingKeyMarshaller = new Marshaller();
            envelopingKeyMarshaller.Put(certInfo.integrityHMAC.Length, "integrityHMAC.Length");
            envelopingKeyMarshaller.Put(certInfo.integrityHMAC, "integrityHMAC");
            envelopingKeyMarshaller.Put(certInfo.encIdentity.Length, "encIdentity");
            envelopingKeyMarshaller.Put(certInfo.encIdentity, "encIdentity.Length");
            var bytes = envelopingKeyMarshaller.GetBytes();

            logger?.LogTrace($"Converted enveloping key to byte array (identity HMAC length: {certInfo.integrityHMAC.Length}, encrypted identity length: {certInfo.encIdentity.Length}, result byte array length: {bytes.Length}).");

            return bytes;
        }

        public static IdObject BytesToEnvelopingKey(this byte[] bytes, ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(bytes);

            IdObject envelopingKey = new IdObject();
            var envelopingKeyMarshaller = new Marshaller(bytes);
            int identityHmacLength = envelopingKeyMarshaller.Get<int>();
            envelopingKey.integrityHMAC = envelopingKeyMarshaller.GetArray<byte>(identityHmacLength);
            int encIdentityLength = envelopingKeyMarshaller.Get<int>();
            envelopingKey.encIdentity = envelopingKeyMarshaller.GetArray<byte>(encIdentityLength);

            logger?.LogTrace($"Converted byte array to enveloping key (identity HMAC length: {identityHmacLength}, encrypted identity length: {encIdentityLength}, result byte array length: {bytes.Length}).");

            return envelopingKey;
        }
    }
}
