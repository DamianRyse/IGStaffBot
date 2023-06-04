using System.Text;
using Newtonsoft.Json;

namespace IGStaffBot;

internal class Configuration
{
    internal string DiscordToken { get; }

    /// <summary>
    /// Reads the configuration file and deserializes it to a Configuration class object.
    /// </summary>
    /// <param name="filePath">Path to the JSON file.</param>
    /// <returns>Deserialized program configuration</returns>
    internal static Configuration ReadFromFile(string filePath)
    {
        using var file = File.OpenText(filePath);
        var serializer = new JsonSerializer();
        var conf = (Configuration)serializer.Deserialize(file, typeof(Configuration))!;
        
        return conf;
    }
}