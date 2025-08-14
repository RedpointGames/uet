namespace Redpoint.Reservation
{
    using System;
    using System.Numerics;
    using System.Security.Cryptography;
    using System.Text;

    public static class StabilityHash
    {
        public static string GetStabilityHash(string inputString, int? length)
        {
            var inputBytes = SHA256.HashData(Encoding.ASCII.GetBytes(inputString));
            const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz.-_";
            var dividend = new BigInteger(inputBytes);
            var builder = new StringBuilder();
            while (dividend != 0)
            {
                dividend = BigInteger.DivRem(dividend, alphabet.Length, out BigInteger remainder);
                builder.Insert(0, alphabet[Math.Abs((int)remainder)]);
            }
            string target;
            if (!length.HasValue)
            {
                target = builder.ToString();
            }
            else
            {
                target = builder.ToString()[..length.Value];
            }
            var targetChars = target.ToCharArray();
            // @note: We must replace . at the end of the string with _
            // because Windows does not support trailing dots. However,
            // we don't want to alter the length of the resulting string
            // for path length stability reasons, so we replace
            // dots with underscores instead of trimming.
            for (int d = targetChars.Length - 1; d >= 0; d--)
            {
                if (targetChars[d] == '.')
                {
                    targetChars[d] = '_';
                }
                else
                {
                    break;
                }
            }
            return new string(targetChars);
        }
    }
}
