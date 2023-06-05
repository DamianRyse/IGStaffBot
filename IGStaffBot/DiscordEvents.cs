using System.Data;
using System.Runtime.CompilerServices;
using System.Text;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
// ReSharper disable SuggestBaseTypeForParameter

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

    /// <summary>
    /// Reads the audit log of a specific guild. It also casts the audit logs into specific types and calls the handler (if available).
    /// </summary>
    /// <param name="guild"></param>
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
                case GuildUpdateAuditLogData:
                    await GuildUpdateAuditLogHandler(log, guild);
                    break;
                case ChannelCreateAuditLogData:
                    await ChannelCreateAuditLogHandler(log, guild);
                    break;
                case ChannelUpdateAuditLogData:
                    await ChannelUpdateAuditLogHandler(log, guild);
                    break;
                case ChannelDeleteAuditLogData:
                    await ChannelDeleteAuditLogHandler(log, guild);
                    break;
                case OverwriteCreateAuditLogData:
                    await OverwriteCreateAuditLogHandler(log, guild);
                    break;
                case OverwriteUpdateAuditLogData:
                    await OverwriteUpdateAuditLogHandler(log, guild);
                    break;
                case OverwriteDeleteAuditLogData:
                    await OverwriteDeleteAuditLogHandler(log, guild);
                    break;
                case KickAuditLogData:
                    await KickAuditLogHandler(log,guild);
                    break;
                case PruneAuditLogData:
                    await PruneAuditLogHandler(log, guild);
                    break;
                case BanAuditLogData:
                    await BanAuditLogHandler(log, guild);
                    break;
                case UnbanAuditLogData:
                    await UnbanAuditLogHandler(log, guild);
                    break;
                case MemberUpdateAuditLogData:
                    await MemberUpdateAuditLogHandler(log, guild);
                    break;
                case MemberRoleAuditLogData:
                    await MemberRoleAuditLogHandler(log,guild);
                    break;
                // Move Member
                // Disconnect Member
                // Add Bot
                // Create Thread
                // Update Thread
                // Delete Thread
                // Create role
                case RoleUpdateAuditLogData:
                    await RoleUpdateAuditLogHandler(log, guild);
                    break;
                // Delete role
                case InviteCreateAuditLogData:
                    await InviteCreatedLogHandler(log,guild);
                    break;
                case InviteDeleteAuditLogData:
                    await InviteDeleteAuditLogHandler(log, guild);
                    break;
                // Create Webhook
                // Update Webhook
                // Delete Webhook
                // Create Emoji
                // Update Emoji
                // Delete Emoji
                // Delete Messages
                // Bulk Delete Messages
                // Pin Message
                // Unpin Message
                // Create Integration
                // Update Integration
                // Delete Integration
                // Create Sticker
                // Update Sticker
                // Delete Sticker
                // Start Stage
                // Update Stage
                // End Stage
                // Create Event
                // Update Event
                // Cancel Event
                // Update Command Permission
                // AutoMod Block Message
                // Create AutoMod Rule
                // Update AutoMod Rule
                // Cancel AutoMod Rule
                // Feature Item on Home
                // Remove Item from Home
                // Create Soundboard Sound
                // Update Soundboard Sound
                // Delete Soundboard Sound
        
                default:
                    // Find the event
                    var ev = _configuration.Events.First(x =>
                        x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
                    
                    // Get the guilds and users
                    var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
                    var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
                    
                    // Post default message
                    await destinationChannel.SendMessageAsync($"{TimestampTag.FromDateTime(log.CreatedAt.LocalDateTime)} {log.User.Username}: {log.Action} *(Unhandled audit log format)*");
                    _auditLogIdCache.Add(log.Id);
                    break;
            }
            
            // After handling the audit log, we add the ID to a cache list, so it won't be processed another time
            _auditLogIdCache.Add(log.Id);
        }
    }

    private async Task ChannelCreateAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (ChannelCreateAuditLogData)data.Data;

        // Get the guilds, channels and users
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} created a channel: {auditLog.ChannelName}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"**Type:** {auditLog.ChannelType}\n" +
                             $"**Channel ID:** {auditLog.ChannelId}\n" +
                             $"**NSFW:** {(auditLog.IsNsfw.HasValue ? (auditLog.IsNsfw.Value ? "Yes" : "No") : "n/a")}\n" +
                             $"**Audio bitrate:** {(auditLog.Bitrate.HasValue ? auditLog.Bitrate.Value + " kbps" : "n/a")}\n" +
                             $"**Slow mode:** {(auditLog.SlowModeInterval.HasValue ? auditLog.SlowModeInterval.Value + " s" : "n/a")}")
                            // TODO: add the permission overwrites
            .WithColor(Color.Green)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
    }
    
    private async Task ChannelUpdateAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (ChannelUpdateAuditLogData)data.Data;

        // Get the guilds, channels and users
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        //Prepare changelog
        var changelog = new StringBuilder();
        if (auditLog.Before.Name != auditLog.After.Name)
            changelog.AppendLine($"Changed name to: {auditLog.After.Name}");
        if (auditLog.Before.Bitrate.HasValue && auditLog.After.Bitrate.HasValue && (auditLog.Before.Bitrate.Value != auditLog.After.Bitrate.Value))
            changelog.AppendLine($"Changed bitrate to: {auditLog.After.Bitrate.Value} kbps");
        if (auditLog.Before.IsNsfw.HasValue && auditLog.After.IsNsfw.HasValue && (auditLog.Before.IsNsfw.Value != auditLog.After.IsNsfw.Value))
            changelog.AppendLine(auditLog.After.IsNsfw.Value ? "Turned on NSFW mode" : "Turned off NSFW mode");
        if (auditLog.Before.SlowModeInterval.HasValue && auditLog.After.SlowModeInterval.HasValue && 
            auditLog.Before.SlowModeInterval.Value != auditLog.After.SlowModeInterval.Value)
            changelog.AppendLine($"Changed slow mode delay to {auditLog.After.SlowModeInterval.Value} seconds");
        if (auditLog.Before.Topic != auditLog.After.Topic)
            changelog.AppendLine($"Changed the topic to: {auditLog.After.Topic}");


        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} updated a channel: {auditLog.Before.Name}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"{changelog.ToString()}")
            .WithColor(Color.Gold)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
    }
    
    private async Task ChannelDeleteAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (ChannelDeleteAuditLogData)data.Data;

        // Get the guilds, channels and users
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} deleted a channel: {auditLog.ChannelName}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"**Channel ID:** {auditLog.ChannelId}\n" +
                             $"**Type**: {auditLog.ChannelType}")
            .WithColor(Color.Red)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
    }
    
    private async Task OverwriteCreateAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (OverwriteCreateAuditLogData)data.Data;

        // Get the guilds, channels and users
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} created channel permissions for: {guild.GetChannel(auditLog.ChannelId)?.Name}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}")
            .WithColor(Color.Green)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
    }
    
    private async Task OverwriteUpdateAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (OverwriteUpdateAuditLogData)data.Data;

        // Get the guilds, channels and users
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} updated channel permissions for: {guild.GetChannel(auditLog.ChannelId)?.Name}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}")
            .WithColor(Color.Gold)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
    }

    private async Task OverwriteDeleteAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (OverwriteDeleteAuditLogData)data.Data;

        // Get the guilds, channels and users
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} deleted channel permissions for: {guild.GetChannel(auditLog.ChannelId)?.Name}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}")
            .WithColor(Color.Red)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
    }
    
    private async Task PruneAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (PruneAuditLogData)data.Data;

        // Get the guilds, channels and users
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} pruned {auditLog.MembersRemoved} members")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"**Threshold days**: {auditLog.PruneDays}")
            .WithColor(Color.Red)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
    }
    
    private async Task RoleUpdateAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (RoleUpdateAuditLogData)data.Data;

        // Get the guilds and channels
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // Compare the changes and add only the changed objects to a StringBuilder
        var changelog = new StringBuilder();
        if (auditLog.Before.Name != auditLog.After.Name)
            changelog.AppendLine($"Changed name to: {auditLog.After.Name}");
        if (auditLog.Before.Color != auditLog.After.Color)
            changelog.AppendLine($"Set color to: {auditLog.After.Color}");
        if (auditLog.Before.Hoist != auditLog.After.Hoist)
            if(auditLog.After.Hoist.HasValue)
                changelog.AppendLine(auditLog.After.Hoist.Value ? "Hoisted the role" : "Depressed the role");
        if (auditLog.Before.Mentionable != auditLog.After.Mentionable)
            if (auditLog.After.Mentionable.HasValue)
                changelog.AppendLine(auditLog.After.Mentionable.Value ? "Set role to mentionable" : "Set role to not-mentionable");
        if (auditLog.Before.Permissions.HasValue && auditLog.After.Permissions.HasValue)
        {
            var permsBefore = auditLog.Before.Permissions.Value;
            var permsAfter = auditLog.After.Permissions.Value;
            if (permsBefore.Administrator != permsAfter.Administrator)
                changelog.AppendLine(permsAfter.Administrator
                    ? "Gave Administrator privileges"
                    : "Revoked Administrator privileges");
            if (permsBefore.Connect != permsAfter.Connect)
                changelog.AppendLine(permsAfter.Connect ? "Gave 'Connect' permission" : "Revoked 'Connect' permission");
            if (permsBefore.Speak != permsAfter.Speak)
                changelog.AppendLine(permsAfter.Speak ? "Gave 'Speak' permission" : "Revoked 'Speak' permission");
            if (permsBefore.Stream != permsAfter.Stream)
                changelog.AppendLine(permsAfter.Stream ? "Gave 'Stream' permission" : "Revoked 'Stream' permission");
            if (permsBefore.AddReactions != permsAfter.AddReactions)
                changelog.AppendLine(permsAfter.AddReactions ? "Gave 'Add Reactions' permission" : "Revoked 'Add Reactions' permission");
            if (permsBefore.AttachFiles != permsAfter.AttachFiles)
                changelog.AppendLine(permsAfter.AttachFiles ? "Gave 'Attach Files' permission" : "Revoked 'Attach Files' permission");
            if (permsBefore.BanMembers != permsAfter.BanMembers)
                changelog.AppendLine(permsAfter.BanMembers ? "Gave 'Ban Members' permission" : "Revoked 'Ban Members' permission");
            if (permsBefore.ChangeNickname != permsAfter.ChangeNickname)
                changelog.AppendLine(permsAfter.ChangeNickname ? "Gave 'Change Nickname' permission" : "Revoked 'Change Nickname' permission");
            if (permsBefore.DeafenMembers != permsAfter.DeafenMembers)
                changelog.AppendLine(permsAfter.DeafenMembers ? "Gave 'Deafen Members' permission" : "Revoked 'Deafen Members' permission");
            if (permsBefore.EmbedLinks != permsAfter.EmbedLinks)
                changelog.AppendLine(permsAfter.EmbedLinks ? "Gave 'Embed Links' permission" : "Revoked 'Embed Links' permission");
            if (permsBefore.KickMembers != permsAfter.KickMembers)
                changelog.AppendLine(permsAfter.KickMembers ? "Gave 'Kick Members' permission" : "Revoked 'Kick Members' permission");
            if (permsBefore.ManageChannels != permsAfter.ManageChannels)
                changelog.AppendLine(permsAfter.ManageChannels ? "Gave 'Manage Channels' permission" : "Revoked 'Manage Channels' permission");
            if (permsBefore.ManageEvents != permsAfter.ManageEvents)
                changelog.AppendLine(permsAfter.ManageEvents ? "Gave 'Manage Events' permission" : "Revoked 'Manage Events' permission");
            if (permsBefore.ManageGuild != permsAfter.ManageGuild)
                changelog.AppendLine(permsAfter.ManageGuild ? "Gave 'Manage Guild' permission" : "Revoked 'Manage Guild' permission");
            if (permsBefore.ManageMessages != permsAfter.ManageMessages)
                changelog.AppendLine(permsAfter.ManageMessages ? "Gave 'Manage Messages' permission" : "Revoked 'Manage Messages' permission");
            if (permsBefore.ManageNicknames != permsAfter.ManageNicknames)
                changelog.AppendLine(permsAfter.ManageNicknames ? "Gave 'Manage Nicknames' permission" : "Revoked 'Manage Nicknames' permission");
            if (permsBefore.ManageRoles != permsAfter.ManageRoles)
                changelog.AppendLine(permsAfter.ManageRoles ? "Gave 'Manage Roles' permission" : "Revoked 'Manage Roles' permission");
            if (permsBefore.ManageThreads != permsAfter.ManageThreads)
                changelog.AppendLine(permsAfter.ManageThreads ? "Gave 'Manage Threads' permission" : "Revoked 'Manage Threads' permission");
            if (permsBefore.ManageWebhooks != permsAfter.ManageWebhooks)
                changelog.AppendLine(permsAfter.ManageWebhooks ? "Gave 'Manage Webhooks' permission" : "Revoked 'Manage Webhooks' permission");
            if (permsBefore.MentionEveryone != permsAfter.MentionEveryone)
                changelog.AppendLine(permsAfter.MentionEveryone ? "Role now can mention @everyone" : "Role now can't mention @everyone anymore.");
            if (permsBefore.ModerateMembers != permsAfter.ModerateMembers)
                changelog.AppendLine(permsAfter.ModerateMembers ? "Gave 'Moderate Members' permission" : "Revoked 'Moderate Members' permission");
            if (permsBefore.MoveMembers != permsAfter.MoveMembers)
                changelog.AppendLine(permsAfter.MoveMembers ? "Gave 'Move Members' permission" : "Revoked 'Move Members' permission");
            if (permsBefore.MuteMembers != permsAfter.MuteMembers)
                changelog.AppendLine(permsAfter.MuteMembers ? "Gave 'Mute Members' permission" : "Revoked 'Mute Members' permission");
            if (permsBefore.PrioritySpeaker != permsAfter.PrioritySpeaker)
                changelog.AppendLine(permsAfter.PrioritySpeaker ? "Role is now a priority speaker" : "Revoked 'Priority Speaker' privilege");
            if (permsBefore.SendMessages != permsAfter.SendMessages)
                changelog.AppendLine(permsAfter.SendMessages ? "Gave 'Send Messages' permission" : "Revoked 'Send Messages' privilege");
            if (permsBefore.ViewChannel != permsAfter.ViewChannel)
                changelog.AppendLine(permsAfter.ViewChannel ? "Gave 'View Channel' permission" : "Revoked 'View Channel' privilege");
            if (permsBefore.CreateInstantInvite != permsAfter.CreateInstantInvite)
                changelog.AppendLine(permsAfter.CreateInstantInvite ? "Role can now create instant invites" : "Role is now denied to create instant invites");
            if (permsBefore.CreatePrivateThreads != permsAfter.CreatePrivateThreads)
                changelog.AppendLine(permsAfter.CreatePrivateThreads ? "Role can now create private threads" : "Role is now denied to create private threads");
            if (permsBefore.CreatePublicThreads != permsAfter.CreatePublicThreads)
                changelog.AppendLine(permsAfter.CreatePublicThreads ? "Role can now create public threads" : "Role is now denied to create public threads");
            if (permsBefore.ReadMessageHistory != permsAfter.ReadMessageHistory)
                changelog.AppendLine(permsAfter.ReadMessageHistory ? "Gave 'Read Message History' permission" : "Revoked 'Read Message History' permission");
            if (permsBefore.RequestToSpeak != permsAfter.RequestToSpeak)
                changelog.AppendLine(permsAfter.RequestToSpeak ? "Gave 'Request To Speak' permission" : "Revoked 'Request To Speak' permission");
            if (permsBefore.StartEmbeddedActivities != permsAfter.StartEmbeddedActivities)
                changelog.AppendLine(permsAfter.StartEmbeddedActivities ? "Gave 'Start Embedded Activities' permission" : "Revoked 'Start Embedded Activities' permission");
            if (permsBefore.UseApplicationCommands != permsAfter.UseApplicationCommands)
                changelog.AppendLine(permsAfter.UseApplicationCommands ? "Gave 'Use Application Commands' permission" : "Revoked 'Use Application Commands' permission");
            if (permsBefore.UseExternalEmojis != permsAfter.UseExternalEmojis)
                changelog.AppendLine(permsAfter.UseExternalEmojis ? "Gave 'Use External Emojis' permission" : "Revoked 'Use External Emojis' permission");
            if (permsBefore.UseExternalStickers != permsAfter.UseExternalStickers)
                changelog.AppendLine(permsAfter.UseExternalStickers ? "Gave 'Use External Stickers' permission" : "Revoked 'Use External Stickers' permission");
            if (permsBefore.ViewAuditLog != permsAfter.ViewAuditLog)
                changelog.AppendLine(permsAfter.ViewAuditLog ? "Gave 'View Audit Log' permission" : "Revoked 'View Audit Log' permission");
            if (permsBefore.ViewGuildInsights != permsAfter.ViewGuildInsights)
                changelog.AppendLine(permsAfter.ViewGuildInsights ? "Gave 'View Guild Insights' permission" : "Revoked 'View Guild Insights' permission");
            if (permsBefore.ManageEmojisAndStickers != permsAfter.ManageEmojisAndStickers)
                changelog.AppendLine(permsAfter.ManageEmojisAndStickers ? "Gave 'Manage Emojis And Stickers' permission" : "Revoked 'Manage Emojis And Stickers' permission");
            if (permsBefore.SendMessagesInThreads != permsAfter.SendMessagesInThreads)
                changelog.AppendLine(permsAfter.SendMessagesInThreads ? "Gave 'Send Messages In Threads' permission" : "Revoked 'Send Messages In Threads' permission");
            if (permsBefore.UseVAD != permsAfter.UseVAD)
                changelog.AppendLine(permsAfter.UseVAD ? "Role can now use Voice-Activity-Detection." : "Role is now required to use Push-to-talk.");
            if (permsBefore.SendTTSMessages != permsAfter.SendTTSMessages)
                changelog.AppendLine(permsAfter.SendTTSMessages ? "Role can now send TTS messages" : "Role now is denied to send TTS messages.");
        }


        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} updated role: {auditLog.Before.Name}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"{changelog.ToString()}")
            .WithColor(Color.Red)
            .WithImageUrl(guild.IconUrl);

        await destinationChannel.SendMessageAsync(embed: emb.Build());
    }
    
    private async Task BanAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (BanAuditLogData)data.Data;

        // Get the guilds, channels and users
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} banned: {auditLog.Target.Username}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"**Reason:** {data.Reason}")
            .WithColor(Color.Orange)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
    }
    
    private async Task UnbanAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
    {
        // Find the event
        var ev = _configuration.Events.First(x =>
            x is { EventType: Configuration.EventType.AuditLog, IsEnabled: true } && x.SourceDiscordId == guild.Id);
        
        // Convert the Audit Log to it's class
        var auditLog = (UnbanAuditLogData)data.Data;

        // Get the guilds, channels and users
        var destinationGuild = _client.GetGuild(ev.DestinationDiscordId);
        var destinationChannel = destinationGuild.GetTextChannel(ev.DestinationChannelId);
        
        // create the embed for the message
        var emb = new EmbedBuilder();
        emb.WithTitle($"{data.User.Username} unbanned: {auditLog.Target.Username}")
            .WithDescription($"**Date/Time:** {TimestampTag.FromDateTime(data.CreatedAt.LocalDateTime)}\n" +
                             $"**Reason:** {data.Reason}")
            .WithColor(Color.Orange)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
    }
    
    private async Task MemberRoleAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
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
                             changelog.ToString())
            .WithColor(Color.LightGrey)
            .WithThumbnailUrl(string.IsNullOrEmpty(data.User.GetAvatarUrl()) ? data.User.GetDefaultAvatarUrl() : data.User.GetAvatarUrl());

        await destinationChannel.SendMessageAsync(embed: emb.Build());
    }
    
    private async Task KickAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
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
    }
    
    private async Task MemberUpdateAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
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
    }
   
    private async Task GuildUpdateAuditLogHandler(RestAuditLogEntry data, SocketGuild guild)
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
    }
    
    private async Task InviteCreatedLogHandler(RestAuditLogEntry data,SocketGuild guild)
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
    }
    
    private async Task InviteDeleteAuditLogHandler(RestAuditLogEntry data,SocketGuild guild)
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