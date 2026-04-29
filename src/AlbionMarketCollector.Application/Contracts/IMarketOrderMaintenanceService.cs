namespace AlbionMarketCollector.Application.Contracts;

public interface IMarketOrderMaintenanceService
{
    Task<int> DeleteByLocationIdsAsync(
        IReadOnlyCollection<string> locationIds,
        CancellationToken cancellationToken);
}
