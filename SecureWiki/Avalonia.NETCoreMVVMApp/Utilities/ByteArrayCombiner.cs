using System;

namespace SecureWiki.Utilities
{
    public static class ByteArrayCombiner
    {
        public static byte[] Combine(byte[] a, byte[] b)
        {
            byte[] output = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, output, 0, a.Length);
            Buffer.BlockCopy(b, 0, output, a.Length, b.Length);

            return output;
        }
    }
}