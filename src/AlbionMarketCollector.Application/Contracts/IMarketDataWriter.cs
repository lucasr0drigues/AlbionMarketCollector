using AlbionMarketCollector.Domain.Models;

namespace AlbionMarketCollector.Application.Contracts;

public interface IMarketDataWriter
{
    Task UpsertMarketOrdersAsync(
        IReadOnlyCollection<MarketOrder> orders,
        CancellationToken cancellationToken);

    Task UpsertMarketHistoryAsync(
        IReadOnlyCollection<MarketHistoryPoint> historyPoints,
        CancellationToken cancellationToken);

    Task SaveCollectorStateAsync(
        CollectorStateSnapshot snapshot,
        CancellationToken cancellationToken);
}
