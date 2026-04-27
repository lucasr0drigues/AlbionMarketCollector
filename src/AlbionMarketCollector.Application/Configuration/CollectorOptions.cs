namespace AlbionMarketCollector.Application.Configuration;

public sealed class CollectorOptions
{
    public string CollectorKey { get; set; } = "default";

    public int ChannelCapacity { get; set; } = 2048;

    public CaptureOptions Capture { get; set; } = new();

    public PersistenceOptions Persistence { get; set; } = new();

    public CollectorDebugOptions Debug { get; set; } = new();
}
