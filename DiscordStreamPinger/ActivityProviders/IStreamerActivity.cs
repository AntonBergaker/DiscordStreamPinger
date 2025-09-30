namespace DiscordStreamPinger.ActivityProviders;

public enum ActivityStatus {
    Unknown,
    Offline,
    Online,
}

interface IStreamerActivity {
    delegate Task StreamingStatusChangedEvent();
    event StreamingStatusChangedEvent? StreamingStatusChanged;

    string Username { get; }
    string AvatarUrl { get; }
    ActivityStatus Status { get; }
    StreamData? Stream { get; }
}

abstract class BaseStreamerActivity(string username, string avatarUrl) : IStreamerActivity {
    public string Username { get; set; } = username;
    public string AvatarUrl { get; set; } = avatarUrl;
    public ActivityStatus Status { get; protected set; }
    public StreamData? Stream { get; protected set; }

    public event IStreamerActivity.StreamingStatusChangedEvent? StreamingStatusChanged;

    public async Task SetStreamingStatus(ActivityStatus newStatus, StreamData? stream) {
        if (StreamingStatusChanged == null) {
            return;
        }

        Status = newStatus;
        Stream = stream;
        await StreamingStatusChanged.Invoke();
    }
}
