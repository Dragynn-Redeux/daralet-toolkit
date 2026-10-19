using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace DaraletPlayer.Feed {
    /// <summary>
    /// JSON in and out, the one way the plugin does it. The server writes with System.Text.Json, which writes a dictionary
    /// as an object ({"a":1}); DataContractJsonSerializer only reads (and writes) that with UseSimpleDictionaryFormat.
    /// </summary>
    public static class Json {
        public static T Read<T>(string json) {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) {
                return (T)Serializer<T>().ReadObject(stream);
            }
        }

        public static T Read<T>(Stream stream) {
            return (T)Serializer<T>().ReadObject(stream);
        }

        public static string Write<T>(T value) {
            using (var stream = new MemoryStream()) {
                Serializer<T>().WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static void Write<T>(Stream stream, T value) {
            Serializer<T>().WriteObject(stream, value);
        }

        private static DataContractJsonSerializer Serializer<T>() {
            return new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
        }
    }
}
