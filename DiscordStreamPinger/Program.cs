using StreamingBot;
using System.Text.Json;

var text = File.ReadAllText("./config.json");
var config = JsonSerializer.Deserialize<Config>(text, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
var bot = new DiscordBot(config!);
await bot.Start();

await Task.Delay(-1);