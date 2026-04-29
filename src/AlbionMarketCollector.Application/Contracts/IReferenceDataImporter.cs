using AlbionMarketCollector.Application.ReferenceData;

namespace AlbionMarketCollector.Application.Contracts;

public interface IReferenceDataImporter
{
    Task<ReferenceDataImportResult> ImportLocationsAsync(
        IReadOnlyCollection<LocationReference> locations,
        CancellationToken cancellationToken);

    Task<ReferenceDataImportResult> ImportItemsAsync(
        IReadOnlyCollection<ItemReference> items,
        CancellationToken cancellationToken);
}
