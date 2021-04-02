using System;
using System.Collections.Generic;

namespace SecureWiki.Utilities
{
    public class ByteArrayComparer : IComparer<byte[]>
    {
        public int Compare(byte[]? x, byte[]? y)
        {
            if (x == null && y == null)
            {
                return 0;
            }

            if (x == null)
            {
                return -1;
            }

            if (y == null)
            {
                return 1;
            }

            if (x.Length < y.Length)
            {
                return -1;
            }
            
            if (x.Length > y.Length)
            {
                return 1;
            }

            for (int i = 0; i < x.Length; i++)
            {
                var comp = x[i].CompareTo(y[i]);
                if (comp < 0)
                {
                    return -1;
                }
                if (comp > 0)
                {
                    return 1;
                }
            }

            return 0;
        }
    }
}