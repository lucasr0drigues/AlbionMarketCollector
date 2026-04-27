using System.Text.Json;
using AlbionMarketCollector.Application.Models;

namespace AlbionMarketCollector.UnitTests.Fixtures;

internal static class ReplayFixtureBuilder
{
    public static string CreateMarketFlowFixtureFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"albion-market-flow-{Guid.NewGuid():N}.json");
        var fixtures = new[]
        {
            CreateEnvelope(
                "2026-04-27T12:00:00Z",
                PhotonFixtureBuilder.CreateResponsePacket(
                    2,
                    0,
                    null,
                    new Dictionary<byte, object?>
                    {
                        [1] = new byte[]
                        {
                            0x78, 0x56, 0x34, 0x12,
                            0xBC, 0x9A,
                            0xF0, 0xDE,
                            0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF,
                        },
                        [2] = "Lucas",
                        [8] = "3005",
                    })),
            CreateEnvelope(
                "2026-04-27T12:00:01Z",
                PhotonFixtureBuilder.CreateRequestPacket(81, new Dictionary<byte, object?>())),
            CreateEnvelope(
                "2026-04-27T12:00:02Z",
                PhotonFixtureBuilder.CreateResponsePacket(
                    81,
                    0,
                    new[]
                    {
                        "{\"Id\":101,\"ItemTypeId\":\"T4_BAG\",\"ItemGroupTypeId\":\"T4_BAG\",\"LocationId\":\"\",\"QualityLevel\":1,\"EnchantmentLevel\":0,\"UnitPriceSilver\":1200,\"Amount\":3,\"AuctionType\":\"offer\",\"Expires\":\"2026-04-27T12:00:00Z\"}",
                    },
                    new Dictionary<byte, object?>())),
            CreateEnvelope(
                "2026-04-27T12:00:03Z",
                PhotonFixtureBuilder.CreateResponsePacket(
                    82,
                    0,
                    new[]
                    {
                        "{\"Id\":201,\"ItemTypeId\":\"T4_BAG\",\"ItemGroupTypeId\":\"T4_BAG\",\"LocationId\":\"3005\",\"QualityLevel\":1,\"EnchantmentLevel\":0,\"UnitPriceSilver\":1100,\"Amount\":2,\"AuctionType\":\"request\",\"Expires\":\"2026-04-27T12:30:00Z\"}",
                    },
                    new Dictionary<byte, object?>())),
            CreateEnvelope(
                "2026-04-27T12:00:04Z",
                PhotonFixtureBuilder.CreateRequestPacket(
                    95,
                    new Dictionary<byte, object?>
                    {
                        [1] = -121,
                        [2] = (byte)2,
                        [3] = (byte)1,
                        [4] = 0,
                        [255] = 77UL,
                    })),
            CreateEnvelope(
                "2026-04-27T12:00:05Z",
                PhotonFixtureBuilder.CreateResponsePacket(
                    95,
                    0,
                    null,
                    new Dictionary<byte, object?>
                    {
                        [0] = new long[] { -121, 9 },
                        [1] = new ulong[] { 100UL, 200UL },
                        [2] = new ulong[] { 1_700_000_001UL, 1_700_000_002UL },
                        [255] = 77UL,
                    })),
            CreateEnvelope(
                "2026-04-27T12:00:06Z",
                PhotonFixtureBuilder.CreateEncryptedPacket()),
        };

        File.WriteAllText(path, JsonSerializer.Serialize(fixtures, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static ReplayFixtureEnvelope CreateEnvelope(string capturedAtUtc, byte[] payload)
    {
        return new ReplayFixtureEnvelope
        {
            SourceIp = "5.188.125.10",
            TransportProtocol = TransportProtocol.Tcp,
            CapturedAtUtc = DateTimeOffset.Parse(capturedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
            PayloadBase64 = Convert.ToBase64String(payload),
        };
    }

    private sealed class ReplayFixtureEnvelope
    {
        public string SourceIp { get; set; } = string.Empty;

        public TransportProtocol TransportProtocol { get; set; }

        public DateTimeOffset CapturedAtUtc { get; set; }

        public string PayloadBase64 { get; set; } = string.Empty;
    }
}
