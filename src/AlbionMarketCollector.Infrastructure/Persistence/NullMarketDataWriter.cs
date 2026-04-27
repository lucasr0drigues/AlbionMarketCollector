using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Domain.Models;

namespace AlbionMarketCollector.Infrastructure.Persistence;

public sealed class NullMarketDataWriter : IMarketDataWriter
{
    public Task UpsertMarketOrdersAsync(
        IReadOnlyCollection<MarketOrder> orders,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task UpsertMarketHistoryAsync(
        IReadOnlyCollection<MarketHistoryPoint> historyPoints,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task SaveCollectorStateAsync(
        CollectorStateSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
