using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.Application.Contracts;

public interface IReferenceDataQueryService
{
    Task<IReadOnlyList<ItemSearchResult>> SearchItemsAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LocationSearchResult>> SearchLocationsAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken);
}
