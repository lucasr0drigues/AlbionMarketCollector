namespace AlbionMarketCollector.Domain.Models;

public sealed class MarketHistoryPoint
{
    public int ServerId { get; set; }

    public string LocationId { get; set; } = string.Empty;

    public string ItemTypeId { get; set; } = string.Empty;

    public byte QualityLevel { get; set; }

    public byte Timescale { get; set; }

    public DateTimeOffset DataTimestampUtc { get; set; }

    public long ItemAmount { get; set; }

    public ulong SilverAmount { get; set; }

    public DateTimeOffset ObservedAtUtc { get; set; }
}
