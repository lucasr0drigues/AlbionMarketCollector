namespace AlbionMarketCollector.Application.Models;

public sealed record AppSettings(
    bool Premium,
    decimal? DefaultMinTotalProfitSilver,
    decimal? DefaultMinProfitPercent);

public sealed record UpdateAppSettingsRequest(
    bool Premium,
    decimal? DefaultMinTotalProfitSilver,
    decimal? DefaultMinProfitPercent);
