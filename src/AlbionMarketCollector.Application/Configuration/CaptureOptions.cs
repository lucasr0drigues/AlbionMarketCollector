namespace AlbionMarketCollector.Application.Configuration;

public sealed class CaptureOptions
{
    public bool EnableLiveCapture { get; set; }

    public int Port { get; set; } = 5056;

    public string[] ListenDevices { get; set; } = [];

    public string? ReplayFixturePath { get; set; }

    public int ReplayDelayMilliseconds { get; set; }
}
