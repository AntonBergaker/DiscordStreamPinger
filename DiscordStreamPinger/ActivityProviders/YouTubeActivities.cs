using DiscordStreamPinger.ActivityProviders;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using StreamData = DiscordStreamPinger.ActivityProviders.StreamData;

namespace StreamingBot.ActivityProviders;
internal class YouTubeActivities {
    private readonly YouTubeService _youTubeService;
    private readonly List<YouTubeChannel> _channels;

    public YouTubeActivities(ConfigYouTube config) {
        _youTubeService = new YouTubeService(new BaseClientService.Initializer() {
            ApiKey = config.ApiKey,
            ApplicationName = config.ClientId,
        });
        _channels = new();
    }

    private enum YouTubeStatus {
        Unknown,
        Live,
        Offline,
    }

    private class YouTubeChannel(string youtubeId, string username, string avatarUrl) : BaseStreamerActivity(username, avatarUrl) {
        public string YoutubeId { get; } = youtubeId;
        
        public YouTubeStatus YouTubeStatus { get; set; } = YouTubeStatus.Unknown;
    }

    public async Task<IStreamerActivity?> AddChannel(string handle) {
        var youTubeChannel = await GetChannel(handle);
        if (youTubeChannel == null) {
            return null;
        }

        var channel = new YouTubeChannel(youTubeChannel.Id, youTubeChannel.Snippet.Title, youTubeChannel.Snippet.Thumbnails.Medium.Url);

        lock (_channels) {
            _channels.Add(channel);
        }

        return channel;
    }

    private async Task<Channel?> GetChannel(string handle) {
        var channelRequest = _youTubeService.Channels.List(new(["id", "snippet"]));
        channelRequest.ForHandle = handle;
        channelRequest.MaxResults = 1;

        var channelResult = await channelRequest.ExecuteAsync();
        
        if (channelResult.Items == null || channelResult.Items.Count == 0) {
            return null;
        }

        return channelResult.Items[0];
    }

    private static ActivityStatus YouTubeStatusToActivityStatus(YouTubeStatus status) {
        return status switch {
            YouTubeStatus.Live => ActivityStatus.Online,
            YouTubeStatus.Offline => ActivityStatus.Offline,
            _ => ActivityStatus.Unknown,
        };
    }

    public async Task Start() {
        int fails = 0;
        while (fails < 1000) {
            try {
                List<YouTubeChannel> channels;
                lock (_channels) {
                    channels = _channels.ToList();
                }

                foreach (var channel in channels) {
                    var youTubeStream = await GetLivestream(channel.YoutubeId);

                    var newStatus = youTubeStream == null ? YouTubeStatus.Offline : YouTubeStatus.Live;
                    var oldStatus = channel.YouTubeStatus;

                    if (newStatus != oldStatus && oldStatus != YouTubeStatus.Unknown) {
                        StreamData? activity = null;
                        if (newStatus == YouTubeStatus.Live && youTubeStream != null) {
                            var (id, content, topic) = youTubeStream.Value;
                            activity = new(
                                StreamUrl: $"https://youtu.be/{id}",
                                Game: topic.TopicCategories.FirstOrDefault() ?? "Some game",
                                Title: content.Title
                            );
                        }

                        // If online, update account data
                        var youTubeChannelData = await GetChannel(channel.YoutubeId);
                        if (youTubeChannelData != null) {
                            channel.AvatarUrl = youTubeChannelData.Snippet.Thumbnails.Medium.Url;
                            channel.Username = youTubeChannelData.Snippet.Title;
                        }

                        await channel.SetStreamingStatus(
                            YouTubeStatusToActivityStatus(newStatus),
                            activity
                        );

                    }

                    channel.YouTubeStatus = newStatus;
                }
                fails = 0;
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
                fails++;
            }

            await Task.Delay(865 * 1000 * (fails + 1));
        }
    }

    private async Task<(string Id, VideoSnippet Details, VideoTopicDetails Topic)?> GetLivestream(string youtubeId) {
        var searchRequest = _youTubeService.Search.List(new(["id"]));
        searchRequest.EventType = SearchResource.ListRequest.EventTypeEnum.Live;
        searchRequest.MaxResults = 1;
        searchRequest.Type = "video";
        searchRequest.ChannelId = youtubeId;

        var searchResult = await searchRequest.ExecuteAsync();

        if (searchResult.Items.Count == 0) {
            return null;
        }
        
        var id = searchResult.Items[0].Id;
        var videoRequest = _youTubeService.Videos.List(new(["snippet", "topicDetails"]));
        videoRequest.Id = id.VideoId;

        var videoResult = await videoRequest.ExecuteAsync();
        
        if (videoResult.Items.Count == 0) {
            return null;
        }
        return (id.VideoId, videoResult.Items[0].Snippet, videoResult.Items[0].TopicDetails);
    }
}
