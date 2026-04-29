using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.Application.Contracts;

public interface IBlackMarketFlipQueryService
{
    Task<BlackMarketFlipPage> FindOpportunitiesAsync(
        BlackMarketFlipQuery query,
        CancellationToken cancellationToken);
}
