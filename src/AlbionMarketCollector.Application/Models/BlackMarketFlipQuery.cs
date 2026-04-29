namespace AlbionMarketCollector.Application.Models;

public sealed class BlackMarketFlipQuery
{
    public IReadOnlyList<string> SourceLocationIds { get; init; } = [];

    public IReadOnlyList<string> ExcludedSourceLocationIds { get; init; } = [];

    public IReadOnlyList<string> SellingLocationIds { get; init; } = [];

    public int? MaxAgeMinutes { get; init; }

    public long? MinProfitSilver { get; init; }

    public long? MinTotalProfitSilver { get; init; }

    public decimal? MinProfitPercent { get; init; }

    public string? ItemSearch { get; init; }

    public IReadOnlyList<string> ItemUniqueNames { get; init; } = [];

    public int? QualityLevel { get; init; }

    public int? EnchantmentLevel { get; init; }

    public string? SortBy { get; init; }

    public string? SortDirection { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 48;
}
