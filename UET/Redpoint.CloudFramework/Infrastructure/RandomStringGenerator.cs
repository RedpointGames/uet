namespace Redpoint.CloudFramework.Infrastructure
{
    using System;
    using System.Security.Cryptography;

    public class RandomStringGenerator : IRandomStringGenerator
    {
        private readonly RandomNumberGenerator _cryptoRng;

        public RandomStringGenerator()
        {
            _cryptoRng = RandomNumberGenerator.Create();
        }

        public string GetRandomString(int halfLength)
        {
            var bytes = new byte[halfLength];
            _cryptoRng.GetBytes(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
