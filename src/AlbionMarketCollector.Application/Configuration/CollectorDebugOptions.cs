namespace AlbionMarketCollector.Application.Configuration;

public sealed class CollectorDebugOptions
{
    public bool LogRawPayloads { get; set; }

    public bool LogDecodedMessages { get; set; } = true;

    public string? DumpCapturedPayloadsPath { get; set; }
}
