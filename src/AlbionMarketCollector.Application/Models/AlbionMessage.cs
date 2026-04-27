namespace AlbionMarketCollector.Application.Models;

public abstract record AlbionMessage(DateTimeOffset ObservedAtUtc);

public sealed record EncryptedMarketDataMessage(
    DateTimeOffset ObservedAtUtc) : AlbionMessage(ObservedAtUtc);

public sealed record JoinResponseMessage(
    string? CharacterId,
    string? CharacterName,
    string? LocationId,
    DateTimeOffset ObservedAtUtc) : AlbionMessage(ObservedAtUtc);

public sealed record GetGameServerByClusterMessage(
    string? ZoneId,
    DateTimeOffset ObservedAtUtc) : AlbionMessage(ObservedAtUtc);

public sealed record AuctionGetOffersRequestMessage(
    DateTimeOffset ObservedAtUtc) : AlbionMessage(ObservedAtUtc);

public sealed record AuctionGetOffersResponseMessage(
    IReadOnlyList<string> MarketOrders,
    DateTimeOffset ObservedAtUtc) : AlbionMessage(ObservedAtUtc);

public sealed record AuctionGetRequestsResponseMessage(
    IReadOnlyList<string> MarketOrders,
    DateTimeOffset ObservedAtUtc) : AlbionMessage(ObservedAtUtc);

public sealed record AuctionGetItemAverageStatsRequestMessage(
    int ItemId,
    byte QualityLevel,
    byte Timescale,
    uint EnchantmentLevel,
    ulong MessageId,
    DateTimeOffset ObservedAtUtc) : AlbionMessage(ObservedAtUtc);

public sealed record AuctionGetItemAverageStatsResponseMessage(
    IReadOnlyList<long> ItemAmounts,
    IReadOnlyList<ulong> SilverAmounts,
    IReadOnlyList<ulong> Timestamps,
    ulong MessageId,
    DateTimeOffset ObservedAtUtc) : AlbionMessage(ObservedAtUtc);
