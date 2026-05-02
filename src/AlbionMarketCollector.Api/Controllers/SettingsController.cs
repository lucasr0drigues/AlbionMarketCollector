using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace AlbionMarketCollector.Api.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly IAppSettingsService _settingsService;

    public SettingsController(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAsync(cancellationToken).ConfigureAwait(false);
        return Ok(settings);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] UpdateAppSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var settings = await _settingsService.UpdateAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(settings);
    }
}
