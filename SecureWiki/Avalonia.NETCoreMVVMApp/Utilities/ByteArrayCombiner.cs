using System;
using System.Collections.Generic;
using System.Linq;

namespace SecureWiki.Utilities
{
    public static class ByteArrayCombiner
    {
        // Combine two byte arrays to one byte array
        public static byte[] Combine(byte[] a, byte[] b)
        {
            byte[] output = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, output, 0, a.Length);
            Buffer.BlockCopy(b, 0, output, a.Length, b.Length);

            return output;
        }

        // Combine a list of byte arrays to one byte array
        public static byte[] Combine(List<byte[]> list)
        {
            var len = list.Sum(item => item.Length);
            byte[] output = new byte[len];

            var dstOffset = 0;
            
            foreach (var a in list)
            {
                Buffer.BlockCopy(a, 0, output, dstOffset, a.Length);
                dstOffset += a.Length;
            }

            return output;
        }
        
        // Return subarray of data at start index with given length
        public static byte[] SubArray(byte[] data, int start, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(data, start, result, 0, length);
            return result;
        }

    }
}