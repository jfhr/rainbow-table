using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Rainbow
{
    internal static class RainbowHelper
    {
        static readonly ThreadLocal<MD5> md5 = new ThreadLocal<MD5>(() => MD5.Create());
        static readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();

        static RainbowParameters Pms => Program.Pms;

        public static string GenerateRandomPassword()
        {
            var randomBytes = new byte[4];
            rng.GetBytes(randomBytes);

            var hash = md5.Value.ComputeHash(randomBytes);
            return DerivePasswordInternal(hash, 0);
        }

        /// <summary>
        /// The reduction function. Returns different results depending on the value of <paramref name="iteration"/>.
        /// </summary>
        public static string DerivePassword(ByteString hash, int iteration)
        {
            var result = DerivePasswordInternal(hash.Copy(), iteration);
            return result;
        }

        /// <summary>
        /// Internal reduction function, this modifies <paramref name="hash"/>
        /// </summary>
        /// <remarks>
        /// Basically, we do the following:
        /// <list type="bullet">
        /// <item> XOR the first up to four bytes of the hash with the iteration </item>
        /// <item> Hash the result </item>
        /// <item> Use the resulting bytes as index into the list of allowed chars </item>
        /// </list>
        /// <para>
        /// We have to take the index modulo the number of allowed chars. This would mean that some
        /// characters are more likely than others.
        /// </para>
        /// <para>
        /// For example, let's say all upper- and lowercase letters are allowed, that's 52 characters.
        /// The byte values can be anywhere from 0 to 255. There are five values for x so that x % 52 = 10
        /// (10, 62, 114, 166, and 218). There are only four values for x so that x % 52 = 50 (50, 102, 154, 
        /// and 206). So we would be more likely to choose the character at index 10 than the one at index 50.
        /// To eliminate this bias, we skip if the byte value is too large, that is if 
        /// <code>x >= (256 / charCount * charCount)</code>
        /// </para>
        /// </remarks>
        private static string DerivePasswordInternal(byte[] hash, int iteration)
        {
            // basically, H(hash ^ iteration)
            for (int i = 0; i < hash.Length && i < 4; i++)
            {
                hash[i] ^= (byte)iteration;
                iteration >>= 8;
            }

            var hash2 = md5.Value.ComputeHash(hash);

            int charCount = Pms.PasswordChars.Length;
            var password = new char[Pms.PasswordLength];
            int indexInHash = 0;
            int indexInPassword = 0;
            while (indexInPassword < password.Length)
            {
                // if we've exhausted the hash, but don't yet have a complete password,
                // we make a new hash to continue. i hope this doesn't happen often
                if (indexInHash >= hash2.Length)
                {
                    hash2 = md5.Value.ComputeHash(hash2);
                    indexInHash = 0;
                }

                int charIndex = hash2[indexInHash];
                if (charIndex < (256 / charCount * charCount))
                {
                    // charIndex is ok
                    password[indexInPassword] = Pms.PasswordChars[charIndex % charCount];
                    indexInPassword++;
                }

                // else it is in the 'biased' area, so we discard it
                indexInHash++;
            }

            return new string(password);
        }

        /// <summary>
        /// The hash function.
        /// </summary>
        public static ByteString HashPassword(string password)
        {
            var passwordBytes = Encoding.Default.GetBytes(password);
            byte[] hash = md5.Value.ComputeHash(passwordBytes);

            if (hash.Length == Pms.HashLength)
            {
                return (ByteString)hash;
            }

            var cutHash = new byte[Pms.HashLength];
            Array.Copy(hash, cutHash, Pms.HashLength);
            var result = (ByteString)cutHash;
            return result;
        }

        /// <summary>
        /// Returns a dynamic enumerable of all the possible passwords based on the parameters.
        /// </summary>
        public static IEnumerable<string> IterateAllPasswords()
        {
            // take the cartesian product of the allowed chars with itself n times
            var partialProducts = Enumerable.Repeat(Enumerable.Empty<char>(), 1);
            for (int i = 0; i < Pms.PasswordLength; i++)
            {
                partialProducts = CartesianProductInternal(Pms.PasswordChars, partialProducts);
            }

            // convert the IEnumerable<char> to string, using the fact 
            // that we know the length ahead of time
            return partialProducts.Select(e =>
            {
                var str = new char[Pms.PasswordLength];
                var enumerator = e.GetEnumerator();
                for (int i = 0; i < str.Length; i++)
                {
                    enumerator.MoveNext();
                    str[i] = enumerator.Current;
                }
                return new string(str);
            });
        }

        private static IEnumerable<IEnumerable<char>> CartesianProductInternal(IEnumerable<char> source, IEnumerable<IEnumerable<char>> partialProducts)
        {
            foreach (var c in source)
            {
                foreach (var partialProduct in partialProducts)
                {
                    yield return partialProduct.Append(c);
                }
            }
        }
    }
}
