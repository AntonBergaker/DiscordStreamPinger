namespace StreamingBot;

public enum ActivityStatus {
    Unknown,
    Offline,
    Online,
}

interface IActivity {
    delegate Task StreamingStatusChangedEvent();
    event StreamingStatusChangedEvent? StreamingStatusChanged;

    string Username { get; }
    string AvatarUrl { get; }
    ActivityStatus Status { get; }
    StreamingStream? Stream { get; }
}

abstract class BaseActivity(string username, string avatarUrl) : IActivity {
    public string Username { get; set; } = username;
    public string AvatarUrl { get; set; } = avatarUrl;
    public ActivityStatus Status { get; protected set; }
    public StreamingStream? Stream { get; protected set; }

    public event IActivity.StreamingStatusChangedEvent? StreamingStatusChanged;

    public async Task SetStreamingStatus(ActivityStatus newStatus, StreamingStream? stream) {
        if (StreamingStatusChanged == null) {
            return;
        }

        Status = newStatus;
        Stream = stream;
        await StreamingStatusChanged.Invoke();
    }
}
