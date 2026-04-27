using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using Microsoft.Extensions.Logging;

namespace AlbionMarketCollector.Infrastructure.Replay;

public sealed class ReplayFixtureCaptureService : ICaptureService
{
    private readonly CaptureOptions _options;
    private readonly ILogger<ReplayFixtureCaptureService> _logger;

    public ReplayFixtureCaptureService(
        CollectorOptions options,
        ILogger<ReplayFixtureCaptureService> logger)
    {
        _options = options.Capture;
        _logger = logger;
    }

    public async Task StartAsync(ChannelWriter<CapturedPayload> writer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ReplayFixturePath))
        {
            return;
        }

        var files = ResolveFixtureFiles(_options.ReplayFixturePath);
        var replayCount = 0;
        foreach (var file in files)
        {
            var fixtures = await LoadFixturesAsync(file, cancellationToken).ConfigureAwait(false);
            foreach (var fixture in fixtures)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var payload = new CapturedPayload(
                    fixture.SourceIp,
                    fixture.TransportProtocol,
                    Convert.FromBase64String(fixture.PayloadBase64),
                    fixture.CapturedAtUtc ?? DateTimeOffset.UtcNow);

                await writer.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                replayCount++;

                if (_options.ReplayDelayMilliseconds > 0)
                {
                    await Task.Delay(_options.ReplayDelayMilliseconds, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        _logger.LogInformation("Replay completed with {ReplayCount} payloads from {FixturePath}.", replayCount, _options.ReplayFixturePath);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static IReadOnlyList<string> ResolveFixtureFiles(string replayFixturePath)
    {
        if (File.Exists(replayFixturePath))
        {
            return [replayFixturePath];
        }

        if (Directory.Exists(replayFixturePath))
        {
            return Directory
                .EnumerateFiles(replayFixturePath, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        throw new FileNotFoundException($"Replay fixture path '{replayFixturePath}' was not found.");
    }

    private static async Task<IReadOnlyList<ReplayFixtureEnvelope>> LoadFixturesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var fixtures = await JsonSerializer.DeserializeAsync<List<ReplayFixtureEnvelope>>(
            stream,
            new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return fixtures ?? [];
    }

    private sealed class ReplayFixtureEnvelope
    {
        public string SourceIp { get; set; } = string.Empty;

        public TransportProtocol TransportProtocol { get; set; }

        public string PayloadBase64 { get; set; } = string.Empty;

        public DateTimeOffset? CapturedAtUtc { get; set; }
    }
}
