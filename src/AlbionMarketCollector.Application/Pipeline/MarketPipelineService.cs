using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace AlbionMarketCollector.Application.Pipeline;

public sealed class MarketPipelineService : IMarketPipelineService
{
    private readonly IMarketStateStore _stateStore;
    private readonly IPhotonParser _photonParser;
    private readonly IAlbionProtocolDecoder _albionProtocolDecoder;
    private readonly MarketProjectionService _projectionService;
    private readonly IMarketDataWriter _marketDataWriter;
    private readonly ILogger<MarketPipelineService> _logger;

    public MarketPipelineService(
        IMarketStateStore stateStore,
        IPhotonParser photonParser,
        IAlbionProtocolDecoder albionProtocolDecoder,
        MarketProjectionService projectionService,
        IMarketDataWriter marketDataWriter,
        ILogger<MarketPipelineService> logger)
    {
        _stateStore = stateStore;
        _photonParser = photonParser;
        _albionProtocolDecoder = albionProtocolDecoder;
        _projectionService = projectionService;
        _marketDataWriter = marketDataWriter;
        _logger = logger;
    }

    public async Task ProcessAsync(CapturedPayload payload, CancellationToken cancellationToken)
    {
        _stateStore.TrackPacketSource(payload.SourceIp, payload.CapturedAtUtc);

        var photonMessages = _photonParser.Parse(payload.Payload);
        if (photonMessages.Count > 0)
        {
            var albionMessages = _albionProtocolDecoder.Decode(photonMessages, payload.CapturedAtUtc);
            _logger.LogDebug(
                "Processed payload from {SourceIp} into {PhotonCount} photon messages and {AlbionCount} Albion messages. Types=[{AlbionMessageTypes}]",
                payload.SourceIp,
                photonMessages.Count,
                albionMessages.Count,
                FormatAlbionMessageTypes(albionMessages));
            await _projectionService.ApplyAsync(albionMessages, cancellationToken).ConfigureAwait(false);
        }

        await _marketDataWriter
            .SaveCollectorStateAsync(_stateStore.GetSnapshot(), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string FormatAlbionMessageTypes(IReadOnlyList<AlbionMessage> messages)
    {
        if (messages.Count == 0)
        {
            return "<none>";
        }

        return string.Join(
            ", ",
            messages
                .GroupBy(static message => message.GetType().Name)
                .OrderBy(static group => group.Key, StringComparer.Ordinal)
                .Select(static group => group.Count() == 1
                    ? group.Key
                    : string.Create(
                        CultureInfo.InvariantCulture,
                        $"{group.Key}x{group.Count()}")));
    }
}
