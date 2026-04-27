using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.Infrastructure.AlbionProtocol;

public sealed class NoOpAlbionProtocolDecoder : IAlbionProtocolDecoder
{
    public IReadOnlyList<AlbionMessage> Decode(
        IReadOnlyList<PhotonMessage> messages,
        DateTimeOffset observedAtUtc)
    {
        return new AlbionProtocolDecoder().Decode(messages, observedAtUtc);
    }
}
