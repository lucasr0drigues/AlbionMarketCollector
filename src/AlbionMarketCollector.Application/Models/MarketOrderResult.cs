namespace AlbionMarketCollector.Application.Models;

public sealed record MarketOrderResult(
    int ServerId,
    string LocationId,
    string LocationName,
    long AlbionOrderId,
    string ItemTypeId,
    string ItemName,
    int QualityLevel,
    int EnchantmentLevel,
    long UnitPriceSilver,
    long Amount,
    string OrderType,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc,
    double AgeMinutes);
