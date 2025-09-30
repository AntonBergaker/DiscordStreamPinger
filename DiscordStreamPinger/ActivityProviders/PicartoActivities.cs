using DiscordStreamPinger.ActivityProviders;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace StreamingBot.ActivityProviders;
internal class PicartoActivities {

    private enum PicartoUserStatus {
        Unknown,
        Offline,
        Online
    }

    private static ActivityStatus PicartoStatusToStreamingStatus(PicartoUserStatus status) => status switch {
        PicartoUserStatus.Offline => ActivityStatus.Offline,
        PicartoUserStatus.Online => ActivityStatus.Online,
        _ => ActivityStatus.Unknown
    };


    private readonly List<PicartoActivity> _activities;

    public PicartoActivities() {
        _activities = new();
    }

    public async Task Start() {
        int fails = 0;
        while (fails < 1000) {
            try {
                List<PicartoActivity> activities;
                lock (_activities) {
                    activities = _activities.ToList();
                }

                var client = new HttpClient();

                foreach (var user in activities) {
                    var json = await RequestAccountData(client, user.Username);

                    var isOnline = json["online"]?.GetValue<bool>() ?? false;

                    var newStatus = isOnline ? PicartoUserStatus.Online : PicartoUserStatus.Offline;
                    var oldStatus = user.PicartoStatus;

                    var games = json["category"] as JsonArray ?? [];
                    var game = games.FirstOrDefault()?.GetValue<string>() ?? "No category detected";

                    if (newStatus != oldStatus && oldStatus != PicartoUserStatus.Unknown) {
                        StreamData? activity = null;
                        if (newStatus == PicartoUserStatus.Online) {
                            activity = new(
                                StreamUrl: $"https://picarto.tv/{user.Username}",
                                Game: game,
                                Title: json["title"]?.GetValue<string>() ?? "Some title"
                            );
                        }

                        // If online, update account data
                        if (json.TryGetPropertyValue("name", out var nameNode)) {
                            user.Username = nameNode?.GetValue<string>() ?? user.Username;
                        }
                        if (json.TryGetPropertyValue("avatar", out var avatarNode)) {
                            user.AvatarUrl = avatarNode?.GetValue<string>() ?? user.AvatarUrl;
                        }

                        await user.SetStreamingStatus(
                            PicartoStatusToStreamingStatus(newStatus),
                            activity
                        );
                        
                    }

                    user.PicartoStatus = newStatus;
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

    private class PicartoActivity(string username, string avatarUrl) : BaseStreamerActivity(username, avatarUrl) {
        public PicartoUserStatus PicartoStatus { get; set; } = PicartoUserStatus.Unknown;
    }

    public async Task<IStreamerActivity?> AddAccount(string accountName) {
        JsonObject json;
        try {
            json = await RequestAccountData(new(), accountName);

        }
        catch {
            return null;
        }

        var user = new PicartoActivity(
            json["name"]?.GetValue<string>() ?? "Unknown Name",
            json["avatar"]?.GetValue<string>() ?? ""
        );

        lock (_activities) {
            _activities.Add(user);
        }

        return user;
    }

    private async Task<JsonObject> RequestAccountData(HttpClient client, string accountName) {
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd(new("curl/7.16.3 (powerpc-apple-darwin9.0) libcurl/7.16.3"));
        var response = await client.GetFromJsonAsync<JsonObject>($"https://api.picarto.tv/api/v1/channel/name/{accountName}");

        if (response == null) {
            throw new NullReferenceException();
        }
        return response;
    }
}
