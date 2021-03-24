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

        public static object ReadFileAndDeserialize(string filepath, Type type)
        {
            var jsonData = File.ReadAllText(filepath);
            var item = JsonConvert.DeserializeObject(jsonData, type);

            return item;
        }

    }
}