using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.Application.Contracts;

public interface IAlbionProtocolDecoder
{
    IReadOnlyList<AlbionMessage> Decode(
        IReadOnlyList<PhotonMessage> messages,
        DateTimeOffset observedAtUtc);
}
