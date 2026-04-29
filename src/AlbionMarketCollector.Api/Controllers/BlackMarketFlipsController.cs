using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace AlbionMarketCollector.Api.Controllers;

[ApiController]
[Route("api/flips/black-market")]
public sealed class BlackMarketFlipsController : ControllerBase
{
    private readonly IBlackMarketFlipQueryService _queryService;

    public BlackMarketFlipsController(IBlackMarketFlipQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string[]? sourceLocationIds,
        [FromQuery] string[]? excludedSourceLocationIds,
        [FromQuery] string[]? sellingLocationIds,
        [FromQuery] string? sourceLocationId,
        [FromQuery] string? blackMarketLocationId,
        [FromQuery] int? maxAgeMinutes = null,
        [FromQuery] long? minProfitSilver = null,
        [FromQuery] long? minTotalProfitSilver = null,
        [FromQuery] decimal? minProfitPercent = null,
        [FromQuery] string? itemSearch = null,
        [FromQuery] string[]? itemUniqueNames = null,
        [FromQuery] int? qualityLevel = null,
        [FromQuery] int? enchantmentLevel = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 48,
        CancellationToken cancellationToken = default)
    {
        var normalizedSourceLocationIds = NormalizeValues(sourceLocationIds);
        if (!string.IsNullOrWhiteSpace(sourceLocationId))
        {
            normalizedSourceLocationIds.Add(sourceLocationId.Trim());
        }

        var normalizedSellingLocationIds = NormalizeValues(sellingLocationIds);
        if (!string.IsNullOrWhiteSpace(blackMarketLocationId))
        {
            normalizedSellingLocationIds.Add(blackMarketLocationId.Trim());
        }

        var results = await _queryService
            .FindOpportunitiesAsync(
                new BlackMarketFlipQuery
                {
                    SourceLocationIds = normalizedSourceLocationIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    ExcludedSourceLocationIds = NormalizeValues(excludedSourceLocationIds).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    SellingLocationIds = normalizedSellingLocationIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    MaxAgeMinutes = maxAgeMinutes,
                    MinProfitSilver = minProfitSilver,
                    MinTotalProfitSilver = minTotalProfitSilver,
                    MinProfitPercent = minProfitPercent,
                    ItemSearch = itemSearch,
                    ItemUniqueNames = NormalizeValues(itemUniqueNames).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    QualityLevel = qualityLevel,
                    EnchantmentLevel = enchantmentLevel,
                    SortBy = sortBy,
                    SortDirection = sortDirection,
                    Page = page,
                    PageSize = pageSize,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(results);
    }

    private static List<string> NormalizeValues(string[]? values)
    {
        return values?
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList() ?? [];
    }
}
