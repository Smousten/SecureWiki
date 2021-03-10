using System.Security.Cryptography;
using System.Text;

namespace SecureWiki.Cryptography
{
    public class RandomString
    {
        public string ComputeHash(string inputString)  
        {  
            // init SHA-256   
            using (SHA256 sha256 = SHA256.Create())  
            {  
                // Convert input to byte array and compute hash
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(inputString));  
  
                // Build string from byte array
                StringBuilder sb = new();  
                for (int i = 0; i < bytes.Length; i++)  
                {  
                    sb.Append(bytes[i].ToString("x2"));  
                }  
                return sb.ToString();  
            }  
        }
    }
}