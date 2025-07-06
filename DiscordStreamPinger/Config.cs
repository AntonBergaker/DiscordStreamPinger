namespace StreamingBot;

public record Config(ConfigDiscord Discord, ConfigTwitch Twitch, ConfigYouTube YouTube, ConfigUser[] Users);

public record ConfigDiscord(string Token, ulong GuildId, ulong StatusChannel, ConfigDiscordWelcome Welcome, ulong StreamingRole, ulong PingedRole);

public record ConfigDiscordWelcome(ulong Channel, string Title, string Description, string Color);

public record ConfigYouTube(string ClientId, string ApiKey);

public record ConfigTwitch(string ClientId, string ClientSecret);

public record ConfigUser(ulong DiscordId, string? YouTube, string? Picarto, string? Twitch);