using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Channels;
using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using Microsoft.Extensions.Logging;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

namespace AlbionMarketCollector.Infrastructure.Capture;

public sealed class SharpPcapCaptureService : ICaptureService
{
    private static readonly string[] VirtualMacPrefixes =
    [
        "ac:de:48:00:11:22",
        "00:03:ff",
        "00:15:5d",
        "0a:00:27",
        "00:00:00:00:00",
        "00:50:56",
        "00:1c:14",
        "00:0c:29",
        "00:05:69",
        "00:1c:42",
        "00:0f:4b",
        "00:16:3e",
        "08:00:27",
    ];

    private readonly CaptureOptions _options;
    private readonly CollectorDebugOptions _debugOptions;
    private readonly ILogger<SharpPcapCaptureService> _logger;
    private readonly List<LibPcapLiveDevice> _activeDevices = [];
    private readonly object _dumpLock = new();
    private readonly List<ReplayFixtureEnvelope> _dumpedPayloads = [];
    private ChannelWriter<CapturedPayload>? _writer;

    public SharpPcapCaptureService(
        CollectorOptions options,
        ILogger<SharpPcapCaptureService> logger)
    {
        _options = options.Capture;
        _debugOptions = options.Debug;
        _logger = logger;
    }

    public async Task StartAsync(ChannelWriter<CapturedPayload> writer, CancellationToken cancellationToken)
    {
        _writer = writer;

        var devices = SelectDevices();
        if (devices.Count == 0)
        {
            throw new InvalidOperationException("No capture devices matched the current configuration.");
        }

        var filter = $"tcp port {_options.Port} or udp port {_options.Port}";
        foreach (var device in devices)
        {
            device.Open(DeviceModes.Promiscuous, read_timeout: 1000);
            device.Filter = filter;
            device.OnPacketArrival += OnPacketArrival;
            device.StartCapture();
            _activeDevices.Add(device);

            _logger.LogInformation(
                "Started capture on device {DeviceName} ({Description}) with filter {Filter}.",
                device.Name,
                device.Description,
                filter);
        }

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var device in _activeDevices)
        {
            try
            {
                device.OnPacketArrival -= OnPacketArrival;
                device.StopCapture();
            }
            catch
            {
            }
            finally
            {
                device.Dispose();
            }
        }

        _activeDevices.Clear();
        FlushCapturedPayloadDump();
        return Task.CompletedTask;
    }

    private void OnPacketArrival(object sender, PacketCapture packetCapture)
    {
        if (_writer is null)
        {
            return;
        }

        try
        {
            var rawCapture = packetCapture.GetPacket();
            var packet = Packet.ParsePacket(rawCapture.LinkLayerType, rawCapture.Data);
            var ipv4Packet = packet.Extract<IPv4Packet>();
            if (ipv4Packet is null)
            {
                return;
            }

            var tcpPacket = packet.Extract<TcpPacket>();
            if (tcpPacket?.PayloadData is { Length: > 0 })
            {
                WritePayload(ipv4Packet.SourceAddress?.ToString(), TransportProtocol.Tcp, tcpPacket.PayloadData, rawCapture.Timeval.Date);
                return;
            }

            var udpPacket = packet.Extract<UdpPacket>();
            if (udpPacket?.PayloadData is { Length: > 0 })
            {
                WritePayload(ipv4Packet.SourceAddress?.ToString(), TransportProtocol.Udp, udpPacket.PayloadData, rawCapture.Timeval.Date);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignoring packet that could not be parsed.");
        }
    }

    private void WritePayload(string? sourceIp, TransportProtocol protocol, byte[] payload, DateTime capturedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(sourceIp) || _writer is null)
        {
            return;
        }

        if (_debugOptions.LogRawPayloads)
        {
            _logger.LogDebug(
                "Captured {Protocol} payload from {SourceIp} with {Length} bytes.",
                protocol,
                sourceIp,
                payload.Length);
        }

        if (!string.IsNullOrWhiteSpace(_debugOptions.DumpCapturedPayloadsPath))
        {
            lock (_dumpLock)
            {
                _dumpedPayloads.Add(new ReplayFixtureEnvelope
                {
                    SourceIp = sourceIp,
                    TransportProtocol = protocol.ToString(),
                    CapturedAtUtc = new DateTimeOffset(DateTime.SpecifyKind(capturedAtUtc, DateTimeKind.Utc)),
                    PayloadBase64 = Convert.ToBase64String(payload),
                });
            }
        }

        _writer.WriteAsync(
            new CapturedPayload(
                sourceIp,
                protocol,
                payload.ToArray(),
                new DateTimeOffset(DateTime.SpecifyKind(capturedAtUtc, DateTimeKind.Utc))),
            CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    private void FlushCapturedPayloadDump()
    {
        if (string.IsNullOrWhiteSpace(_debugOptions.DumpCapturedPayloadsPath))
        {
            return;
        }

        List<ReplayFixtureEnvelope> snapshot;
        lock (_dumpLock)
        {
            if (_dumpedPayloads.Count == 0)
            {
                return;
            }

            snapshot = [.. _dumpedPayloads];
            _dumpedPayloads.Clear();
        }

        var dumpPath = Path.GetFullPath(_debugOptions.DumpCapturedPayloadsPath);
        var parentDirectory = Path.GetDirectoryName(dumpPath);
        if (!string.IsNullOrWhiteSpace(parentDirectory))
        {
            Directory.CreateDirectory(parentDirectory);
        }

        File.WriteAllText(
            dumpPath,
            JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));

        _logger.LogInformation(
            "Wrote {PayloadCount} captured payloads to {DumpPath}.",
            snapshot.Count,
            dumpPath);
    }

    private IReadOnlyList<LibPcapLiveDevice> SelectDevices()
    {
        var configuredFilters = _options.ListenDevices
            .Select(static value => value.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var devices = CaptureDeviceList.Instance
            .OfType<LibPcapLiveDevice>()
            .Where(device => MatchesConfiguredFilters(device, configuredFilters) || (configuredFilters.Length == 0 && IsDefaultPhysicalDevice(device)))
            .ToArray();

        _logger.LogInformation("Selected {DeviceCount} capture devices.", devices.Length);
        return devices;
    }

    private static bool MatchesConfiguredFilters(LibPcapLiveDevice device, IReadOnlyList<string> configuredFilters)
    {
        if (configuredFilters.Count == 0)
        {
            return false;
        }

        var normalizedMac = NormalizeMac(device.MacAddress);
        return configuredFilters.Any(filter =>
            (normalizedMac is not null && normalizedMac.StartsWith(NormalizeFilter(filter), StringComparison.OrdinalIgnoreCase)) ||
            device.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(device.Description) && device.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsDefaultPhysicalDevice(LibPcapLiveDevice device)
    {
        var normalizedMac = NormalizeMac(device.MacAddress);
        if (string.IsNullOrWhiteSpace(normalizedMac))
        {
            return false;
        }

        if (VirtualMacPrefixes.Any(prefix => normalizedMac.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (device.Name.Contains("loopback", StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(device.Description) && device.Description.Contains("loopback", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var physicalAddress = device.MacAddress?.GetAddressBytes();
        if (physicalAddress is null || physicalAddress.Length == 0)
        {
            return false;
        }

        var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(nic => nic.GetPhysicalAddress().GetAddressBytes().SequenceEqual(physicalAddress));

        return networkInterface is not null &&
               networkInterface.OperationalStatus == OperationalStatus.Up &&
               networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
               networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel;
    }

    private static string NormalizeFilter(string value)
    {
        return value.Replace('-', ':').ToLowerInvariant();
    }

    private static string? NormalizeMac(PhysicalAddress? macAddress)
    {
        if (macAddress is null)
        {
            return null;
        }

        var bytes = macAddress.GetAddressBytes();
        if (bytes.Length == 0)
        {
            return null;
        }

        return string.Join(':', bytes.Select(static value => value.ToString("x2")));
    }

    private sealed class ReplayFixtureEnvelope
    {
        public string SourceIp { get; set; } = string.Empty;

        public string TransportProtocol { get; set; } = string.Empty;

        public DateTimeOffset? CapturedAtUtc { get; set; }

        public string PayloadBase64 { get; set; } = string.Empty;
    }
}
