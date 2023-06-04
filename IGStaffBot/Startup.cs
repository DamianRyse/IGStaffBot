using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace IGStaffBot;

public class Startup
{
    private DiscordSocketClient _client;
    private Configuration _configuration;
    private DiscordEvents _events;

    public async Task Initialize()
    {
        // Read the .env file
        _configuration = Configuration.ReadFromFile(".env");
        
        // Initialize a new DiscordEvents class
        _events = new DiscordEvents(_client);
        
        
        await using var services = ConfigureServices();
        _client = services.GetRequiredService<DiscordSocketClient>();

        _client.Ready += _events.OnReady;
        _client.Log += _events.LogAsync;
        _client.UserJoined += _events.OnUserJoined;
        _client.UserLeft += _events.OnUserLeft;
        _client.UserBanned += _events.OnUserBanned;
        _client.UserUnbanned += _events.OnUserUnbanned;
        _client.GuildUpdated += _events.OnGuildUpdated;
        _client.RoleCreated += _events.OnRoleCreated;
        _client.RoleDeleted += _events.OnRoleDeleted;
        _client.RoleUpdated += _events.OnRoleUpdated;
        
        services.GetRequiredService<CommandService>().Log += _events.LogAsync;
        
        await _client.LoginAsync(TokenType.Bot, _configuration.DiscordToken);
        await _client.StartAsync();

        await Task.Delay(Timeout.Infinite);
    }
    
    

    
    private static ServiceProvider ConfigureServices()
    {
        return new ServiceCollection()
            .AddSingleton<DiscordSocketClient>()
            .AddSingleton<CommandService>()
            .BuildServiceProvider();
    }
    
}


