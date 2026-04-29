using System.Text;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Infrastructure.Persistence;
using Npgsql;

namespace AlbionMarketCollector.Infrastructure.MarketData;

public sealed class PostgreSqlBlackMarketFlipQueryService : IBlackMarketFlipQueryService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IReferenceSchemaInitializer _schemaInitializer;

    public PostgreSqlBlackMarketFlipQueryService(
        NpgsqlDataSource dataSource,
        IReferenceSchemaInitializer schemaInitializer)
    {
        _dataSource = dataSource;
        _schemaInitializer = schemaInitializer;
    }

    public async Task<IReadOnlyList<BlackMarketFlipOpportunity>> FindOpportunitiesAsync(
        BlackMarketFlipQuery query,
        CancellationToken cancellationToken)
    {
        await _schemaInitializer.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var limit = Math.Clamp(query.Limit, 1, 500);
        var maxAgeMinutes = query.MaxAgeMinutes is > 0 ? query.MaxAgeMinutes.Value : (int?)null;
        var minProfitSilver = Math.Max(1, query.MinProfitSilver ?? 1);
        var sourceLocationIds = NormalizeList(query.SourceLocationIds);
        var excludedSourceLocationIds = NormalizeList(query.ExcludedSourceLocationIds);
        var sellingLocationIds = NormalizeList(query.SellingLocationIds);
        var itemUniqueNames = NormalizeList(query.ItemUniqueNames);

        await using var command = _dataSource.CreateCommand();
        command.Parameters.AddWithValue("minProfitSilver", minProfitSilver);
        command.Parameters.AddWithValue("limit", limit);

        var buyFilters = new StringBuilder(
            """
            WITH buy_orders AS (
                SELECT mo.*
                FROM market_orders mo
                LEFT JOIN items i ON i.unique_name = mo.item_type_id
                WHERE mo.order_type = 'Buy'
                  AND (mo.expires_at_utc IS NULL OR mo.expires_at_utc > now())
            """);

        if (sellingLocationIds.Length > 0)
        {
            buyFilters.AppendLine("AND mo.location_id = ANY(@sellingLocationIds)");
            command.Parameters.AddWithValue("sellingLocationIds", sellingLocationIds);
        }

        AppendAgeFilter(buyFilters, command, maxAgeMinutes, "mo");
        AppendOptionalFilters(buyFilters, command, query, itemUniqueNames, "mo", "i");

        var sql = buyFilters;
        sql.AppendLine(
            """
            ),
            ranked_opportunities AS (
                SELECT
                    sell.server_id AS sell_server_id,
                    sell.item_type_id,
                    COALESCE(item.localized_name, sell.item_type_id) AS item_localized_name,
                    sell.quality_level AS sell_quality_level,
                    sell.enchantment_level,
                    sell.location_id AS source_location_id,
                    COALESCE(source_location.name, sell.location_id) AS source_location_name,
                    buy.location_id AS selling_location_id,
                    COALESCE(black_location.name, buy.location_id) AS selling_location_name,
                    buy.albion_order_id AS buy_order_id,
                    buy.unit_price_silver AS buy_price_silver,
                    buy.amount AS buy_amount,
                    buy.last_seen_at_utc AS buy_last_seen_at_utc,
                    sell.albion_order_id AS sell_order_id,
                    sell.unit_price_silver AS sell_price_silver,
                    sell.amount AS sell_amount,
                    sell.last_seen_at_utc AS sell_last_seen_at_utc,
                    buy.unit_price_silver - sell.unit_price_silver AS profit_per_item_silver,
                    row_number() OVER (
                        PARTITION BY buy.server_id, buy.order_type, buy.albion_order_id
                        ORDER BY sell.unit_price_silver ASC, sell.last_seen_at_utc DESC
                    ) AS source_rank
                FROM buy_orders buy
                INNER JOIN market_orders sell
                    ON sell.order_type = 'Sell'
                   AND sell.item_type_id = buy.item_type_id
                   AND sell.enchantment_level = buy.enchantment_level
                   AND sell.quality_level >= buy.quality_level
                   AND sell.location_id <> buy.location_id
                   AND (sell.expires_at_utc IS NULL OR sell.expires_at_utc > now())
                LEFT JOIN items item ON item.unique_name = sell.item_type_id
                LEFT JOIN locations source_location ON source_location.id = sell.location_id
                LEFT JOIN locations black_location ON black_location.id = buy.location_id
                WHERE buy.unit_price_silver - sell.unit_price_silver >= @minProfitSilver
            """);

        if (sourceLocationIds.Length > 0)
        {
            sql.AppendLine("AND sell.location_id = ANY(@sourceLocationIds)");
            command.Parameters.AddWithValue("sourceLocationIds", sourceLocationIds);
        }
        else if (excludedSourceLocationIds.Length > 0)
        {
            sql.AppendLine("AND sell.location_id <> ALL(@excludedSourceLocationIds)");
            command.Parameters.AddWithValue("excludedSourceLocationIds", excludedSourceLocationIds);
        }

        AppendAgeFilter(sql, command, maxAgeMinutes, "sell");

        sql.AppendLine(
            """
            ),
            selected_source_opportunities AS (
                SELECT *
                FROM ranked_opportunities
                WHERE source_rank = 1
            """);

        if (query.MinProfitPercent is { } minProfitPercent)
        {
            sql.AppendLine("AND (profit_per_item_silver::numeric / NULLIF(sell_price_silver, 0)::numeric * 100) >= @minProfitPercent");
            command.Parameters.AddWithValue("minProfitPercent", minProfitPercent);
        }

        sql.AppendLine(
            """
            ),
            allocated_opportunities AS (
                SELECT
                    *,
                    COALESCE(
                        SUM(buy_amount) OVER (
                            PARTITION BY sell_server_id, sell_order_id
                            ORDER BY profit_per_item_silver DESC, buy_last_seen_at_utc DESC, buy_order_id
                            ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING
                        ),
                        0
                    )::bigint AS previously_allocated_buy_amount
                FROM selected_source_opportunities
            ),
            tradable_opportunities AS (
                SELECT
                    item_type_id,
                    item_localized_name,
                    sell_quality_level,
                    enchantment_level,
                    source_location_id,
                    source_location_name,
                    selling_location_id,
                    selling_location_name,
                    buy_order_id,
                    buy_price_silver,
                    buy_amount,
                    buy_last_seen_at_utc,
                    sell_order_id,
                    sell_price_silver,
                    sell_amount,
                    sell_last_seen_at_utc,
                    LEAST(
                        buy_amount,
                        GREATEST(0::bigint, sell_amount - previously_allocated_buy_amount)
                    ) AS max_tradable_amount
                FROM allocated_opportunities
            )
            SELECT
                item_type_id,
                item_localized_name,
                sell_quality_level,
                enchantment_level,
                source_location_id,
                source_location_name,
                selling_location_id,
                selling_location_name,
                buy_order_id,
                buy_price_silver,
                buy_amount,
                buy_last_seen_at_utc,
                sell_order_id,
                sell_price_silver,
                sell_amount,
                sell_last_seen_at_utc,
                max_tradable_amount
            FROM tradable_opportunities
            WHERE max_tradable_amount > 0
            """);

        sql.AppendLine("ORDER BY (buy_price_silver - sell_price_silver) * max_tradable_amount DESC, buy_price_silver - sell_price_silver DESC");
        sql.AppendLine("LIMIT @limit;");
        command.CommandText = sql.ToString();

        var now = DateTimeOffset.UtcNow;
        var results = new List<BlackMarketFlipOpportunity>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var buyLastSeenAtUtc = PostgreSqlValueReader.GetUtcDateTimeOffset(reader.GetDateTime(11));
            var sellLastSeenAtUtc = PostgreSqlValueReader.GetUtcDateTimeOffset(reader.GetDateTime(15));
            var buyPrice = reader.GetInt64(9);
            var buyAmount = reader.GetInt64(10);
            var sellPrice = reader.GetInt64(13);
            var sellAmount = reader.GetInt64(14);
            var maxTradableAmount = reader.GetInt64(16);
            var profitPerItem = buyPrice - sellPrice;
            var profitPercent = sellPrice == 0
                ? 0
                : Math.Round((decimal)profitPerItem / sellPrice * 100, 2);

            results.Add(new BlackMarketFlipOpportunity(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetInt64(8),
                buyPrice,
                buyAmount,
                buyLastSeenAtUtc,
                Math.Max(0, (now - buyLastSeenAtUtc).TotalMinutes),
                reader.GetInt64(12),
                sellPrice,
                sellAmount,
                sellLastSeenAtUtc,
                Math.Max(0, (now - sellLastSeenAtUtc).TotalMinutes),
                maxTradableAmount,
                profitPerItem,
                profitPercent,
                profitPerItem * maxTradableAmount));
        }

        return results;
    }

    private static void AppendOptionalFilters(
        StringBuilder sql,
        NpgsqlCommand command,
        BlackMarketFlipQuery query,
        string[] itemUniqueNames,
        string orderAlias,
        string itemAlias)
    {
        if (query.QualityLevel is { } qualityLevel)
        {
            sql.AppendLine($"AND {orderAlias}.quality_level = @qualityLevel");
            AddParameterIfMissing(command, "qualityLevel", qualityLevel);
        }

        if (query.EnchantmentLevel is { } enchantmentLevel)
        {
            sql.AppendLine($"AND {orderAlias}.enchantment_level = @enchantmentLevel");
            AddParameterIfMissing(command, "enchantmentLevel", enchantmentLevel);
        }

        if (!string.IsNullOrWhiteSpace(query.ItemSearch))
        {
            sql.AppendLine($"AND (lower({orderAlias}.item_type_id) LIKE @itemSearch OR lower(COALESCE({itemAlias}.localized_name, '')) LIKE @itemSearch)");
            AddParameterIfMissing(command, "itemSearch", $"%{query.ItemSearch.Trim().ToLowerInvariant()}%");
        }

        if (itemUniqueNames.Length > 0)
        {
            sql.AppendLine($"AND {orderAlias}.item_type_id = ANY(@itemUniqueNames)");
            AddParameterIfMissing(command, "itemUniqueNames", itemUniqueNames);
        }
    }

    private static void AddParameterIfMissing(NpgsqlCommand command, string name, object value)
    {
        if (command.Parameters.Contains(name))
        {
            return;
        }

        command.Parameters.AddWithValue(name, value);
    }

    private static void AppendAgeFilter(
        StringBuilder sql,
        NpgsqlCommand command,
        int? maxAgeMinutes,
        string orderAlias)
    {
        if (maxAgeMinutes is not { } minutes)
        {
            return;
        }

        sql.AppendLine($"AND {orderAlias}.last_seen_at_utc >= @minSeenAtUtc");
        AddParameterIfMissing(command, "minSeenAtUtc", DateTimeOffset.UtcNow.AddMinutes(-minutes).UtcDateTime);
    }

    private static string[] NormalizeList(IReadOnlyList<string> values)
    {
        return values
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
