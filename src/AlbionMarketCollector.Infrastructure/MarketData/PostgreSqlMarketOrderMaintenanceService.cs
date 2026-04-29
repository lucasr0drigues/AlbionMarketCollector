using AlbionMarketCollector.Application.Contracts;
using Npgsql;

namespace AlbionMarketCollector.Infrastructure.MarketData;

public sealed class PostgreSqlMarketOrderMaintenanceService : IMarketOrderMaintenanceService
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgreSqlMarketOrderMaintenanceService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<int> DeleteByLocationIdsAsync(
        IReadOnlyCollection<string> locationIds,
        CancellationToken cancellationToken)
    {
        var normalizedLocationIds = locationIds
            .Select(locationId => locationId.Trim())
            .Where(locationId => !string.IsNullOrWhiteSpace(locationId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedLocationIds.Length == 0)
        {
            return 0;
        }

        await using var command = _dataSource.CreateCommand(
            """
            DELETE FROM market_orders
            WHERE location_id = ANY(@locationIds);
            """);
        command.Parameters.AddWithValue("locationIds", normalizedLocationIds);

        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return 0;
        }
    }
}
