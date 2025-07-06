using Discord;
using Discord.WebSocket;
using StreamingBot.ActivityProviders;
using System.Globalization;

namespace StreamingBot;
public class DiscordBot {
    private readonly DiscordSocketClient _client;

    private readonly TwitchActivities _twitch;
    private readonly PicartoActivities _picarto;
    private readonly YouTubeActivities _youTube;
    private readonly Config _config;

    private SocketGuild? _guild;
    private SocketTextChannel? _statusChannel;
    private SocketTextChannel? _welcomeChannel;

    private List<User> _users;

    public DiscordBot(Config config) {
        _config = config;
        _client = new DiscordSocketClient(new() { AlwaysDownloadUsers = true, 
            GatewayIntents = 
                (GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers) & 
                ~(GatewayIntents.GuildScheduledEvents | GatewayIntents.GuildInvites)
            
        });
        _client.Log += Client_Log;
        _client.Ready += Client_Ready;
        _client.UserJoined += Client_UserJoined;

        _twitch = new TwitchActivities(config.Twitch);

        _picarto = new PicartoActivities();

        _youTube = new YouTubeActivities(config.YouTube);

        _users = new();
    }

    private async Task Client_UserJoined(SocketGuildUser user) {
        if (user.IsBot || user.IsWebhook) {
            return;
        }

        var welcome = _config.Discord.Welcome;
        if (welcome == null) {
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(welcome.Title.Replace("{user}", user.Mention))
            .WithColor(uint.Parse(_config.Discord.Welcome.Color, NumberStyles.HexNumber))
            .WithDescription(welcome.Description.Replace("{user}", user.Mention))
        ;

        if (_welcomeChannel == null) {
            return;
        }

        await _welcomeChannel.SendMessageAsync(embed: embed.Build());
    }

    private bool _hasInit = false;

    private async Task Client_Ready() {
        if (_hasInit) {
            return;
        }

        _hasInit = true;
        _guild = _client.GetGuild(_config.Discord.GuildId);
        _statusChannel = (SocketTextChannel)_guild.GetChannel(_config.Discord.StatusChannel);
        if (_config.Discord.Welcome != null) {
            _welcomeChannel = (SocketTextChannel)_guild.GetChannel(_config.Discord.Welcome.Channel);
        }

        foreach (var configUser in _config.Users) {
            var user = new User(configUser.DiscordId);

            if (configUser.YouTube != null) {
                var activity = await _youTube.AddChannel(configUser.YouTube);
                RegisterActivity(user, activity, "YouTube", configUser.YouTube);
            }
            if (configUser.Picarto != null) {
                var activity = await _picarto.AddAccount(configUser.Picarto);
                RegisterActivity(user, activity, "picarto", configUser.Picarto);
            }
            if (configUser.Twitch != null) {
                var activity = await _twitch.AddAccount(configUser.Twitch);
                RegisterActivity(user, activity, "twitch", configUser.Twitch);
            }

            _users.Add(user);
        }
    }

    private Embed[] GetEmbeds(User user) {
        return user.Activities.Where(x => x.Status == ActivityStatus.Online).Select(x => BuildEmbed(x)).ToArray();
    }

    private Embed BuildEmbed(IActivity activity) {
        var stream = activity.Stream!;
        return new EmbedBuilder()
            .WithTitle($"{activity.Username} went live!")
            .WithDescription($"Streaming: **{stream.Game}**\n\n{stream.Title}")
            .WithColor(0x3498db)
            .WithUrl(stream.StreamUrl)
            .WithThumbnailUrl(activity.AvatarUrl)
            .Build();
    }

    private void RegisterActivity(User user, IActivity? activity, string service, string serviceParameters) {
        if (activity == null) {
            Console.WriteLine($"Failed to add service {service} account {serviceParameters}");
            return;
        }
        user.Activities.Add(activity);

        activity.StreamingStatusChanged += () => Activity_StreamingStatusChanged(user);
    }

    private async Task Activity_StreamingStatusChanged(User user) {
        var isOnline = user.Activities.Any(x => x.Status == ActivityStatus.Online);
        
        await UpdateRoles(user, isOnline);

        // If already has message
        if (user.DiscordMessage != null) {
            // Clear message, went offline
            if (isOnline == false) {
                user.DiscordMessage = null;
                return;
            }

            // Update message
            await user.DiscordMessage.ModifyAsync((props) => {
                props.Embeds = GetEmbeds(user);
            });
            return;
        }

        if (_statusChannel == null) {
            return;
        }

        if (isOnline) {
            // If new message
            string? text = null;
            if (_config.Discord.PingedRole != 0) {
                text = $"<@&{_config.Discord.PingedRole}>";
            }

            var message = await _statusChannel.SendMessageAsync(text, embeds: GetEmbeds(user));
            user.DiscordMessage = message;
        }
    }

    private async Task UpdateRoles(User user, bool isOnline) {
        if (_guild != null) {
            var discordUser = _guild.GetUser(user.DiscordId);
            var role = _guild.Roles.FirstOrDefault(x => x.Id == _config.Discord.StreamingRole);

            if (discordUser != null && role != null) {
                try {
                    if (isOnline == false) {
                        await discordUser.RemoveRoleAsync(role);
                    }
                    else if (isOnline) {
                        await discordUser.AddRoleAsync(role);
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine(ex);
                }
            }
        }
    }


    private Task Client_Log(LogMessage arg) {
        if (arg.Exception != null) {
            Console.WriteLine(arg.Exception);
        }
        if (arg.Message != null) {
            Console.WriteLine(arg.Message);
        }
        return Task.CompletedTask;
    }

    public async Task Start() {
        _ = _twitch.Start();
        _ = _picarto.Start();
        _ = _youTube.Start();
        await _client.LoginAsync(TokenType.Bot, _config.Discord.Token);
        await _client.StartAsync();
    }
}
