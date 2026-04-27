using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Domain.Models;
using AlbionMarketCollector.Infrastructure.State;
using Xunit;

namespace AlbionMarketCollector.UnitTests.State;

public sealed class InMemoryMarketStateStoreTests
{
    [Theory]
    [InlineData("5.188.125.77", 1)]
    [InlineData("5.45.187.88", 2)]
    [InlineData("193.169.238.99", 3)]
    public void TrackPacketSource_ResolvesAlbionServerIds(string sourceIp, int expectedServerId)
    {
        var store = new InMemoryMarketStateStore(new CollectorOptions());

        store.TrackPacketSource(sourceIp, DateTimeOffset.UtcNow);

        var snapshot = store.GetSnapshot();
        Assert.Equal(expectedServerId, snapshot.CurrentServerId);
        Assert.Equal(sourceIp, snapshot.GameServerIp);
    }

    [Theory]
    [InlineData("3005", "3005")]
    [InlineData("BLACKBANK-001", "BLACKBANK-001")]
    [InlineData("Caerleon-Auction2", "Caerleon-Auction2")]
    [InlineData("@island@12345678-1234-1234-1234-1234567890ab", "@ISLAND@12345678-1234-1234-1234-1234567890ab")]
    [InlineData("invalid-location", "")]
    public void NormalizeLocation_MatchesExpectedPatterns(string input, string expected)
    {
        var store = new InMemoryMarketStateStore(new CollectorOptions());

        var normalized = store.NormalizeLocation(input);

        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void PendingHistoryRequest_IsTakenOnce_FromModuloCache()
    {
        var store = new InMemoryMarketStateStore(new CollectorOptions());
        var request = new PendingMarketHistoryRequest
        {
            MessageId = 9001,
            ItemTypeId = "T4_BAG",
            QualityLevel = 2,
            Timescale = 1,
            CapturedAtUtc = DateTimeOffset.UtcNow,
        };

        store.CachePendingHistoryRequest(request);

        Assert.True(store.TryTakePendingHistoryRequest(9001, DateTimeOffset.UtcNow, out var cached));
        Assert.NotNull(cached);
        Assert.Equal("T4_BAG", cached!.ItemTypeId);
        Assert.False(store.TryTakePendingHistoryRequest(9001, DateTimeOffset.UtcNow, out _));
    }

    [Fact]
    public void PendingHistoryRequest_ExpiresAfterTtl()
    {
        var store = new InMemoryMarketStateStore(new CollectorOptions());
        store.CachePendingHistoryRequest(new PendingMarketHistoryRequest
        {
            MessageId = 10,
            ItemTypeId = "T4_BAG",
            QualityLevel = 1,
            Timescale = 0,
            CapturedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
        });

        Assert.False(store.TryTakePendingHistoryRequest(10, DateTimeOffset.UtcNow, out _));
    }
}
