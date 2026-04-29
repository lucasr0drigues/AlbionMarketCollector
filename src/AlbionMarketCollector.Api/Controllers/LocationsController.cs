using AlbionMarketCollector.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AlbionMarketCollector.Api.Controllers;

[ApiController]
[Route("api/locations")]
public sealed class LocationsController : ControllerBase
{
    private readonly IReferenceDataQueryService _queryService;

    public LocationsController(IReferenceDataQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] string? search,
        [FromQuery] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var results = await _queryService.SearchLocationsAsync(search, limit, cancellationToken).ConfigureAwait(false);
        return Ok(results);
    }
}
