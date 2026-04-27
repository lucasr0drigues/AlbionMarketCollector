using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Application.Pipeline;
using AlbionMarketCollector.Domain.Enums;
using AlbionMarketCollector.Domain.Models;
using AlbionMarketCollector.Infrastructure.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AlbionMarketCollector.UnitTests.Pipeline;

public sealed class MarketProjectionServiceTests
{
    [Fact]
    public async Task SellOrders_FallbackToCurrentLocation_WhenPayloadLocationIsEmpty()
    {
        var stateStore = new InMemoryMarketStateStore(new AlbionMarketCollector.Application.Configuration.CollectorOptions());
        stateStore.TrackPacketSource("5.188.125.10", DateTimeOffset.UtcNow);
        stateStore.UpdateLocation("3005", DateTimeOffset.UtcNow);
        var writer = new RecordingMarketDataWriter();
        var service = new MarketProjectionService(stateStore, writer, NullLogger<MarketProjectionService>.Instance);

        const string payload = """
            {"Id":101,"ItemTypeId":"T4_BAG","ItemGroupTypeId":"T4_BAG","LocationId":"","QualityLevel":1,"EnchantmentLevel":0,"UnitPriceSilver":1200,"Amount":3,"AuctionType":"offer","Expires":"2026-04-27T12:00:00Z"}
            """;

        await service.ApplyAsync(
            [new AuctionGetOffersResponseMessage([payload], DateTimeOffset.UtcNow)],
            CancellationToken.None);

        var order = Assert.Single(writer.Orders);
        Assert.Equal(MarketOrderType.Sell, order.OrderType);
        Assert.Equal("3005", order.LocationId);
        Assert.Equal(1, order.ServerId);
    }

    [Fact]
    public async Task SellOrders_WarnsAndSkips_WhenLocationIsUnknown()
    {
        var stateStore = new InMemoryMarketStateStore(new AlbionMarketCollector.Application.Configuration.CollectorOptions());
        var writer = new RecordingMarketDataWriter();
        var logger = new RecordingLogger<MarketProjectionService>();
        var service = new MarketProjectionService(stateStore, writer, logger);

        const string payload = """
            {"Id":101,"ItemTypeId":"T4_BAG","ItemGroupTypeId":"T4_BAG","LocationId":"4002","QualityLevel":1,"EnchantmentLevel":0,"UnitPriceSilver":1200,"Amount":3,"AuctionType":"offer","Expires":"2026-04-27T12:00:00Z"}
            """;

        await service.ApplyAsync(
            [new AuctionGetOffersResponseMessage([payload], DateTimeOffset.UtcNow)],
            CancellationToken.None);

        Assert.Empty(writer.Orders);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error &&
                     entry.Message.Contains("market orders", StringComparison.OrdinalIgnoreCase) &&
                     entry.Message.Contains("location is unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MarketHistory_WarnsAndSkips_WhenLocationIsUnknown()
    {
        var stateStore = new InMemoryMarketStateStore(new AlbionMarketCollector.Application.Configuration.CollectorOptions());
        var writer = new RecordingMarketDataWriter();
        var logger = new RecordingLogger<MarketProjectionService>();
        var service = new MarketProjectionService(stateStore, writer, logger);

        await service.ApplyAsync(
            [new AuctionGetItemAverageStatsRequestMessage(8281, 1, 2, 0, 260, DateTimeOffset.UtcNow)],
            CancellationToken.None);

        await service.ApplyAsync(
            [new AuctionGetItemAverageStatsResponseMessage(
                [15],
                [1_273_990_000UL],
                [639_115_704_000_000_000UL],
                260,
                DateTimeOffset.UtcNow)],
            CancellationToken.None);

        Assert.Empty(writer.HistoryPoints);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error &&
                     entry.Message.Contains("market history", StringComparison.OrdinalIgnoreCase) &&
                     entry.Message.Contains("location is unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LocationMessages_LogSuccessfulLocationUpdates()
    {
        var stateStore = new InMemoryMarketStateStore(new AlbionMarketCollector.Application.Configuration.CollectorOptions());
        var writer = new RecordingMarketDataWriter();
        var logger = new RecordingLogger<MarketProjectionService>();
        var service = new MarketProjectionService(stateStore, writer, logger);

        await service.ApplyAsync(
            [
                new JoinResponseMessage("character-id", "Lucas", "3005", DateTimeOffset.UtcNow),
                new GetGameServerByClusterMessage("Caerleon-Auction2", DateTimeOffset.UtcNow),
            ],
            CancellationToken.None);

        Assert.Equal("Caerleon-Auction2", stateStore.GetSnapshot().CurrentLocationId);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Information &&
                     entry.Message.Contains("Updated Albion location to 3005", StringComparison.OrdinalIgnoreCase) &&
                     entry.Message.Contains("join response", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Information &&
                     entry.Message.Contains("Updated Albion location to Caerleon-Auction2", StringComparison.OrdinalIgnoreCase) &&
                     entry.Message.Contains("cluster lookup", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MarketHistory_RewritesSmallNegativeItemAmounts_AndSkipsLargeNegativeValues()
    {
        var stateStore = new InMemoryMarketStateStore(new AlbionMarketCollector.Application.Configuration.CollectorOptions());
        stateStore.TrackPacketSource("5.45.187.22", DateTimeOffset.UtcNow);
        stateStore.UpdateLocation("4002", DateTimeOffset.UtcNow);
        var writer = new RecordingMarketDataWriter();
        var service = new MarketProjectionService(stateStore, writer, NullLogger<MarketProjectionService>.Instance);

        await service.ApplyAsync(
            [new AuctionGetItemAverageStatsRequestMessage(-121, 2, 1, 0, 77, DateTimeOffset.UtcNow)],
            CancellationToken.None);

        await service.ApplyAsync(
            [new AuctionGetItemAverageStatsResponseMessage(
                [-121, -125, 9],
                [100UL, 200UL, 300UL],
                [1_700_000_002UL, 1_700_000_001UL, 1_700_000_003UL],
                77,
                DateTimeOffset.UtcNow)],
            CancellationToken.None);

        Assert.Equal(2, writer.HistoryPoints.Count);
        Assert.Equal("135", writer.HistoryPoints[0].ItemTypeId);
        Assert.Equal(9, writer.HistoryPoints[0].ItemAmount);
        Assert.Equal(135, writer.HistoryPoints[1].ItemAmount);
        Assert.True(writer.HistoryPoints[0].DataTimestampUtc >= writer.HistoryPoints[1].DataTimestampUtc);
    }

    [Fact]
    public async Task MarketHistory_AcceptsDotNetTickTimestamps()
    {
        var stateStore = new InMemoryMarketStateStore(new AlbionMarketCollector.Application.Configuration.CollectorOptions());
        stateStore.TrackPacketSource("5.188.125.62", DateTimeOffset.UtcNow);
        stateStore.UpdateLocation("3005", DateTimeOffset.UtcNow);
        var writer = new RecordingMarketDataWriter();
        var service = new MarketProjectionService(stateStore, writer, NullLogger<MarketProjectionService>.Instance);

        await service.ApplyAsync(
            [new AuctionGetItemAverageStatsRequestMessage(8281, 1, 2, 0, 260, DateTimeOffset.UtcNow)],
            CancellationToken.None);

        await service.ApplyAsync(
            [new AuctionGetItemAverageStatsResponseMessage(
                [15, 8, 44],
                [1_273_990_000UL, 725_670_000UL, 4_204_210_000UL],
                [639_115_704_000_000_000UL, 639_111_384_000_000_000UL, 639_120_456_000_000_000UL],
                260,
                DateTimeOffset.UtcNow)],
            CancellationToken.None);

        Assert.Equal(3, writer.HistoryPoints.Count);
        Assert.Equal(new DateTimeOffset(639_120_456_000_000_000L, TimeSpan.Zero), writer.HistoryPoints[0].DataTimestampUtc);
        Assert.Equal(new DateTimeOffset(639_115_704_000_000_000L, TimeSpan.Zero), writer.HistoryPoints[1].DataTimestampUtc);
        Assert.Equal(new DateTimeOffset(639_111_384_000_000_000L, TimeSpan.Zero), writer.HistoryPoints[2].DataTimestampUtc);
    }

    [Fact]
    public async Task EncryptedMarketMessage_ClearsWaitingFlag()
    {
        var stateStore = new InMemoryMarketStateStore(new AlbionMarketCollector.Application.Configuration.CollectorOptions());
        stateStore.SetWaitingForMarketData(true);
        var writer = new RecordingMarketDataWriter();
        var service = new MarketProjectionService(stateStore, writer, NullLogger<MarketProjectionService>.Instance);

        await service.ApplyAsync(
            [new EncryptedMarketDataMessage(DateTimeOffset.UtcNow)],
            CancellationToken.None);

        Assert.False(stateStore.GetSnapshot().WaitingForMarketData);
    }

    private sealed class RecordingMarketDataWriter : IMarketDataWriter
    {
        public List<MarketOrder> Orders { get; } = [];

        public List<MarketHistoryPoint> HistoryPoints { get; } = [];

        public CollectorStateSnapshot? Snapshot { get; private set; }

        public Task UpsertMarketOrdersAsync(IReadOnlyCollection<MarketOrder> orders, CancellationToken cancellationToken)
        {
            Orders.AddRange(orders);
            return Task.CompletedTask;
        }

        public Task UpsertMarketHistoryAsync(IReadOnlyCollection<MarketHistoryPoint> historyPoints, CancellationToken cancellationToken)
        {
            HistoryPoints.AddRange(historyPoints);
            return Task.CompletedTask;
        }

        public Task SaveCollectorStateAsync(CollectorStateSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
