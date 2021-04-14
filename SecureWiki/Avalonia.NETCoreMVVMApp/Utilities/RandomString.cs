using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SecureWiki.Utilities
{
    public static class RandomString
    {
        
        // Compute SHA-256 hash of input string
        public static string ComputeHash(string inputString)
        {
            // init SHA-256   
            using SHA256 sha256 = SHA256.Create();
            // Convert input to byte array and compute hash
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(inputString));  
  
            // Build string from byte array
            StringBuilder sb = new();  
            foreach (var t in bytes)
            {
                sb.Append(t.ToString("x2"));
            }  
            return sb.ToString();
        }
        
        // Create random string of given length containing alphanumeric characters
        public static string GenerateRandomAlphanumericString(int length = 20)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            
            var random = new Random();
            var randomString = new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            
            return randomString;
        }
    }
}