using AlbionMarketCollector.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AlbionMarketCollector.Api.Controllers;

[ApiController]
[Route("api/items")]
public sealed class ItemsController : ControllerBase
{
    private readonly IReferenceDataQueryService _queryService;

    public ItemsController(IReferenceDataQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] string? search,
        [FromQuery] int limit = 25,
        CancellationToken cancellationToken = default)
    {
        var results = await _queryService.SearchItemsAsync(search, limit, cancellationToken).ConfigureAwait(false);
        return Ok(results);
    }
}
