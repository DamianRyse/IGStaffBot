using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace IGStaffBot;

internal class DiscordEvents
{
    private readonly DiscordSocketClient _client;
    private readonly Configuration _configuration;
    private List<ulong> _auditLogIdCache = new List<ulong>();

    internal DiscordEvents(DiscordSocketClient client, Configuration config)
    {
        _client = client;
        _configuration = config;
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

    public async Task ReadAuditLogAsync(SocketGuild guild)
    {
        var auditLog = guild.GetAuditLogsAsync(5).FlattenAsync().Result.OrderBy(x=>x.CreatedAt);
        foreach (var log in auditLog)
        {
            // If the ulong is already present in the cache, we skip this one.
            if (_auditLogIdCache.Contains(log.Id))
                continue;
            
            switch (log.Data)
            {
                case MemberRoleAuditLogData:
                    await ProcessMemberRoleAuditLog(log,guild);
                    break;
                case KickAuditLogData:
                    await ProcessKickAuditLog(log,guild);
                    break;
                case InviteCreateAuditLogData:
                    await ProcessInviteCreatedLog(log,guild);
                    break;
                case InviteDeleteAuditLogData:
                    await ProcessInviteDeleteAuditLog(log, guild);
                    break;
                case GuildUpdateAuditLogData:
                    await ProcessGuildUpdateAuditLog(log, guild);
                    break;
                case MemberUpdateAuditLogData:
                    await ProcessMemberUpdateAuditLog(log, guild);
                    break;
                default:
                    // Find the event
                    var ev = _configuration.Events.First(x =>
                        x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
                    
                    // Get the guilds, channels and users
                    var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
                    var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
                    
                    // Post default message
                    await destinationChannel.SendMessageAsync($"{TimestampTag.FromDateTime(log.CreatedAt.LocalDateTime)} {log.User.Username}: {log.Action} *(Unhandled audit log format)*");
                    _auditLogIdCache.Add(log.Id);
                    break;
            }
        }
    }

    
    private async Task ProcessMemberRoleAuditLog(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (MemberRoleAuditLogData)data.Data;

        // Get the guilds, channels and users
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // Prep a list of changed roles and add a + or - sign to indicate if they got added or removed
        var changelog = new StringBuilder();
        foreach (var role in auditLog.Roles)
        {
            changelog.AppendLine(role.Added ? $"**+** {role.Name}" : $"**-** {role.Name}");
        }

        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} changed roles of member: {auditLog.Target.Username}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"**In guild:** {guild.Name}\n" +
                             changelog.ToString())
            .WithColor(Color.LightGrey)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
        _auditLogIdCache.Add(data.Id);
    }
    
    private async Task ProcessKickAuditLog(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (KickAuditLogData)data.Data;

        // Get the guilds and channels
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);

        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} kicked member: {auditLog.Target.Username}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"{data.Reason}")
            .WithColor(Color.LightGrey)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
        _auditLogIdCache.Add(data.Id);
    }
    
    private async Task ProcessMemberUpdateAuditLog(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (MemberUpdateAuditLogData)data.Data;

        // Get the guilds and channels
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // Compare the changes and add only the changed objects to a StringBuilder
        var changelog = new StringBuilder();
        if (auditLog.Before.Nickname != auditLog.After.Nickname)
            changelog.AppendLine($"Changed nickname to: {auditLog.After.Nickname}");
        if (auditLog.Before.Deaf != auditLog.After.Deaf)
            if(auditLog.After.Deaf.HasValue)
                changelog.AppendLine(auditLog.After.Deaf.Value ? "Deafend the user" : "Undeafend the user");
        if (auditLog.Before.Mute != auditLog.After.Mute)
            if (auditLog.After.Mute.HasValue)
                changelog.AppendLine(auditLog.After.Mute.Value ? "Muted the user" : "Unmuted the user");
        

        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} updated member: {auditLog.Target.Username}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"{changelog.ToString()}")
            .WithColor(Color.DarkBlue)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
        _auditLogIdCache.Add(data.Id);
    }
   
    private async Task ProcessGuildUpdateAuditLog(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (GuildUpdateAuditLogData)data.Data;

        // Get the guilds and channels
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // Compare the changes and add only the changed objects to a StringBuilder
        var changelog = new StringBuilder();
        if (auditLog.Before.Name != auditLog.After.Name)
            changelog.AppendLine($"Changed name to: {auditLog.After.Name}");
        if (auditLog.Before.Owner != auditLog.After.Owner)
            changelog.AppendLine($"Changed owner to: {auditLog.After.Owner}");
        if (auditLog.Before.AfkTimeout != auditLog.After.AfkTimeout)
            changelog.AppendLine($"Changed AFK Timeout to: {auditLog.After.AfkTimeout}");
        if (auditLog.Before.AfkChannelId != auditLog.After.AfkChannelId)
            if (auditLog.After.AfkChannelId != null)
                changelog.AppendLine(
                    $"Changed AFK channel to: {guild.GetChannel(auditLog.After.AfkChannelId.Value).Name}");
        if (auditLog.Before.SystemChannelId != auditLog.After.SystemChannelId)
            if (auditLog.After.SystemChannelId != null)
                changelog.AppendLine(
                    $"Changed systems channel to: {guild.GetChannel(auditLog.After.SystemChannelId.Value)}");
        if (auditLog.Before.IconHash != auditLog.After.IconHash)
            changelog.AppendLine($"Set the guild icon.");


        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} updated guild settings")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"{changelog.ToString()}")
            .WithColor(Color.Red)
            .WithImageUrl(guild.IconUrl);

        await destinationChannel.SendMessageAsync(embed: emb.Build());
        _auditLogIdCache.Add(data.Id);
    }
    
    private async Task ProcessInviteCreatedLog(RestAuditLogEntry data,SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (InviteCreateAuditLogData)data.Data;

        // Get the guilds and channels
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{auditLog.Creator.Username} created invite code {auditLog.Code}")
            .WithDescription($"**Creation time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"**In Discord:** {guild.Name}\n" +
                             $"**In channel:** {guild.GetChannel(auditLog.ChannelId).Name}\n" +
                             $"**Max usages:** {auditLog.MaxUses}\n" +
                             $"**Valid until:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime.AddSeconds(auditLog.MaxAge)).ToString()}")
            .WithColor(Color.Gold)
            .WithThumbnailUrl(string.IsNullOrEmpty(auditLog.Creator.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : auditLog.Creator.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
        _auditLogIdCache.Add(data.Id);
    }
    
    private async Task ProcessInviteDeleteAuditLog(RestAuditLogEntry data,SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (InviteDeleteAuditLogData)data.Data;
        
        // Get the guilds and channels
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} deleted invite code {auditLog.Code}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"**In Discord:** {guild.Name}\n" +
                             $"**In channel:** {guild.GetChannel(auditLog.ChannelId).Name}\n" +
                             $"**Created by:** {auditLog.Creator.Username}" +
                             $"**Usages:** {auditLog.Uses}/{auditLog.MaxUses}\n" +
                             $"**Valid until:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime.AddSeconds(auditLog.MaxAge)).ToString()}")
            .WithColor(Color.Gold)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
        _auditLogIdCache.Add(data.Id);
    }
    
    internal Task LogAsync(LogMessage log)
    {
        Console.WriteLine($"[{DateTime.UtcNow} UTC] {log.ToString()}");
        return Task.CompletedTask;
    }

    public async Task OnUserJoined(SocketGuildUser user)
    {
        // Get all enabled events with the specific EventType and the correct source Discord ID
        var events = _configuration.Events.Where(@event => @event.EventType == Configuration.EventType.UserJoined &&
                                                           @event.SourceDiscordId == user.Guild.Id &&
                                                           @event.IsEnabled).ToArray();
        
        // Go through each event and send the log message.
        foreach (var e in events)
        {
            var sourceGuild = _client.GetGuild(e.SourceDiscordId);
            var destinationGuild = _client.GetGuild(e.DestinationDiscordId);
            var destinationChannel = destinationGuild.GetTextChannel(e.DestinationChannelId);

            await destinationChannel.SendMessageAsync($"*USER JOINED:* **{user}** in **{sourceGuild.Name}**");
        }
    }

    public async Task OnUserLeft(SocketGuild sourceGuild, SocketUser user)
    {
        // Get all events with the specific EventType and the correct source Discord ID
        var events = _configuration.Events.Where(@event => @event.EventType == Configuration.EventType.UserLeft &&
                                                           @event.SourceDiscordId == sourceGuild.Id &&
                                                           @event.IsEnabled).ToArray();
        
        // Go through each event and send the log message.
        foreach (var e in events)
        {
            var destinationGuild = _client.GetGuild(e.DestinationDiscordId);
            var destinationChannel = destinationGuild.GetTextChannel(e.DestinationChannelId);

            await destinationChannel.SendMessageAsync($"*USER LEFT:* **{user}** in **{sourceGuild.Name}**");
        }
    }
}