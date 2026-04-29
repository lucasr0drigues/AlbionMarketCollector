using AlbionMarketCollector.Application.Contracts;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AlbionMarketCollector.Infrastructure.ReferenceData;

public sealed class PostgreSqlReferenceSchemaInitializer : IReferenceSchemaInitializer
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgreSqlReferenceSchemaInitializer> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public PostgreSqlReferenceSchemaInitializer(
        NpgsqlDataSource dataSource,
        ILogger<PostgreSqlReferenceSchemaInitializer> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken)
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
                CREATE TABLE IF NOT EXISTS locations (
                    id text PRIMARY KEY,
                    name text NOT NULL
                );

                CREATE TABLE IF NOT EXISTS items (
                    id integer PRIMARY KEY,
                    unique_name text NOT NULL,
                    localized_name text NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ux_items_unique_name
                    ON items (unique_name);
                CREATE INDEX IF NOT EXISTS ix_items_localized_name_lower
                    ON items (lower(localized_name));
                CREATE INDEX IF NOT EXISTS ix_locations_name_lower
                    ON locations (lower(name));

                DO $$
                BEGIN
                    IF to_regclass('public.market_orders') IS NOT NULL THEN
                        CREATE INDEX IF NOT EXISTS ix_market_orders_flipper
                            ON market_orders (location_id, order_type, item_type_id, quality_level, enchantment_level, unit_price_silver, last_seen_at_utc);
                    END IF;
                END $$;
                """);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
            _logger.LogInformation("Reference data schema is ready.");
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}
