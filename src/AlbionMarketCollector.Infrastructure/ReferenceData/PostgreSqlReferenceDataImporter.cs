using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.ReferenceData;
using Npgsql;

namespace AlbionMarketCollector.Infrastructure.ReferenceData;

public sealed class PostgreSqlReferenceDataImporter : IReferenceDataImporter
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IReferenceSchemaInitializer _schemaInitializer;

    public PostgreSqlReferenceDataImporter(
        NpgsqlDataSource dataSource,
        IReferenceSchemaInitializer schemaInitializer)
    {
        _dataSource = dataSource;
        _schemaInitializer = schemaInitializer;
    }

    public async Task<ReferenceDataImportResult> ImportLocationsAsync(
        IReadOnlyCollection<LocationReference> locations,
        CancellationToken cancellationToken)
    {
        if (locations.Count == 0)
        {
            return new ReferenceDataImportResult(0);
        }

        await _schemaInitializer.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var location in locations)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO locations (id, name)
                VALUES ($1, $2)
                ON CONFLICT (id)
                DO UPDATE SET name = EXCLUDED.name;
                """,
                connection,
                transaction);

            command.Parameters.AddWithValue(location.Id);
            command.Parameters.AddWithValue(location.Name);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ReferenceDataImportResult(locations.Count);
    }

    public async Task<ReferenceDataImportResult> ImportItemsAsync(
        IReadOnlyCollection<ItemReference> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return new ReferenceDataImportResult(0);
        }

        await _schemaInitializer.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var item in items)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO items (id, unique_name, localized_name)
                VALUES ($1, $2, $3)
                ON CONFLICT (id)
                DO UPDATE SET
                    unique_name = EXCLUDED.unique_name,
                    localized_name = EXCLUDED.localized_name;
                """,
                connection,
                transaction);

            command.Parameters.AddWithValue(item.Id);
            command.Parameters.AddWithValue(item.UniqueName);
            command.Parameters.AddWithValue(item.LocalizedName);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ReferenceDataImportResult(items.Count);
    }
}
