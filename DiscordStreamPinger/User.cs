using Discord.Rest;

namespace StreamingBot;

internal class User(ulong discordId) {
    public ulong DiscordId { get; } = discordId;

    public List<IActivity> Activities { get; } = new();

    public RestUserMessage? DiscordMessage { get; set; }
}
