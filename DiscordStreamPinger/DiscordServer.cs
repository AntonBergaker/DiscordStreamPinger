using Discord;
using Discord.Rest;
using Discord.WebSocket;
using DiscordStreamPinger.ActivityProviders;
using StreamingBot;
using System.Globalization;

namespace DiscordStreamPinger;
internal class DiscordServer {
    private SocketGuild? _guild;

    private SocketTextChannel? _statusChannel;
    private SocketTextChannel? _welcomeChannel;
    private ulong roleId;

    private ConfigDiscordServer _config;

    private readonly Dictionary<string, Streamer> _streamers;
    private readonly Dictionary<Streamer, StreamerMessage> _messages;

    private record StreamerMessage(RestUserMessage DiscordMessage, Dictionary<IStreamerActivity, StreamData> RegisteredActivities);

    public DiscordServer(ConfigDiscordServer serverConfig) {
        _config = serverConfig;

        _streamers = [];
        _messages = [];
    }

    public void Init(DiscordSocketClient client, Dictionary<string, Streamer> streamers) {
        _guild = client.GetGuild(_config.GuildId);
        _statusChannel = _guild.GetChannel(_config.StatusChannel) as SocketTextChannel;
        
        if (_config.Welcome != null) {
            _welcomeChannel = (SocketTextChannel)_guild.GetChannel(_config.Welcome.Channel);
        }

        foreach (var streamerId in _config.Streamers) {
            if (streamers.TryGetValue(streamerId, out var streamer) == false) {
                Console.WriteLine($"Failed to find streamer \"{streamerId}\" in the config.");
                continue;
            }
            
            _streamers.Add(streamerId, streamer);
            foreach (var activity in streamer.Activities) {
                activity.StreamingStatusChanged += () => Activity_StreamingStatusChanged(streamer);
            }
        }
    }

    private async Task Activity_StreamingStatusChanged(Streamer user) {
        var allOnline = user.Activities.Where(x => x.Status == ActivityStatus.Online).ToArray();
        var isOnline = allOnline.Length > 0;

        await UpdateRoles(user, isOnline);

        if (_statusChannel == null) {
            return;
        }

        // If already has message
        if (_messages.TryGetValue(user, out var message)) {
            // Clear message, went offline
            if (isOnline == false) {
                _messages.Remove(user);
                return;
            }

            foreach (var onlineActivity in allOnline) {
                message.RegisteredActivities[onlineActivity] = onlineActivity.Stream!;
            }

            // Update message
            await message.DiscordMessage.ModifyAsync((props) => {
                props.Embeds = GetEmbeds(message.RegisteredActivities);
            });
            return;
        }

        if (isOnline) {
            // If new message
            string? text = null;
            if (_config.PingedRole != 0) {
                text = $"<@&{_config.PingedRole}>";
            }

            var activities = new Dictionary<IStreamerActivity, StreamData>();

            foreach (var onlineActivity in allOnline) {
                activities[onlineActivity] = onlineActivity.Stream!;
            }

            var discordMessage = await _statusChannel.SendMessageAsync(text, embeds: GetEmbeds(activities));

            _messages.Add(user, new(discordMessage, activities));
        }
    }

    private async Task UpdateRoles(Streamer user, bool isOnline) {
        if (_guild != null) {
            var discordUser = _guild.GetUser(user.DiscordId);
            var role = _guild.Roles.FirstOrDefault(x => x.Id == _config.StreamingRole);

            if (discordUser != null && role != null) {
                try {
                    if (isOnline == false) {
                        await discordUser.RemoveRoleAsync(role);
                    } else if (isOnline) {
                        await discordUser.AddRoleAsync(role);
                    }
                } catch (Exception ex) {
                    Console.WriteLine(ex);
                }
            }
        }
    }

    private Embed[] GetEmbeds(Dictionary<IStreamerActivity, StreamData> message) {
        return message.Select(x => BuildEmbed(x.Key, x.Value)).ToArray();
    }

    private Embed BuildEmbed(IStreamerActivity activity, StreamData stream) {
        return new EmbedBuilder()
            .WithTitle($"{activity.Username} went live!")
            .WithDescription($"Streaming: **{stream.Game}**\n\n{stream.Title}")
            .WithColor(0x3498db)
            .WithUrl(stream.StreamUrl)
            .WithThumbnailUrl(activity.AvatarUrl)
            .Build();
    }

    public async Task SendWelcomeMessage(SocketGuildUser user) {

        if (_welcomeChannel == null || _config.Welcome == null) {
            return;
        }


        var embed = new EmbedBuilder()
            .WithTitle(_config.Welcome.Title.Replace("{user}", user.Mention))
            .WithColor(uint.Parse(_config.Welcome.Color, NumberStyles.HexNumber))
            .WithDescription(_config.Welcome.Description.Replace("{user}", user.Mention))
        ;

        await _welcomeChannel.SendMessageAsync(embed: embed.Build());
    }
}
