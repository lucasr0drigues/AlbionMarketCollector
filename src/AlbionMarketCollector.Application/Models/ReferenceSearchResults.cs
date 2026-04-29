namespace AlbionMarketCollector.Application.Models;

public sealed record ItemSearchResult(
    int Id,
    string UniqueName,
    string LocalizedName);

public sealed record LocationSearchResult(
    string Id,
    string Name);
