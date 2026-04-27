namespace AlbionMarketCollector.Domain.Models;

public sealed class PendingMarketHistoryRequest
{
    public ulong MessageId { get; set; }

    public string ItemTypeId { get; set; } = string.Empty;

    public byte QualityLevel { get; set; }

    public byte Timescale { get; set; }

    public DateTimeOffset CapturedAtUtc { get; set; }
}
