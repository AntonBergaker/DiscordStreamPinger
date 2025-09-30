namespace StreamingBot;

public record Config(ConfigDiscord Discord, ConfigTwitch Twitch, ConfigYouTube YouTube, Dictionary<string, ConfigStreamer> Streamers);

public record ConfigDiscord(string Token, ConfigDiscordServer[] Servers);

public record ConfigDiscordServer(ulong GuildId, ulong StatusChannel, ConfigDiscordWelcome Welcome, ulong StreamingRole, ulong PingedRole, string[] Streamers);

public record ConfigDiscordWelcome(ulong Channel, string Title, string Description, string Color);

public record ConfigYouTube(string ClientId, string ApiKey);

public record ConfigTwitch(string ClientId, string ClientSecret);

public record ConfigStreamer(ulong DiscordId, string? YouTube, string? Picarto, string? Twitch);