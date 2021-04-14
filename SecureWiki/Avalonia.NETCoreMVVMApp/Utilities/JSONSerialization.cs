using System;
using System.IO;
using Newtonsoft.Json;

namespace SecureWiki.Utilities
{
    public static class JSONSerialization
    {
        public static void SerializeAndWriteFile(string filepath, object item)
        {
            var jsonData = SerializeObject(item);
            File.WriteAllText(filepath, jsonData);
        }

        public static object? ReadFileAndDeserialize(string filepath, Type type)
        {
            var jsonData = File.ReadAllText(filepath);

            var result = DeserializeObject(jsonData, type);

            if (result == null)
            {
                Console.WriteLine("ReadFileAndDeserialize:- Deserialization failed on path='{0}'.", filepath);
            }

            return result;
        }
        
        public static string SerializeObject(object item)
        {
            var jsonData = JsonConvert.SerializeObject(item, Formatting.Indented);
            return jsonData;
        }
        
        // Attempt to deserialize string into object of specified type, return null if this fails. 
        public static object? DeserializeObject(string jsonData, Type type)
        {
            try
            {
                var item = JsonConvert.DeserializeObject(jsonData, type);
                return item;
            }
            catch (JsonReaderException e)
            {
                Console.WriteLine("DeserializeObject:- Deserialization failed on jsonData of type='{0}'.", type);
                Console.WriteLine(jsonData);
                Console.WriteLine(e.Message);
            }

            return null;
        }

    }
}