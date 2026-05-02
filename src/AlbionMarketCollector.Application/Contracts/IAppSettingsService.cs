using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.Application.Contracts;

public interface IAppSettingsService
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken);

    Task<AppSettings> UpdateAsync(
        UpdateAppSettingsRequest request,
        CancellationToken cancellationToken);
}
