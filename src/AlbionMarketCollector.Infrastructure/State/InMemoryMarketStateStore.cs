using System.Text.RegularExpressions;
using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Domain.Models;

namespace AlbionMarketCollector.Infrastructure.State;

public sealed class InMemoryMarketStateStore : IMarketStateStore
{
    private const int CacheSize = 8192;
    private static readonly TimeSpan PendingHistoryTtl = TimeSpan.FromSeconds(30);

    private static readonly Regex NumericLocationRegex = new("^[0-9]{3,6}$", RegexOptions.Compiled);
    private static readonly Regex IslandRegex = new("(?i)@island@[0-9a-f-]{36}", RegexOptions.Compiled);

    private readonly object _gate = new();
    private readonly PendingMarketHistoryRequest?[] _marketHistoryLookup = new PendingMarketHistoryRequest[CacheSize];
    private readonly CollectorStateSnapshot _snapshot;

    public InMemoryMarketStateStore(CollectorOptions options)
    {
        _snapshot = new CollectorStateSnapshot
        {
            CollectorKey = string.IsNullOrWhiteSpace(options.CollectorKey) ? "default" : options.CollectorKey,
        };
    }

    public CollectorStateSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new CollectorStateSnapshot
            {
                CollectorKey = _snapshot.CollectorKey,
                CurrentServerId = _snapshot.CurrentServerId,
                CurrentLocationId = _snapshot.CurrentLocationId,
                CharacterId = _snapshot.CharacterId,
                CharacterName = _snapshot.CharacterName,
                GameServerIp = _snapshot.GameServerIp,
                WaitingForMarketData = _snapshot.WaitingForMarketData,
                LastPacketAtUtc = _snapshot.LastPacketAtUtc,
                LastLocationUpdateAtUtc = _snapshot.LastLocationUpdateAtUtc,
            };
        }
    }

    public void TrackPacketSource(string sourceIp, DateTimeOffset observedAtUtc)
    {
        lock (_gate)
        {
            _snapshot.GameServerIp = sourceIp;
            _snapshot.LastPacketAtUtc = observedAtUtc;
            _snapshot.CurrentServerId = ResolveServerId(sourceIp, _snapshot.CurrentServerId);
        }
    }

    public void SetWaitingForMarketData(bool isWaiting)
    {
        lock (_gate)
        {
            _snapshot.WaitingForMarketData = isWaiting;
        }
    }

    public void UpdateLocation(string? locationId, DateTimeOffset observedAtUtc)
    {
        var normalized = NormalizeLocation(locationId);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        lock (_gate)
        {
            _snapshot.CurrentLocationId = normalized;
            _snapshot.LastLocationUpdateAtUtc = observedAtUtc;
        }
    }

    public void UpdateCharacter(string? characterId, string? characterName)
    {
        lock (_gate)
        {
            _snapshot.CharacterId = characterId ?? string.Empty;
            _snapshot.CharacterName = characterName ?? string.Empty;
        }
    }

    public void CachePendingHistoryRequest(PendingMarketHistoryRequest request)
    {
        var index = (int)(request.MessageId % CacheSize);
        lock (_gate)
        {
            if (_marketHistoryLookup[index] is { } existing &&
                request.CapturedAtUtc - existing.CapturedAtUtc > PendingHistoryTtl)
            {
                _marketHistoryLookup[index] = null;
            }

            _marketHistoryLookup[index] = request;
        }
    }

    public bool TryTakePendingHistoryRequest(ulong messageId, DateTimeOffset observedAtUtc, out PendingMarketHistoryRequest? request)
    {
        var index = (int)(messageId % CacheSize);
        lock (_gate)
        {
            request = _marketHistoryLookup[index];
            if (request is null || request.MessageId != messageId)
            {
                request = null;
                return false;
            }

            if (observedAtUtc - request.CapturedAtUtc > PendingHistoryTtl)
            {
                _marketHistoryLookup[index] = null;
                request = null;
                return false;
            }

            _marketHistoryLookup[index] = null;
            return true;
        }
    }

    public string NormalizeLocation(string? locationId)
    {
        var candidate = locationId?.Trim().Trim(',', '.');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return string.Empty;
        }

        var islandMatch = IslandRegex.Match(candidate);
        if (islandMatch.Success)
        {
            return "@ISLAND@" + islandMatch.Value["@island@".Length..];
        }

        if (NumericLocationRegex.IsMatch(candidate))
        {
            return candidate;
        }

        var lowerCandidate = candidate.ToLowerInvariant();
        if (lowerCandidate.StartsWith("island-player-") ||
            lowerCandidate.StartsWith("@player-island") ||
            lowerCandidate.StartsWith("@island-") ||
            candidate.StartsWith("BLACKBANK-", StringComparison.Ordinal) ||
            candidate.EndsWith("-HellDen", StringComparison.Ordinal) ||
            candidate.EndsWith("-Auction2", StringComparison.Ordinal))
        {
            return candidate;
        }

        return string.Empty;
    }

    private static int ResolveServerId(string sourceIp, int currentServerId)
    {
        if (sourceIp.StartsWith("5.188.125.", StringComparison.Ordinal))
        {
            return 1;
        }

        if (sourceIp.StartsWith("5.45.187.", StringComparison.Ordinal))
        {
            return 2;
        }

        if (sourceIp.StartsWith("193.169.238.", StringComparison.Ordinal))
        {
            return 3;
        }

        return currentServerId;
    }
}
