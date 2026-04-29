using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.Application.Contracts;

public interface IMarketDataQueryService
{
    Task<IReadOnlyList<MarketOrderResult>> GetMarketOrdersAsync(
        MarketOrderQuery query,
        CancellationToken cancellationToken);
}
