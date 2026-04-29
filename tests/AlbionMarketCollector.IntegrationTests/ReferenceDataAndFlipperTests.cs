using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Application.ReferenceData;
using AlbionMarketCollector.Domain.Enums;
using AlbionMarketCollector.Domain.Models;
using AlbionMarketCollector.Infrastructure.MarketData;
using AlbionMarketCollector.Infrastructure.Persistence;
using AlbionMarketCollector.Infrastructure.ReferenceData;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace AlbionMarketCollector.IntegrationTests;

public sealed class ReferenceDataAndFlipperTests
{
    [Fact]
    public async Task ImportsReferenceDataAndFindsFlipOpportunity_WhenConnectionStringIsConfigured()
    {
        var connectionString = Environment.GetEnvironmentVariable("ALBION_MARKET_COLLECTOR_TEST_PG");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var suffix = Guid.NewGuid().ToString("N");
        var sourceLocationId = $"TEST-SOURCE-{suffix}";
        var secondSourceLocationId = $"TEST-SOURCE-SECOND-{suffix}";
        var blackMarketLocationId = $"TEST-BLACK-{suffix}";
        var itemId = Math.Abs(Guid.NewGuid().GetHashCode());
        var itemUniqueName = $"T4_TEST_ITEM_{suffix}";
        var higherQualityItemId = itemId + 1;
        var higherQualityItemUniqueName = $"T4_TEST_ITEM_HIGHER_QUALITY_{suffix}";
        var sellOrderId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var buyOrderId = sellOrderId + 1;
        var observedOlder = DateTimeOffset.UtcNow.AddMinutes(-10);
        var observedNewer = DateTimeOffset.UtcNow;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await CleanupTestRowsAsync(dataSource);

        var schemaInitializer = new PostgreSqlReferenceSchemaInitializer(
            dataSource,
            NullLogger<PostgreSqlReferenceSchemaInitializer>.Instance);
        var importer = new PostgreSqlReferenceDataImporter(dataSource, schemaInitializer);
        var referenceQueries = new PostgreSqlReferenceDataQueryService(dataSource, schemaInitializer);
        var flipQueries = new PostgreSqlBlackMarketFlipQueryService(dataSource, schemaInitializer);
        var writer = new PostgreSqlMarketDataWriter(
            new CollectorOptions
            {
                Persistence = new PersistenceOptions
                {
                    Provider = "PostgreSql",
                    PostgreSql = new PostgreSqlOptions
                    {
                        ConnectionString = connectionString,
                    },
                },
            },
            NullLogger<PostgreSqlMarketDataWriter>.Instance);

        try
        {
            await importer.ImportLocationsAsync(
                [
                    new LocationReference(sourceLocationId, "Test Source"),
                    new LocationReference(secondSourceLocationId, "Test Source Second"),
                    new LocationReference(blackMarketLocationId, "Test Black Market"),
                ],
                CancellationToken.None);
            await importer.ImportItemsAsync(
                [
                    new ItemReference(itemId, itemUniqueName, "Test Flip Item"),
                    new ItemReference(higherQualityItemId, higherQualityItemUniqueName, "Test Higher Quality Item"),
                ],
                CancellationToken.None);

            await writer.UpsertMarketOrdersAsync(
                [
                    new MarketOrder
                    {
                        ServerId = 1,
                        LocationId = sourceLocationId,
                        AlbionOrderId = sellOrderId,
                        ItemTypeId = itemUniqueName,
                        ItemGroupTypeId = itemUniqueName,
                        QualityLevel = 1,
                        EnchantmentLevel = 0,
                        UnitPriceSilver = 1_000,
                        Amount = 3,
                        OrderType = MarketOrderType.Sell,
                        ObservedAtUtc = observedOlder,
                    },
                    new MarketOrder
                    {
                        ServerId = 1,
                        LocationId = secondSourceLocationId,
                        AlbionOrderId = sellOrderId + 2,
                        ItemTypeId = itemUniqueName,
                        ItemGroupTypeId = itemUniqueName,
                        QualityLevel = 1,
                        EnchantmentLevel = 0,
                        UnitPriceSilver = 1_000,
                        Amount = 3,
                        OrderType = MarketOrderType.Sell,
                        ObservedAtUtc = observedNewer,
                    },
                    new MarketOrder
                    {
                        ServerId = 1,
                        LocationId = blackMarketLocationId,
                        AlbionOrderId = buyOrderId,
                        ItemTypeId = itemUniqueName,
                        ItemGroupTypeId = itemUniqueName,
                        QualityLevel = 1,
                        EnchantmentLevel = 0,
                        UnitPriceSilver = 1_500,
                        Amount = 2,
                        OrderType = MarketOrderType.Buy,
                        ObservedAtUtc = observedNewer,
                    },
                    new MarketOrder
                    {
                        ServerId = 1,
                        LocationId = sourceLocationId,
                        AlbionOrderId = sellOrderId + 3,
                        ItemTypeId = higherQualityItemUniqueName,
                        ItemGroupTypeId = higherQualityItemUniqueName,
                        QualityLevel = 4,
                        EnchantmentLevel = 0,
                        UnitPriceSilver = 2_000,
                        Amount = 1,
                        OrderType = MarketOrderType.Sell,
                        ObservedAtUtc = observedNewer,
                    },
                    new MarketOrder
                    {
                        ServerId = 1,
                        LocationId = blackMarketLocationId,
                        AlbionOrderId = buyOrderId + 3,
                        ItemTypeId = higherQualityItemUniqueName,
                        ItemGroupTypeId = higherQualityItemUniqueName,
                        QualityLevel = 2,
                        EnchantmentLevel = 0,
                        UnitPriceSilver = 3_000,
                        Amount = 1,
                        OrderType = MarketOrderType.Buy,
                        ObservedAtUtc = observedNewer,
                    },
                ],
                CancellationToken.None);

            var items = await referenceQueries.SearchItemsAsync("Flip Item", 10, CancellationToken.None);
            Assert.Contains(items, item => item.UniqueName == itemUniqueName);

            var opportunities = await flipQueries.FindOpportunitiesAsync(
                new BlackMarketFlipQuery
                {
                    SourceLocationIds = [sourceLocationId, secondSourceLocationId],
                    SellingLocationIds = [blackMarketLocationId],
                    ItemUniqueNames = [itemUniqueName],
                    MaxAgeMinutes = 60,
                    MinProfitSilver = 1,
                    Limit = 10,
                },
                CancellationToken.None);

            var opportunity = Assert.Single(opportunities);
            Assert.Equal(itemUniqueName, opportunity.ItemUniqueName);
            Assert.Equal(secondSourceLocationId, opportunity.SourceLocationId);
            Assert.Equal(500, opportunity.ProfitPerItemSilver);
            Assert.Equal(2, opportunity.MaxTradableAmount);
            Assert.Equal(1_000, opportunity.EstimatedTotalProfitSilver);

            var optionalFilterOpportunities = await flipQueries.FindOpportunitiesAsync(
                new BlackMarketFlipQuery
                {
                    SellingLocationIds = [blackMarketLocationId],
                    ItemUniqueNames = [itemUniqueName],
                    QualityLevel = 1,
                    EnchantmentLevel = 0,
                    Limit = 10,
                },
                CancellationToken.None);

            Assert.Single(optionalFilterOpportunities);

            var wrongQualityOpportunities = await flipQueries.FindOpportunitiesAsync(
                new BlackMarketFlipQuery
                {
                    SellingLocationIds = [blackMarketLocationId],
                    ItemUniqueNames = [itemUniqueName],
                    QualityLevel = 2,
                    EnchantmentLevel = 0,
                    Limit = 10,
                },
                CancellationToken.None);

            Assert.Empty(wrongQualityOpportunities);

            var higherQualityOpportunities = await flipQueries.FindOpportunitiesAsync(
                new BlackMarketFlipQuery
                {
                    SourceLocationIds = [sourceLocationId],
                    SellingLocationIds = [blackMarketLocationId],
                    ItemUniqueNames = [higherQualityItemUniqueName],
                    QualityLevel = 2,
                    EnchantmentLevel = 0,
                    Limit = 10,
                },
                CancellationToken.None);

            var higherQualityOpportunity = Assert.Single(higherQualityOpportunities);
            Assert.Equal(higherQualityItemUniqueName, higherQualityOpportunity.ItemUniqueName);
            Assert.Equal(4, higherQualityOpportunity.QualityLevel);
            Assert.Equal(1_000, higherQualityOpportunity.ProfitPerItemSilver);
        }
        finally
        {
            await CleanupTestRowsAsync(dataSource);
            await writer.DisposeAsync();
        }
    }

    private static async Task CleanupTestRowsAsync(NpgsqlDataSource dataSource)
    {
        await using var cleanupOrders = dataSource.CreateCommand("DELETE FROM market_orders WHERE item_type_id LIKE 'T4_TEST_ITEM_%';");
        await cleanupOrders.ExecuteNonQueryAsync();

        await using var cleanupItems = dataSource.CreateCommand("DELETE FROM items WHERE unique_name LIKE 'T4_TEST_ITEM_%';");
        await cleanupItems.ExecuteNonQueryAsync();

        await using var cleanupLocations = dataSource.CreateCommand("DELETE FROM locations WHERE id LIKE 'TEST-SOURCE-%' OR id LIKE 'TEST-BLACK-%';");
        await cleanupLocations.ExecuteNonQueryAsync();
    }
}
