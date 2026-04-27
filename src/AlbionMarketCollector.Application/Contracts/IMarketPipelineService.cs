using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.Application.Contracts;

public interface IMarketPipelineService
{
    Task ProcessAsync(CapturedPayload payload, CancellationToken cancellationToken);
}
