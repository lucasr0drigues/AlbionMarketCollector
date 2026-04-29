using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.Application.Contracts;

public interface IBlackMarketFlipQueryService
{
    Task<IReadOnlyList<BlackMarketFlipOpportunity>> FindOpportunitiesAsync(
        BlackMarketFlipQuery query,
        CancellationToken cancellationToken);
}
