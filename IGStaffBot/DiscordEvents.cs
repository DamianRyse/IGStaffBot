using Discord;
using Discord.WebSocket;

namespace IGStaffBot;

internal class DiscordEvents
{
    private DiscordSocketClient _client;

    internal DiscordEvents(DiscordSocketClient client)
    {
        _client = client;
    }

    internal async Task OnReady()
    {
        Console.WriteLine($"[{DateTime.UtcNow} UTC] Connected as '{_client.CurrentUser.Username}'.");
        foreach (var guild in _client.Guilds)
        {
            // Download users per guild to the cache and print out guild names + user count
            await guild.DownloadUsersAsync();
            Console.WriteLine($"[{DateTime.UtcNow} UTC] - {guild.Name} ({guild.Users.Count})");
        }
    }
    
    
    internal Task LogAsync(LogMessage log)
    {
        Console.WriteLine($"[{DateTime.UtcNow} UTC] {log.ToString()}");
        return Task.CompletedTask;
    }

    public Task OnUserJoined(SocketGuildUser arg)
    {
        throw new NotImplementedException();
    }

    public Task OnUserLeft(SocketGuild arg1, SocketUser arg2)
    {
        throw new NotImplementedException();
    }

    public Task OnUserBanned(SocketUser arg1, SocketGuild arg2)
    {
        throw new NotImplementedException();
    }

    public Task OnUserUnbanned(SocketUser arg1, SocketGuild arg2)
    {
        throw new NotImplementedException();
    }

    public Task OnGuildUpdated(SocketGuild arg1, SocketGuild arg2)
    {
        throw new NotImplementedException();
    }

    public Task OnRoleCreated(SocketRole arg)
    {
        throw new NotImplementedException();
    }

    public Task OnRoleDeleted(SocketRole arg)
    {
        throw new NotImplementedException();
    }

    public Task OnRoleUpdated(SocketRole arg1, SocketRole arg2)
    {
        throw new NotImplementedException();
    }
}