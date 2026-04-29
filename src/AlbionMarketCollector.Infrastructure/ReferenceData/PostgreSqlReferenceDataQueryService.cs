using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using Npgsql;

namespace AlbionMarketCollector.Infrastructure.ReferenceData;

public sealed class PostgreSqlReferenceDataQueryService : IReferenceDataQueryService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IReferenceSchemaInitializer _schemaInitializer;

    public PostgreSqlReferenceDataQueryService(
        NpgsqlDataSource dataSource,
        IReferenceSchemaInitializer schemaInitializer)
    {
        _dataSource = dataSource;
        _schemaInitializer = schemaInitializer;
    }

    public async Task<IReadOnlyList<ItemSearchResult>> SearchItemsAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        await _schemaInitializer.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var normalizedLimit = Math.Clamp(limit, 1, 100);
        var searchPattern = $"%{(search ?? string.Empty).Trim().ToLowerInvariant()}%";

        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, unique_name, localized_name
            FROM items
            WHERE $1 = '%%'
               OR lower(unique_name) LIKE $1
               OR lower(localized_name) LIKE $1
            ORDER BY localized_name, unique_name
            LIMIT $2;
            """);

        command.Parameters.AddWithValue(searchPattern);
        command.Parameters.AddWithValue(normalizedLimit);

        var results = new List<ItemSearchResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new ItemSearchResult(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return results;
    }

    public async Task<IReadOnlyList<LocationSearchResult>> SearchLocationsAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken)
    {
        await _schemaInitializer.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var normalizedLimit = Math.Clamp(limit, 1, 100);
        var searchPattern = $"%{(search ?? string.Empty).Trim().ToLowerInvariant()}%";

        await using var command = _dataSource.CreateCommand(
            """
            SELECT id, name
            FROM locations
            WHERE $1 = '%%'
               OR lower(id) LIKE $1
               OR lower(name) LIKE $1
            ORDER BY name, id
            LIMIT $2;
            """);

        command.Parameters.AddWithValue(searchPattern);
        command.Parameters.AddWithValue(normalizedLimit);

        var results = new List<LocationSearchResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new LocationSearchResult(
                reader.GetString(0),
                reader.GetString(1)));
        }

        return results;
    }
}
