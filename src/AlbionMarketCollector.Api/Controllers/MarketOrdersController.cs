using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace AlbionMarketCollector.Api.Controllers;

[ApiController]
[Route("api/market-orders")]
public sealed class MarketOrdersController : ControllerBase
{
    private readonly IMarketDataQueryService _queryService;
    private readonly IMarketOrderMaintenanceService _maintenanceService;

    public MarketOrdersController(
        IMarketDataQueryService queryService,
        IMarketOrderMaintenanceService maintenanceService)
    {
        _queryService = queryService;
        _maintenanceService = maintenanceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? locationId,
        [FromQuery] string? itemSearch,
        [FromQuery] string? orderType,
        [FromQuery] int? maxAgeMinutes,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        MarketOrderType? parsedOrderType = null;
        if (!string.IsNullOrWhiteSpace(orderType))
        {
            if (!Enum.TryParse<MarketOrderType>(orderType, ignoreCase: true, out var parsed))
            {
                return BadRequest("orderType must be Sell or Buy.");
            }

            parsedOrderType = parsed;
        }

        var results = await _queryService
            .GetMarketOrdersAsync(
                new MarketOrderQuery
                {
                    LocationId = locationId,
                    ItemSearch = itemSearch,
                    OrderType = parsedOrderType,
                    MaxAgeMinutes = maxAgeMinutes,
                    Limit = limit,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(results);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAsync(
        [FromQuery] string[]? locationIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedLocationIds = locationIds?
            .Select(locationId => locationId.Trim())
            .Where(locationId => !string.IsNullOrWhiteSpace(locationId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (normalizedLocationIds.Length == 0)
        {
            return BadRequest("At least one locationIds value is required.");
        }

        var deletedCount = await _maintenanceService
            .DeleteByLocationIdsAsync(normalizedLocationIds, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new { deletedCount });
    }
}
