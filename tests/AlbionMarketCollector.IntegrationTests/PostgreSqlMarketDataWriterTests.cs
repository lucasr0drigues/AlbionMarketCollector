using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Domain.Enums;
using AlbionMarketCollector.Domain.Models;
using AlbionMarketCollector.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlbionMarketCollector.IntegrationTests;

public sealed class PostgreSqlMarketDataWriterTests
{
    [Fact]
    public async Task UpsertsOrdersAndState_WhenConnectionStringIsConfigured()
    {
        var connectionString = Environment.GetEnvironmentVariable("ALBION_MARKET_COLLECTOR_TEST_PG");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var options = new CollectorOptions
        {
            Persistence = new PersistenceOptions
            {
                Provider = "PostgreSql",
                PostgreSql = new PostgreSqlOptions
                {
                    ConnectionString = connectionString,
                },
            },
        };

        await using var writer = new PostgreSqlMarketDataWriter(options, NullLogger<PostgreSqlMarketDataWriter>.Instance);

        await writer.UpsertMarketOrdersAsync(
            [
                new MarketOrder
                {
                    ServerId = 1,
                    LocationId = "3005",
                    AlbionOrderId = 101,
                    ItemTypeId = "T4_BAG",
                    ItemGroupTypeId = "T4_BAG",
                    QualityLevel = 1,
                    EnchantmentLevel = 0,
                    UnitPriceSilver = 1200,
                    Amount = 3,
                    OrderType = MarketOrderType.Sell,
                    ObservedAtUtc = DateTimeOffset.UtcNow,
                },
            ],
            CancellationToken.None);

        await writer.SaveCollectorStateAsync(
            new CollectorStateSnapshot
            {
                CollectorKey = "integration-test",
                CurrentServerId = 1,
                CurrentLocationId = "3005",
                GameServerIp = "5.188.125.10",
                LastPacketAtUtc = DateTimeOffset.UtcNow,
                LastLocationUpdateAtUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);
    }
}
