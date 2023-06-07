using System.Text.Json;
using System.Text.Json.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace IGStaffBot;

public class Configuration
{
    [JsonPropertyName("B64_DiscordToken")]
    public string? DiscordToken { get; set; }

    public List<Event>? Events { get; set; }
    
   
    /// <summary>
    /// Reads the configuration file and deserializes it to a Configuration class object.
    /// </summary>
    /// <param name="filePath">Path to the JSON file.</param>
    /// <returns>Deserialized program configuration</returns>
    internal static Configuration ReadFromFile(string filePath)
    {
        // Specify an option to deserialize Enums
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter());
        
        // open a file stream and try to deserialize the content.
        // If the content can't be parsed, return null.
        using var file = File.OpenText(filePath);
        try
        {
            var retVal = JsonSerializer.Deserialize<Configuration>(file.ReadToEnd(), options);
            if (retVal is not null)
                return retVal;

        }
        catch(Exception ex)
        {
            Console.WriteLine($"[{DateTime.UtcNow} UTC] {ex.Message}");
            Console.WriteLine($"[{DateTime.UtcNow} UTC] {ex.StackTrace}");
        }
        
        return new Configuration
        {
            DiscordToken = "NO_DISCORD_TOKEN",
            Events = new List<Event>()
        };
    }

    public enum EventType
    {
        UserJoined,
        UserLeft,
        UserBanned,
        UserUnbanned,
        RoleCreated,
        RoleDeleted,
        RoleUpdated,
        GuildUpdated,
        AuditLog
    }

    public class Event
    {
        public EventType EventType { get; set; }
        public ulong SourceDiscordId { get; set; }
        public ulong DestinationDiscordId { get; set; }
        public ulong DestinationChannelId { get; set; }
        public bool IsEnabled { get; set; }

        public Event()
        {
            
        }
    }
}