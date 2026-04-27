namespace AlbionMarketCollector.Infrastructure.AlbionProtocol.Models;

internal static class AlbionOperationCodes
{
    public const byte Join = 2;
    public const byte GetGameServerByCluster = 17;
    public const byte AuctionGetOffers = 81;
    public const byte AuctionGetRequests = 82;
    public const byte AuctionBuyOffer = 83;
    public const byte AuctionGetItemAverageStats = 95;
}
