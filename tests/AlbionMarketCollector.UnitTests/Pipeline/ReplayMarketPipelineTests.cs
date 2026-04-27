using System.Threading.Channels;
using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Application.Pipeline;
using AlbionMarketCollector.Domain.Models;
using AlbionMarketCollector.Infrastructure.AlbionProtocol;
using AlbionMarketCollector.Infrastructure.Protocol;
using AlbionMarketCollector.Infrastructure.Replay;
using AlbionMarketCollector.Infrastructure.State;
using AlbionMarketCollector.UnitTests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlbionMarketCollector.UnitTests.Pipeline;

public sealed class ReplayMarketPipelineTests
{
    [Fact]
    public async Task ReplayFixture_ProducesOrdersAndMarketHistoryThroughPipeline()
    {
        var fixturePath = ReplayFixtureBuilder.CreateMarketFlowFixtureFile();
        try
        {
            var options = new CollectorOptions
            {
                Capture = new CaptureOptions
                {
                    ReplayFixturePath = fixturePath,
                },
            };

            var captureService = new ReplayFixtureCaptureService(options, NullLogger<ReplayFixtureCaptureService>.Instance);
            var channel = Channel.CreateUnbounded<CapturedPayload>();
            await captureService.StartAsync(channel.Writer, CancellationToken.None);
            channel.Writer.TryComplete();

            var stateStore = new InMemoryMarketStateStore(options);
            var writer = new RecordingMarketDataWriter();
            var projectionService = new MarketProjectionService(stateStore, writer, NullLogger<MarketProjectionService>.Instance);
            var pipelineService = new MarketPipelineService(
                stateStore,
                new PhotonParser(),
                new AlbionProtocolDecoder(NullLogger<AlbionProtocolDecoder>.Instance),
                projectionService,
                writer,
                NullLogger<MarketPipelineService>.Instance);

            await foreach (var payload in channel.Reader.ReadAllAsync())
            {
                await pipelineService.ProcessAsync(payload, CancellationToken.None);
            }

            Assert.Equal(2, writer.Orders.Count);
            Assert.Equal(2, writer.HistoryPoints.Count);
            Assert.All(writer.HistoryPoints, static point => Assert.Equal("3005", point.LocationId));
            Assert.Contains(writer.HistoryPoints, static point => point.ItemTypeId == "135" && point.ItemAmount == 135);
        }
        finally
        {
            File.Delete(fixturePath);
        }
    }

    private sealed class RecordingMarketDataWriter : IMarketDataWriter
    {
        public List<MarketOrder> Orders { get; } = [];

        public List<MarketHistoryPoint> HistoryPoints { get; } = [];

        public CollectorStateSnapshot? Snapshot { get; private set; }

        public Task UpsertMarketOrdersAsync(IReadOnlyCollection<MarketOrder> orders, CancellationToken cancellationToken)
        {
            Orders.AddRange(orders);
            return Task.CompletedTask;
        }

        public Task UpsertMarketHistoryAsync(IReadOnlyCollection<MarketHistoryPoint> historyPoints, CancellationToken cancellationToken)
        {
            HistoryPoints.AddRange(historyPoints);
            return Task.CompletedTask;
        }

        public Task SaveCollectorStateAsync(CollectorStateSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }
    }
}
