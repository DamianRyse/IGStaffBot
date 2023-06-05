using System.Timers;
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
    private System.Timers.Timer _auditLogTimer;

    public async Task Initialize()
    {
        Console.Title = "IMPULSE GAMING - Audit Logs and Events redirector.";
        Console.WriteLine($"IMPULSE GAMING - Audit Logs and Events redirector.");
        Console.WriteLine($"##################################################");
        Console.WriteLine();
        
        // Initialize services
        await using var services = ConfigureServices();
        _client = services.GetRequiredService<DiscordSocketClient>();
        _configuration = services.GetRequiredService<Configuration>();
        
        
        // Initialize a new DiscordEvents class
        _events = new DiscordEvents(_client,_configuration);
        
        // Initialize the Audit log timer
        _auditLogTimer = new System.Timers.Timer(60000);
        _auditLogTimer.Elapsed += AuditLogTimerOnElapsed;
        

        _client.Ready += _events.OnReady;
        _client.Log += _events.LogAsync;
        _client.UserJoined += _events.OnUserJoined;
        _client.UserLeft += _events.OnUserLeft;

        services.GetRequiredService<CommandService>().Log += _events.LogAsync;
        
 
        
        // Login to Discord using the DiscordToken from the .env file. The token inside the file is Base64 encoded and
        // has to be decoded here first.
        await _client.LoginAsync(TokenType.Bot, System.Text.Encoding.UTF8.GetString(
            System.Convert.FromBase64String(_configuration.DiscordToken)));
        await _client.StartAsync();
        
        // start the timer
        _auditLogTimer.Start();

        await Task.Delay(Timeout.Infinite);
    }

    private async void AuditLogTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        var events = _configuration.Events.Where(ev => ev is { EventType: Configuration.EventType.AuditLog, IsEnabled: true });
        foreach (var ev in events)
        {
            if(_client.GetGuild(ev.SourceDiscordId) is { } guild)
                await _events.ReadAuditLogAsync(guild);
            else
            {
                Console.WriteLine($"[{DateTime.UtcNow} UTC] WARNING: Guild with ID {ev.SourceDiscordId} does not exist.");
            }
        }
    }

    private static ServiceProvider ConfigureServices()
    {
        return new ServiceCollection()
            .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Warning,
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers
            }))
            .AddSingleton<CommandService>()
            .AddSingleton(Configuration.ReadFromFile(".env"))
            .BuildServiceProvider();
    }
}


