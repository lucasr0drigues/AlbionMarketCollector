using AlbionMarketCollector.Domain.Models;

namespace AlbionMarketCollector.Application.Contracts;

public interface IMarketStateStore
{
    CollectorStateSnapshot GetSnapshot();

    void TrackPacketSource(string sourceIp, DateTimeOffset observedAtUtc);

    void SetWaitingForMarketData(bool isWaiting);

    void UpdateLocation(string? locationId, DateTimeOffset observedAtUtc);

    void UpdateCharacter(string? characterId, string? characterName);

    void CachePendingHistoryRequest(PendingMarketHistoryRequest request);

    bool TryTakePendingHistoryRequest(ulong messageId, DateTimeOffset observedAtUtc, out PendingMarketHistoryRequest? request);

    string NormalizeLocation(string? locationId);
}
