using AlbionMarketCollector.Domain.Enums;

namespace AlbionMarketCollector.Domain.Models;

public sealed class MarketOrder
{
    public int ServerId { get; set; }

    public string LocationId { get; set; } = string.Empty;

    public long AlbionOrderId { get; set; }

    public string ItemTypeId { get; set; } = string.Empty;

    public string ItemGroupTypeId { get; set; } = string.Empty;

    public int QualityLevel { get; set; }

    public int EnchantmentLevel { get; set; }

    public long UnitPriceSilver { get; set; }

    public long Amount { get; set; }

    public MarketOrderType OrderType { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public DateTimeOffset ObservedAtUtc { get; set; }
}
