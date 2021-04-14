using System;
using System.Collections.Generic;

namespace SecureWiki.Utilities
{
    public class ByteArrayComparer : IComparer<byte[]>
    {
        
        // Returns -1 if x < y, 0 if x=y, and 1 if x > y
        public int Compare(byte[]? x, byte[]? y)
        {
            // If both are null
            if (x == null && y == null) return 0;

            // If only either is null
            if (x == null) return -1;
            if (y == null) return 1;

            // If length differs
            if (x.Length < y.Length) return -1;
            if (x.Length > y.Length) return 1;

            // Else compare each index as int value 
            for (int i = 0; i < x.Length; i++)
            {
                var comp = x[i].CompareTo(y[i]);
                switch (comp)
                {
                    case < 0:
                        return -1;
                    case > 0:
                        return 1;
                }
            }

            return 0;
        }
    }
}