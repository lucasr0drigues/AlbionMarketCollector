using System.Threading.Channels;
using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlbionMarketCollector.Worker;

public sealed class MarketCaptureWorker : BackgroundService
{
    private readonly CollectorOptions _collectorOptions;
    private readonly ICaptureService _captureService;
    private readonly IMarketPipelineService _marketPipelineService;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<MarketCaptureWorker> _logger;

    public MarketCaptureWorker(
        CollectorOptions collectorOptions,
        ICaptureService captureService,
        IMarketPipelineService marketPipelineService,
        IHostApplicationLifetime applicationLifetime,
        ILogger<MarketCaptureWorker> logger)
    {
        _collectorOptions = collectorOptions;
        _captureService = captureService;
        _marketPipelineService = marketPipelineService;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = Channel.CreateBounded<CapturedPayload>(new BoundedChannelOptions(_collectorOptions.ChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

        _logger.LogInformation(
            "Starting market capture worker with live capture {LiveCapture} on port {Port}.",
            _collectorOptions.Capture.EnableLiveCapture,
            _collectorOptions.Capture.Port);

        var captureTask = RunCaptureLoopAsync(channel.Writer, stoppingToken);
        try
        {
            await foreach (var payload in channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await _marketPipelineService.ProcessAsync(payload, stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await captureTask.ConfigureAwait(false);
            _logger.LogInformation("Market capture worker completed. Stopping host.");
            _applicationLifetime.StopApplication();
        }
    }

    private async Task RunCaptureLoopAsync(
        ChannelWriter<CapturedPayload> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await _captureService.StartAsync(writer, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            writer.TryComplete();
            await _captureService.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
