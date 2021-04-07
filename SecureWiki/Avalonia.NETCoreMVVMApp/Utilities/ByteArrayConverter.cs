using System;

namespace SecureWiki.Utilities
{
    public static class ByteArrayConverter
    {
        public static string GetHexString(byte[]? a)
        {
            if (a == null)
            {
                return "input array is null";
            }
            return BitConverter.ToString(a);
        }
    }
}