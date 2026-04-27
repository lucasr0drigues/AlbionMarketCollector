using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Domain.Models;
using Npgsql;
using Microsoft.Extensions.Logging;

namespace AlbionMarketCollector.Infrastructure.Persistence;

public sealed class PostgreSqlMarketDataWriter : IMarketDataWriter, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgreSqlMarketDataWriter> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public PostgreSqlMarketDataWriter(
        CollectorOptions options,
        ILogger<PostgreSqlMarketDataWriter> logger)
    {
        _logger = logger;
        var connectionString = options.Persistence.PostgreSql.ConnectionString
            ?? options.Persistence.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("PostgreSQL persistence requires a connection string.");
        }

        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    public async Task UpsertMarketOrdersAsync(
        IReadOnlyCollection<MarketOrder> orders,
        CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var order in orders)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO market_orders (
                    server_id,
                    location_id,
                    albion_order_id,
                    item_type_id,
                    item_group_type_id,
                    quality_level,
                    enchantment_level,
                    unit_price_silver,
                    amount,
                    order_type,
                    expires_at_utc,
                    first_seen_at_utc,
                    last_seen_at_utc
                )
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $12)
                ON CONFLICT (server_id, order_type, albion_order_id)
                DO UPDATE SET
                    location_id = EXCLUDED.location_id,
                    item_type_id = EXCLUDED.item_type_id,
                    item_group_type_id = EXCLUDED.item_group_type_id,
                    quality_level = EXCLUDED.quality_level,
                    enchantment_level = EXCLUDED.enchantment_level,
                    unit_price_silver = EXCLUDED.unit_price_silver,
                    amount = EXCLUDED.amount,
                    expires_at_utc = EXCLUDED.expires_at_utc,
                    last_seen_at_utc = EXCLUDED.last_seen_at_utc;
                """,
                connection,
                transaction);

            command.Parameters.AddWithValue(order.ServerId);
            command.Parameters.AddWithValue(order.LocationId);
            command.Parameters.AddWithValue(order.AlbionOrderId);
            command.Parameters.AddWithValue(order.ItemTypeId);
            command.Parameters.AddWithValue(order.ItemGroupTypeId);
            command.Parameters.AddWithValue(order.QualityLevel);
            command.Parameters.AddWithValue(order.EnchantmentLevel);
            command.Parameters.AddWithValue(order.UnitPriceSilver);
            command.Parameters.AddWithValue(order.Amount);
            command.Parameters.AddWithValue(order.OrderType.ToString());
            command.Parameters.AddWithValue((object?)order.ExpiresAtUtc ?? DBNull.Value);
            command.Parameters.AddWithValue(order.ObservedAtUtc.UtcDateTime);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Persisted {OrderCount} market orders.", orders.Count);
    }

    public async Task UpsertMarketHistoryAsync(
        IReadOnlyCollection<MarketHistoryPoint> historyPoints,
        CancellationToken cancellationToken)
    {
        if (historyPoints.Count == 0)
        {
            return;
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var point in historyPoints)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO market_history (
                    server_id,
                    location_id,
                    item_type_id,
                    quality_level,
                    timescale,
                    data_timestamp_utc,
                    item_amount,
                    silver_amount,
                    observed_at_utc
                )
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                ON CONFLICT (server_id, location_id, item_type_id, quality_level, timescale, data_timestamp_utc)
                DO UPDATE SET
                    item_amount = EXCLUDED.item_amount,
                    silver_amount = EXCLUDED.silver_amount,
                    observed_at_utc = EXCLUDED.observed_at_utc;
                """,
                connection,
                transaction);

            command.Parameters.AddWithValue(point.ServerId);
            command.Parameters.AddWithValue(point.LocationId);
            command.Parameters.AddWithValue(point.ItemTypeId);
            command.Parameters.AddWithValue((int)point.QualityLevel);
            command.Parameters.AddWithValue((int)point.Timescale);
            command.Parameters.AddWithValue(point.DataTimestampUtc.UtcDateTime);
            command.Parameters.AddWithValue(point.ItemAmount);
            command.Parameters.AddWithValue((decimal)point.SilverAmount);
            command.Parameters.AddWithValue(point.ObservedAtUtc.UtcDateTime);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Persisted {HistoryCount} market history rows.", historyPoints.Count);
    }

    public async Task SaveCollectorStateAsync(
        CollectorStateSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO collector_state (
                collector_key,
                current_server_id,
                current_location_id,
                character_id,
                character_name,
                game_server_ip,
                waiting_for_market_data,
                last_packet_at_utc,
                last_location_update_at_utc
            )
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
            ON CONFLICT (collector_key)
            DO UPDATE SET
                current_server_id = EXCLUDED.current_server_id,
                current_location_id = EXCLUDED.current_location_id,
                character_id = EXCLUDED.character_id,
                character_name = EXCLUDED.character_name,
                game_server_ip = EXCLUDED.game_server_ip,
                waiting_for_market_data = EXCLUDED.waiting_for_market_data,
                last_packet_at_utc = EXCLUDED.last_packet_at_utc,
                last_location_update_at_utc = EXCLUDED.last_location_update_at_utc;
            """);

        command.Parameters.AddWithValue(snapshot.CollectorKey);
        command.Parameters.AddWithValue(snapshot.CurrentServerId);
        command.Parameters.AddWithValue(snapshot.CurrentLocationId);
        command.Parameters.AddWithValue(snapshot.CharacterId);
        command.Parameters.AddWithValue(snapshot.CharacterName);
        command.Parameters.AddWithValue(snapshot.GameServerIp);
        command.Parameters.AddWithValue(snapshot.WaitingForMarketData);
        command.Parameters.AddWithValue((object?)snapshot.LastPacketAtUtc?.UtcDateTime ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)snapshot.LastLocationUpdateAtUtc?.UtcDateTime ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync().ConfigureAwait(false);
        _initializationLock.Dispose();
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var command = _dataSource.CreateCommand(
                """
                CREATE TABLE IF NOT EXISTS market_orders (
                    server_id integer NOT NULL,
                    location_id text NOT NULL,
                    albion_order_id bigint NOT NULL,
                    item_type_id text NOT NULL,
                    item_group_type_id text NOT NULL,
                    quality_level integer NOT NULL,
                    enchantment_level integer NOT NULL,
                    unit_price_silver bigint NOT NULL,
                    amount bigint NOT NULL,
                    order_type text NOT NULL,
                    expires_at_utc timestamptz NULL,
                    first_seen_at_utc timestamptz NOT NULL,
                    last_seen_at_utc timestamptz NOT NULL,
                    PRIMARY KEY (server_id, order_type, albion_order_id)
                );

                CREATE INDEX IF NOT EXISTS ix_market_orders_lookup
                    ON market_orders (item_type_id, location_id, quality_level, enchantment_level, order_type);
                CREATE INDEX IF NOT EXISTS ix_market_orders_price
                    ON market_orders (location_id, order_type, unit_price_silver);
                CREATE INDEX IF NOT EXISTS ix_market_orders_last_seen
                    ON market_orders (last_seen_at_utc);

                CREATE TABLE IF NOT EXISTS market_history (
                    server_id integer NOT NULL,
                    location_id text NOT NULL,
                    item_type_id text NOT NULL,
                    quality_level integer NOT NULL,
                    timescale integer NOT NULL,
                    data_timestamp_utc timestamptz NOT NULL,
                    item_amount bigint NOT NULL,
                    silver_amount numeric(20, 0) NOT NULL,
                    observed_at_utc timestamptz NOT NULL,
                    PRIMARY KEY (server_id, location_id, item_type_id, quality_level, timescale, data_timestamp_utc)
                );

                CREATE INDEX IF NOT EXISTS ix_market_history_lookup
                    ON market_history (item_type_id, location_id, quality_level, timescale, data_timestamp_utc DESC);
                CREATE INDEX IF NOT EXISTS ix_market_history_observed
                    ON market_history (observed_at_utc DESC);

                CREATE TABLE IF NOT EXISTS collector_state (
                    collector_key text PRIMARY KEY,
                    current_server_id integer NOT NULL,
                    current_location_id text NOT NULL,
                    character_id text NOT NULL,
                    character_name text NOT NULL,
                    game_server_ip text NOT NULL,
                    waiting_for_market_data boolean NOT NULL,
                    last_packet_at_utc timestamptz NULL,
                    last_location_update_at_utc timestamptz NULL
                );
                """);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
            _logger.LogInformation("PostgreSQL schema is ready.");
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
