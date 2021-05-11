using System;
using System.Collections.Generic;
using SecureWiki.Model;

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
    
    public class MDFileComparer : IComparer<MDFile>
    {
        public int Compare(MDFile? x, MDFile? y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (ReferenceEquals(null, y)) return 1;
                if (ReferenceEquals(null, x)) return -1;
                return string.Compare(x.name, y.name, StringComparison.Ordinal);
            }
    }
    
    public class MDFolderComparer : IComparer<MDFolder>
    {
        public int Compare(MDFolder? x, MDFolder? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (ReferenceEquals(null, y)) return 1;
            if (ReferenceEquals(null, x)) return -1;
            return string.Compare(x.name, y.name, StringComparison.Ordinal);
        }
    }
}