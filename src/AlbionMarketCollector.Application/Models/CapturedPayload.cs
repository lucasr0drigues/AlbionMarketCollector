namespace AlbionMarketCollector.Application.Models;

public sealed record CapturedPayload(
    string SourceIp,
    TransportProtocol TransportProtocol,
    byte[] Payload,
    DateTimeOffset CapturedAtUtc);
