using System;
using System.Security.Cryptography;

namespace Rainbow
{
    static class RainbowHelper
    {
        public static SHA512 sha512 = new SHA512Managed();

        public static string GenerateRandomPassword(RainbowParameters pms)
        {
            var rng = new Random();
            var randomBytes = new byte[4];
            rng.NextBytes(randomBytes);

            var hash = sha512.ComputeHash(randomBytes);
            return DerivePassword(pms, hash, 0);
        }

        /// <summary>
        /// The reduction function. Returns different results depending on the value of <paramref name="iteration"/>.
        /// </summary>
        public static string DerivePassword(RainbowParameters pms, byte[] hash, int iteration)
        {
            // get first 32 bits of sha512 of original hash
            var hash2 = sha512.ComputeHash(hash);
            int seed = BitConverter.ToInt32(hash2, 0);

            // xor with iteration so we get different results for each row
            seed ^= iteration;

            // seed rng
            var rng = new Random(seed);

            // generate chars from rng
            var password = new char[pms.PasswordLength];
            for (int i = 0; i < pms.PasswordLength; i++)
            {
                int charIndex = rng.Next(0, pms.PasswordChars.Length);
                password[i] = pms.PasswordChars[charIndex];
            }

            return new string(password);
        }

        /// <summary>
        /// The hash function.
        /// </summary>
        public static HashableByteArray HashPassword(RainbowParameters pms, string password)
        {
            var passwordBytes = Convert.FromBase64String(password);
            var hash = sha512.ComputeHash(passwordBytes);
            if (pms.HashLength == 64)
            {
                return (HashableByteArray)hash;
            }

            var cutHash = new byte[pms.HashLength];
            Array.Copy(hash, cutHash, pms.HashLength);
            return (HashableByteArray)cutHash;
        }
    }
}
