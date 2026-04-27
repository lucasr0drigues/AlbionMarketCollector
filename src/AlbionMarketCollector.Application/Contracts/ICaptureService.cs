using System.Threading.Channels;
using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.Application.Contracts;

public interface ICaptureService
{
    Task StartAsync(ChannelWriter<CapturedPayload> writer, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
