using DiscordStreamPinger.ActivityProviders;
using TwitchLib.Api;
using TwitchApiUser = TwitchLib.Api.Helix.Models.Users.GetUsers.User;

namespace StreamingBot.ActivityProviders;
internal class TwitchActivities {
    private enum TwitchUserStatus {
        Unknown,
        Offline,
        Online
    }

    private class TwitchUser(string twitchId, string name, string avatarUrl) : BaseStreamerActivity(name, avatarUrl) {
        public string TwitchId { get; } = twitchId;
        public TwitchUserStatus TwitchStatus { get; set; } = TwitchUserStatus.Unknown;
    }

    private readonly TwitchAPI _api;
    private readonly List<TwitchUser> _users = new();

    public TwitchActivities(ConfigTwitch config) {
        _api = new();
        _api.Settings.ClientId = config.ClientId;
        _api.Settings.Secret = config.ClientSecret;
    }

    public async Task Start() {
        int fails = 0;
        while (fails < 1000) {
            try {
                List<TwitchUser> users;
                lock (_users) {
                    users = _users.ToList();
                }
                var streams = await _api.Helix.Streams.GetStreamsAsync(userIds: users.Select(x => x.TwitchId).ToList());

                var onlineStreams = streams.Streams.ToDictionary(x => x.UserId);

                foreach (var user in users) {
                    var isOnline = onlineStreams.TryGetValue(user.TwitchId, out var stream);
                    var newStatus = isOnline ? TwitchUserStatus.Online : TwitchUserStatus.Offline;
                    var oldStatus = user.TwitchStatus;

                    if (newStatus != oldStatus && oldStatus != TwitchUserStatus.Unknown) {
                        StreamData? activity = null;
                        if (newStatus == TwitchUserStatus.Online && stream != null) {
                            activity = new(
                                StreamUrl: $"https://twitch.tv/{stream.UserLogin}",
                                Game: stream.GameName,
                                Title: stream.Title
                            );
                        }

                        // Update icon on change


                        await user.SetStreamingStatus(
                            TwitchStatusToStreamingStatus(newStatus),
                            activity
                        );

                    }

                    user.TwitchStatus = newStatus;
                }
                fails = 0;
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
                fails++;
            }

            await Task.Delay(10000 * (fails + 1));
        }
    }

    private ActivityStatus TwitchStatusToStreamingStatus(TwitchUserStatus status) => status switch {
        TwitchUserStatus.Offline => ActivityStatus.Offline,
        TwitchUserStatus.Online => ActivityStatus.Online,
        _ => ActivityStatus.Unknown
    };

    public async Task<IStreamerActivity?> AddAccount(string twitchAccount) {
        var twitchUser = await GetUser(twitchAccount);

        if (twitchUser == null) {
            return null;
        }

        var user = new TwitchUser(twitchUser.Id, twitchUser.DisplayName, twitchUser.ProfileImageUrl);

        lock (_users) {
            _users.Add(user);
        }
        return user;
    }

    private async Task<TwitchApiUser?> GetUser(string twitchUsername) {
        var usersResult = await _api.Helix.Users.GetUsersAsync(logins: [twitchUsername]);
        if (usersResult.Users.Length <= 0) {
            return null;
        }

        return usersResult.Users[0];
    }
}
