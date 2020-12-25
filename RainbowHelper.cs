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

        public static string DerivePassword(RainbowParameters pms, byte[] hash, int iteration)
        {
            // get first 32 bits of sha512 of original hash
            byte[] hash2 = sha512.ComputeHash(hash);
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

        public static byte[] HashPassword(RainbowParameters pms, string password)
        {
            var passwordBytes = Convert.FromBase64String(password);
            var hash = sha512.ComputeHash(passwordBytes);
            if (pms.HashLength == 512)
            {
                return hash;
            }

            var cutHash = new byte[pms.HashLength];
            Array.Copy(hash, cutHash, pms.HashLength);
            return cutHash;
        }

        public static bool AreEqual(byte[] a, byte[] b)
        {
            if (a is null || b is null && a != b)
            {
                return false;
            }
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
