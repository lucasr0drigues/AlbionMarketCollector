using AlbionMarketCollector.Domain.Enums;

namespace AlbionMarketCollector.Application.Models;

public sealed class MarketOrderQuery
{
    public string? LocationId { get; init; }

    public string? ItemSearch { get; init; }

    public MarketOrderType? OrderType { get; init; }

    public int? MaxAgeMinutes { get; init; }

    public int Limit { get; init; } = 100;
}
