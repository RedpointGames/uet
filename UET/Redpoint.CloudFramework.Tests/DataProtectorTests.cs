namespace Redpoint.CloudFramework.Tests
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Primitives;
    using NSec.Cryptography;
    using Redpoint.CloudFramework.DataProtection;
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using Xunit;

    public class DataProtectorTests
    {
        internal class MockConfiguration : IConfiguration
        {
            public string? this[string key]
            {
                get
                {
                    switch (key)
                    {
                        case "CloudFramework:Security:XChaCha20Poly1305:Key":
                            return "3mFI3iAAEABJzVq72zoagVie/h0k1d38ScZGI1UiR0qA4KCiOaYdtg==";
                        default:
                            return null;
                    }
                }
                set => throw new NotImplementedException();
            }

            public IEnumerable<IConfigurationSection> GetChildren()
            {
                throw new NotImplementedException();
            }

            public IChangeToken GetReloadToken()
            {
                throw new NotImplementedException();
            }

            public IConfigurationSection GetSection(string key)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void TestNSec()
        {
            var a = AeadAlgorithm.XChaCha20Poly1305;

            var k = Key.Create(AeadAlgorithm.XChaCha20Poly1305, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
            var n = RandomNumberGenerator.GetBytes(a.NonceSize);
            var ad = RandomNumberGenerator.GetBytes(100);

            var length = Encoding.ASCII.GetBytes("Hello World").Length;
            var expected = RandomNumberGenerator.GetBytes(length).ToArray();

            var ciphertext = a.Encrypt(k, n, ad, expected);
            Assert.NotNull(ciphertext);
            Assert.Equal(length + a.TagSize, ciphertext.Length);

            var actual = a.Decrypt(k, n, ad, ciphertext);
            Assert.NotNull(actual);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestGenerateKey()
        {
            var a = AeadAlgorithm.XChaCha20Poly1305;

            var k = Convert.ToBase64String(Key.Create(AeadAlgorithm.XChaCha20Poly1305, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport }).Export(KeyBlobFormat.NSecSymmetricKey));

            TestContext.Current.AddAttachment("generated_key", k);
        }

        [Fact]
        public void TestDecryption()
        {
            var configuration = new MockConfiguration();

            var protector1 = new StaticDataProtector(null!, configuration, null!);
            var protector2 = new StaticDataProtector(null!, configuration, null!);

            var originalValue = "Hello World";
            var encryptedValue = protector1.Protect(Encoding.ASCII.GetBytes(originalValue));
            var decryptedValue = Encoding.ASCII.GetString(protector2.Unprotect(encryptedValue));
            Assert.Equal(originalValue, decryptedValue);
        }
    }
}
