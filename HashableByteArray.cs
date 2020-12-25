using System;
using System.Security.Cryptography;

namespace Rainbow
{
    /// <summary>
    /// Wrapper for a byte[] that overrides GetHashCode()
    /// </summary>
    struct HashableByteArray
    {
        private static readonly MD5 md5 = MD5.Create();
        private byte[] array;

        public HashableByteArray(int length)
        {
            array = new byte[length];
        }

        public byte[] Copy()
        {
            var copy = new byte[array.Length];
            array.CopyTo(copy, 0);
            return copy;
        }

        public override int GetHashCode()
        {
            var hash = md5.ComputeHash(array);
            return BitConverter.ToInt32(hash, 0);
        }

        public override bool Equals(object obj)
        {
            if (obj is byte[] otherArray && otherArray.Length == array.Length)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    if (otherArray[i] != array[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        public static bool operator ==(HashableByteArray a, HashableByteArray b) => a.Equals(b);
        public static bool operator !=(HashableByteArray a, HashableByteArray b) => !(a == b);

        public static implicit operator byte[](HashableByteArray b) => b.array;

        public static explicit operator HashableByteArray(byte[] b)
        {
            var result = new HashableByteArray(b.Length);
            result.array = b;
            return result;
        }
    }
}
