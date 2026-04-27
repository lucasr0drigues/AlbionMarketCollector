namespace AlbionMarketCollector.Application.Models;

public abstract record PhotonMessage;

public sealed record PhotonRequestMessage(
    byte OperationCode,
    IReadOnlyDictionary<byte, object?> Parameters) : PhotonMessage;

public sealed record PhotonResponseMessage(
    byte OperationCode,
    short ReturnCode,
    string? DebugMessage,
    IReadOnlyDictionary<byte, object?> Parameters) : PhotonMessage;

public sealed record PhotonEventMessage(
    byte EventCode,
    IReadOnlyDictionary<byte, object?> Parameters) : PhotonMessage;

public sealed record PhotonEncryptedMessage() : PhotonMessage;
