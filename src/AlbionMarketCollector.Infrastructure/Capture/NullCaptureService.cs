using System.Threading.Channels;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.Infrastructure.Capture;

public sealed class NullCaptureService : ICaptureService
{
    public async Task StartAsync(ChannelWriter<CapturedPayload> writer, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
