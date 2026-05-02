using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using System.Globalization;
using Npgsql;

namespace AlbionMarketCollector.Infrastructure.Settings;

public sealed class PostgreSqlAppSettingsService : IAppSettingsService
{
    private const string PremiumKey = "premium";
    private const string DefaultMinTotalProfitSilverKey = "default_min_total_profit_silver";
    private const string DefaultMinProfitPercentKey = "default_min_profit_percent";

    private readonly NpgsqlDataSource _dataSource;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _initialized;

    public PostgreSqlAppSettingsService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var command = _dataSource.CreateCommand(
            """
            SELECT key, value
            FROM app_settings
            WHERE key = ANY(@keys);
            """);
        command.Parameters.AddWithValue("keys", new[]
        {
            PremiumKey,
            DefaultMinTotalProfitSilverKey,
            DefaultMinProfitPercentKey,
        });

        string? premium = null;
        string? defaultMinTotalProfitSilver = null;
        string? defaultMinProfitPercent = null;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = reader.GetString(0);
            var value = reader.GetString(1);
            switch (key)
            {
                case PremiumKey:
                    premium = value;
                    break;
                case DefaultMinTotalProfitSilverKey:
                    defaultMinTotalProfitSilver = value;
                    break;
                case DefaultMinProfitPercentKey:
                    defaultMinProfitPercent = value;
                    break;
            }
        }

        return new AppSettings(
            ParseBoolean(premium),
            ParseDecimal(defaultMinTotalProfitSilver),
            ParseDecimal(defaultMinProfitPercent));
    }

    public async Task<AppSettings> UpdateAsync(
        UpdateAppSettingsRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await UpsertAsync(PremiumKey, request.Premium ? "true" : "false", cancellationToken).ConfigureAwait(false);
        await UpsertOptionalAsync(DefaultMinTotalProfitSilverKey, request.DefaultMinTotalProfitSilver, cancellationToken).ConfigureAwait(false);
        await UpsertOptionalAsync(DefaultMinProfitPercentKey, request.DefaultMinProfitPercent, cancellationToken).ConfigureAwait(false);

        return new AppSettings(
            request.Premium,
            NormalizeDecimal(request.DefaultMinTotalProfitSilver),
            NormalizeDecimal(request.DefaultMinProfitPercent));
    }

    private async Task UpsertOptionalAsync(
        string key,
        decimal? value,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeDecimal(value);
        if (normalized is null)
        {
            await using var deleteCommand = _dataSource.CreateCommand(
                """
                DELETE FROM app_settings
                WHERE key = @key;
                """);
            deleteCommand.Parameters.AddWithValue("key", key);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await UpsertAsync(key, normalized.Value.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertAsync(
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand(
            """
            INSERT INTO app_settings (key, value, updated_at_utc)
            VALUES (@key, @value, now())
            ON CONFLICT (key)
            DO UPDATE SET
                value = EXCLUDED.value,
                updated_at_utc = EXCLUDED.updated_at_utc;
            """);
        command.Parameters.AddWithValue("key", key);
        command.Parameters.AddWithValue("value", value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
                CREATE TABLE IF NOT EXISTS app_settings (
                    key text PRIMARY KEY,
                    value text NOT NULL,
                    updated_at_utc timestamp with time zone NOT NULL DEFAULT now()
                );
                """);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static bool ParseBoolean(string? value)
    {
        return bool.TryParse(value, out var result) && result;
    }

    private static decimal? ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? NormalizeDecimal(result)
            : null;
    }

    private static decimal? NormalizeDecimal(decimal? value)
    {
        return value is > 0 ? value : null;
    }
}
