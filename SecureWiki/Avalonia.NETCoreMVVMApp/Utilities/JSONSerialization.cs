using System;
using System.IO;
using Newtonsoft.Json;

namespace SecureWiki.Utilities
{
    public class JSONSerialization
    {
        public static void SerializeAndWriteFile(string filepath, object item)
        {
            var jsonData = JsonConvert.SerializeObject(item, Formatting.Indented);
            File.WriteAllText(filepath, jsonData);
        }

        public static object? ReadFileAndDeserialize(string filepath, Type type)
        {
            var jsonData = File.ReadAllText(filepath);
            
            try
            {
                var item = JsonConvert.DeserializeObject(jsonData, type);
                return item;

            }
            catch (JsonReaderException e)
            {
                Console.WriteLine("ReadFileAndDeserialize:- Deserialization failed on path='{0}', type='{1}'.", filepath, type);
                Console.WriteLine(e.Message);
            }

            return null;
        }
        
        public static string SerializeObject(object item)
        {
            var jsonData = JsonConvert.SerializeObject(item, Formatting.Indented);
            return jsonData;
        }
        
        public static object DeserializeObject(string jsonData, Type type)
        {
            var item = JsonConvert.DeserializeObject(jsonData, type);
            return item;
        }

    }
}