using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.Application.Contracts;

public interface IPhotonParser
{
    IReadOnlyList<PhotonMessage> Parse(byte[] payload);
}
