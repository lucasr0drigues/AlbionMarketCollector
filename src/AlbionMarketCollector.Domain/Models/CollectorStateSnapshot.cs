namespace AlbionMarketCollector.Domain.Models;

public sealed class CollectorStateSnapshot
{
    public string CollectorKey { get; set; } = "default";

    public int CurrentServerId { get; set; }

    public string CurrentLocationId { get; set; } = string.Empty;

    public string CharacterId { get; set; } = string.Empty;

    public string CharacterName { get; set; } = string.Empty;

    public string GameServerIp { get; set; } = string.Empty;

    public bool WaitingForMarketData { get; set; }

    public DateTimeOffset? LastPacketAtUtc { get; set; }

    public DateTimeOffset? LastLocationUpdateAtUtc { get; set; }
}
