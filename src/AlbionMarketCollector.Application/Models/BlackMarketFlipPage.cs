namespace AlbionMarketCollector.Application.Models;

public sealed record BlackMarketFlipPage(
    IReadOnlyList<BlackMarketFlipOpportunity> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages,
    bool HasMore);
