using Discord;
using Discord.WebSocket;
using DiscordStreamPinger;
using DiscordStreamPinger.ActivityProviders;
using StreamingBot.ActivityProviders;
using System.Globalization;

namespace StreamingBot;
public class DiscordBot {
    private readonly DiscordSocketClient _client;

    private readonly TwitchActivities _twitch;
    private readonly PicartoActivities _picarto;
    private readonly YouTubeActivities _youTube;
    private readonly Config _config;

    private Dictionary<ulong, DiscordServer> _discordServers;

    private readonly Dictionary<string, Streamer> _streamers;

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

        _discordServers = [];
        _streamers = new();

        foreach (var serverConfig in config.Discord.Servers) {
            _discordServers.Add(serverConfig.GuildId, new DiscordServer(serverConfig));
        }
    }

    private async Task Client_UserJoined(SocketGuildUser user) {
        if (user.IsBot || user.IsWebhook) {
            return;
        }

        if (_discordServers.TryGetValue(user.Guild.Id, out var server) == false) {
            return;
        }

        await server.SendWelcomeMessage(user);
    }

    private bool _hasInit = false;

    private async Task Client_Ready() {
        if (_hasInit) {
            return;
        }

        foreach ((var streamerId, var configStreamer) in _config.Streamers) {
            var streamer = new Streamer(configStreamer.DiscordId);

            if (configStreamer.YouTube != null) {
                var activity = await _youTube.AddChannel(configStreamer.YouTube);
                RegisterActivity(streamer, activity, "YouTube", configStreamer.YouTube);
            }
            if (configStreamer.Picarto != null) {
                var activity = await _picarto.AddAccount(configStreamer.Picarto);
                RegisterActivity(streamer, activity, "picarto", configStreamer.Picarto);
            }
            if (configStreamer.Twitch != null) {
                var activity = await _twitch.AddAccount(configStreamer.Twitch);
                RegisterActivity(streamer, activity, "twitch", configStreamer.Twitch);
            }

            _streamers.Add(streamerId, streamer);
        }

        foreach (var discordServer in _discordServers.Values) {
            discordServer.Init(_client, _streamers);
        }
    }


    private void RegisterActivity(Streamer user, IStreamerActivity? activity, string service, string serviceParameters) {
        if (activity == null) {
            Console.WriteLine($"Failed to add service {service} account {serviceParameters}");
            return;
        }
        user.Activities.Add(activity);
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
