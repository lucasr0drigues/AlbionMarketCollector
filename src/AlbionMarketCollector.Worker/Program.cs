using System.IO;
using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Pipeline;
using AlbionMarketCollector.Infrastructure.AlbionProtocol;
using AlbionMarketCollector.Infrastructure.Capture;
using AlbionMarketCollector.Infrastructure.Persistence;
using AlbionMarketCollector.Infrastructure.Protocol;
using AlbionMarketCollector.Infrastructure.Replay;
using AlbionMarketCollector.Infrastructure.State;
using AlbionMarketCollector.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

var collectorOptions = builder.Configuration
    .GetSection("AlbionMarketCollector")
    .Get<CollectorOptions>() ?? new CollectorOptions();

ValidateOptions(collectorOptions);

builder.Services.AddSingleton(collectorOptions);
builder.Services.AddSingleton<IMarketStateStore, InMemoryMarketStateStore>();
builder.Services.AddSingleton<IPhotonParser, PhotonParser>();
builder.Services.AddSingleton<IAlbionProtocolDecoder, AlbionProtocolDecoder>();
builder.Services.AddSingleton<MarketProjectionService>();
builder.Services.AddSingleton<IMarketPipelineService, MarketPipelineService>();
builder.Services.AddSingleton<IMarketDataWriter>(serviceProvider =>
{
    return collectorOptions.Persistence.Provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<PostgreSqlMarketDataWriter>(serviceProvider)
        : ActivatorUtilities.CreateInstance<NullMarketDataWriter>(serviceProvider);
});
builder.Services.AddSingleton<ICaptureService>(serviceProvider =>
{
    if (!string.IsNullOrWhiteSpace(collectorOptions.Capture.ReplayFixturePath))
    {
        return ActivatorUtilities.CreateInstance<ReplayFixtureCaptureService>(serviceProvider);
    }

    return collectorOptions.Capture.EnableLiveCapture
        ? ActivatorUtilities.CreateInstance<SharpPcapCaptureService>(serviceProvider)
        : ActivatorUtilities.CreateInstance<NullCaptureService>(serviceProvider);
});
builder.Services.AddHostedService<MarketCaptureWorker>();

var host = builder.Build();
host.Run();

static void ValidateOptions(CollectorOptions options)
{
    if (options.Capture.EnableLiveCapture && !string.IsNullOrWhiteSpace(options.Capture.ReplayFixturePath))
    {
        throw new InvalidOperationException("Choose either live capture or replay mode, not both.");
    }

    if (options.Persistence.Provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) &&
        string.IsNullOrWhiteSpace(options.Persistence.PostgreSql.ConnectionString) &&
        string.IsNullOrWhiteSpace(options.Persistence.ConnectionString))
    {
        throw new InvalidOperationException("PostgreSQL persistence requires AlbionMarketCollector:Persistence:PostgreSql:ConnectionString.");
    }
}
