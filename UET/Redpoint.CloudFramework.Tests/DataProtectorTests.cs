namespace Redpoint.CloudFramework.Tests
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Primitives;
    using Redpoint.CloudFramework.DataProtection;
    using System;
    using System.Collections.Generic;
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
                        case "CloudFramework:Security:AES:Key":
                            return "/kiievoYGVVUONHYJBwUhjjQjgwUkhRpGF6F/luR7YY=";
                        case "CloudFramework:Security:AES:IV":
                            return "keqOqvOgSbQU1/cPjFM9FA==";
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
        public void TestDecryption()
        {
            var protector1 = new StaticDataProtector(null!, new MockConfiguration(), null!);
            var protector2 = new StaticDataProtector(null!, new MockConfiguration(), null!);

            var originalValue = "Hello World";
            var encryptedValue = protector1.Protect(Encoding.ASCII.GetBytes(originalValue));
            var decryptedValue = Encoding.ASCII.GetString(protector2.Unprotect(encryptedValue));
            Assert.Equal(originalValue, decryptedValue);
        }
    }
}
