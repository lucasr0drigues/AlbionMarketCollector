using System.Text;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Domain.Enums;
using AlbionMarketCollector.Infrastructure.Persistence;
using Npgsql;

namespace AlbionMarketCollector.Infrastructure.MarketData;

public sealed class PostgreSqlMarketDataQueryService : IMarketDataQueryService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IReferenceSchemaInitializer _schemaInitializer;

    public PostgreSqlMarketDataQueryService(
        NpgsqlDataSource dataSource,
        IReferenceSchemaInitializer schemaInitializer)
    {
        _dataSource = dataSource;
        _schemaInitializer = schemaInitializer;
    }

    public async Task<IReadOnlyList<MarketOrderResult>> GetMarketOrdersAsync(
        MarketOrderQuery query,
        CancellationToken cancellationToken)
    {
        await _schemaInitializer.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var limit = Math.Clamp(query.Limit, 1, 500);
        var sql = new StringBuilder(
            """
            SELECT
                mo.server_id,
                mo.location_id,
                COALESCE(l.name, mo.location_id) AS location_name,
                mo.albion_order_id,
                mo.item_type_id,
                COALESCE(i.localized_name, mo.item_type_id) AS item_name,
                mo.quality_level,
                mo.enchantment_level,
                mo.unit_price_silver,
                mo.amount,
                mo.order_type,
                mo.expires_at_utc,
                mo.first_seen_at_utc,
                mo.last_seen_at_utc
            FROM market_orders mo
            LEFT JOIN items i ON i.unique_name = mo.item_type_id
            LEFT JOIN locations l ON l.id = mo.location_id
            WHERE 1 = 1
            """);

        await using var command = _dataSource.CreateCommand();

        if (!string.IsNullOrWhiteSpace(query.LocationId))
        {
            sql.AppendLine("AND mo.location_id = @locationId");
            command.Parameters.AddWithValue("locationId", query.LocationId);
        }

        if (query.OrderType is { } orderType)
        {
            sql.AppendLine("AND mo.order_type = @orderType");
            command.Parameters.AddWithValue("orderType", orderType.ToString());
        }

        if (query.MaxAgeMinutes is > 0)
        {
            sql.AppendLine("AND mo.last_seen_at_utc >= @minSeenAtUtc");
            command.Parameters.AddWithValue("minSeenAtUtc", DateTimeOffset.UtcNow.AddMinutes(-query.MaxAgeMinutes.Value).UtcDateTime);
        }

        if (!string.IsNullOrWhiteSpace(query.ItemSearch))
        {
            sql.AppendLine("AND (lower(mo.item_type_id) LIKE @itemSearch OR lower(COALESCE(i.localized_name, '')) LIKE @itemSearch)");
            command.Parameters.AddWithValue("itemSearch", $"%{query.ItemSearch.Trim().ToLowerInvariant()}%");
        }

        sql.AppendLine("ORDER BY mo.last_seen_at_utc DESC");
        sql.AppendLine("LIMIT @limit;");
        command.Parameters.AddWithValue("limit", limit);
        command.CommandText = sql.ToString();

        var now = DateTimeOffset.UtcNow;
        var results = new List<MarketOrderResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var lastSeenAtUtc = PostgreSqlValueReader.GetUtcDateTimeOffset(reader.GetDateTime(13));
            results.Add(new MarketOrderResult(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetInt64(8),
                reader.GetInt64(9),
                reader.GetString(10),
                reader.IsDBNull(11) ? null : PostgreSqlValueReader.GetUtcDateTimeOffset(reader.GetDateTime(11)),
                PostgreSqlValueReader.GetUtcDateTimeOffset(reader.GetDateTime(12)),
                lastSeenAtUtc,
                Math.Max(0, (now - lastSeenAtUtc).TotalMinutes)));
        }

        return results;
    }
}
