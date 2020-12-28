using System;
using System.Security.Cryptography;
#if DEBUG
using System.Text;
#endif

namespace Rainbow
{
    /// <summary>
    /// Wrapper for a byte[] that overrides GetHashCode()
    /// </summary>
    internal struct ByteString
    {
        private static readonly MD5 md5 = MD5.Create();
        private byte[] array;

        public ByteString(int length)
        {
            array = new byte[length];
        }

        public ByteString Copy()
        {
            var copy = new byte[array.Length];
            array.CopyTo(copy, 0);
            return (ByteString)copy;
        }

        public override int GetHashCode()
        {
            var hash = md5.ComputeHash(array);
            return BitConverter.ToInt32(hash, 0);
        }

        public override bool Equals(object obj)
        {
            if (obj is ByteString other)
            {
                return Equals(other.array);
            }
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

        public static bool operator ==(ByteString a, ByteString b) => a.Equals(b);
        public static bool operator !=(ByteString a, ByteString b) => !(a == b);

        public static implicit operator byte[](ByteString b) => b.array;

        public static explicit operator ByteString(byte[] b)
        {
            var result = new ByteString(b.Length);
            result.array = b;
            return result;
        }

#if DEBUG
        public override string ToString()
        {
            if (array == null)
            {
                return "null";
            }
            var result = new StringBuilder(array.Length * 2);
            foreach (byte b in array)
            {
                result.Append(b.ToString("X"));
            }
            return result.ToString();
        }
#endif
    }
}
