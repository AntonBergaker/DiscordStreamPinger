using Discord.Rest;
using DiscordStreamPinger.ActivityProviders;

namespace StreamingBot;

internal class Streamer(ulong discordId) {
    public ulong DiscordId { get; } = discordId;

    public List<IStreamerActivity> Activities { get; } = new();
}
