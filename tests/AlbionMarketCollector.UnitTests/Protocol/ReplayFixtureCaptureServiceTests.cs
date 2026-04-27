using System.Threading.Channels;
using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Infrastructure.Replay;
using AlbionMarketCollector.UnitTests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlbionMarketCollector.UnitTests.Protocol;

public sealed class ReplayFixtureCaptureServiceTests
{
    [Fact]
    public async Task StartAsync_ReplaysFixturePayloads()
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

            var service = new ReplayFixtureCaptureService(options, NullLogger<ReplayFixtureCaptureService>.Instance);
            var channel = Channel.CreateUnbounded<CapturedPayload>();

            await service.StartAsync(channel.Writer, CancellationToken.None);
            channel.Writer.TryComplete();

            var payloads = new List<CapturedPayload>();
            await foreach (var payload in channel.Reader.ReadAllAsync())
            {
                payloads.Add(payload);
            }

            Assert.Equal(7, payloads.Count);
            Assert.Equal("5.188.125.10", payloads[0].SourceIp);
            Assert.Equal(TransportProtocol.Tcp, payloads[0].TransportProtocol);
        }
        finally
        {
            File.Delete(fixturePath);
        }
    }
}
