using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Infrastructure.AlbionProtocol;

namespace AlbionMarketCollector.UnitTests.AlbionProtocol;

public sealed class AlbionProtocolDecoderTests
{
    private readonly AlbionProtocolDecoder _decoder = new();

    [Fact]
    public void Decode_JoinResponse_MapsCharacterAndLocation()
    {
        var characterBytes = new byte[]
        {
            0x78, 0x56, 0x34, 0x12,
            0xBC, 0x9A,
            0xF0, 0xDE,
            0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF,
        };

        var messages = _decoder.Decode(
            [
                new PhotonResponseMessage(
                    2,
                    0,
                    null,
                    new Dictionary<byte, object?>
                    {
                        [1] = characterBytes,
                        [2] = "Lucas",
                        [8] = "3005",
                    }),
            ],
            DateTimeOffset.UtcNow);

        var join = Assert.IsType<JoinResponseMessage>(Assert.Single(messages));
        Assert.Equal("12345678-9abc-def0-1234-567890abcdef", join.CharacterId);
        Assert.Equal("Lucas", join.CharacterName);
        Assert.Equal("3005", join.LocationId);
    }

    [Fact]
    public void Decode_GetGameServerByClusterRequest_MapsZone()
    {
        var messages = _decoder.Decode(
            [
                new PhotonRequestMessage(
                    17,
                    new Dictionary<byte, object?> { [0] = "Caerleon-Auction2" }),
            ],
            DateTimeOffset.UtcNow);

        var decoded = Assert.IsType<GetGameServerByClusterMessage>(Assert.Single(messages));
        Assert.Equal("Caerleon-Auction2", decoded.ZoneId);
    }

    [Fact]
    public void Decode_SellOrderResponse_UsesDebugStringArray()
    {
        var messages = _decoder.Decode(
            [
                new PhotonResponseMessage(
                    81,
                    0,
                    null,
                    new Dictionary<byte, object?> { [0] = new[] { "{\"Id\":1}" } }),
            ],
            DateTimeOffset.UtcNow);

        var decoded = Assert.IsType<AuctionGetOffersResponseMessage>(Assert.Single(messages));
        Assert.Single(decoded.MarketOrders);
    }

    [Fact]
    public void Decode_BuyOrderResponse_MapsAuctionBuyOfferAsRequests()
    {
        var messages = _decoder.Decode(
            [
                new PhotonResponseMessage(
                    83,
                    0,
                    null,
                    new Dictionary<byte, object?> { [0] = new[] { "{\"Id\":2}" } }),
            ],
            DateTimeOffset.UtcNow);

        var decoded = Assert.IsType<AuctionGetRequestsResponseMessage>(Assert.Single(messages));
        Assert.Single(decoded.MarketOrders);
    }

    [Fact]
    public void Decode_HistoryRequestAndResponse_PreserveMessageId()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var messages = _decoder.Decode(
            [
                new PhotonRequestMessage(
                    95,
                    new Dictionary<byte, object?>
                    {
                        [1] = -121,
                        [2] = (byte)2,
                        [3] = (byte)1,
                        [4] = 0,
                        [255] = 77UL,
                    }),
                new PhotonResponseMessage(
                    95,
                    0,
                    null,
                    new Dictionary<byte, object?>
                    {
                        [0] = new long[] { -121, 9 },
                        [1] = new ulong[] { 100UL, 200UL },
                        [2] = new ulong[] { 1_700_000_001UL, 1_700_000_002UL },
                        [255] = 77UL,
                    }),
            ],
            observedAtUtc);

        Assert.Collection(
            messages,
            request =>
            {
                var decoded = Assert.IsType<AuctionGetItemAverageStatsRequestMessage>(request);
                Assert.Equal((ulong)77, decoded.MessageId);
            },
            response =>
            {
                var decoded = Assert.IsType<AuctionGetItemAverageStatsResponseMessage>(response);
                Assert.Equal((ulong)77, decoded.MessageId);
                Assert.Equal(2, decoded.ItemAmounts.Count);
            });
    }

    [Fact]
    public void Decode_HistoryRequestAndResponse_AcceptsLiveCompactPayloadShape()
    {
        var observedAtUtc = DateTimeOffset.UtcNow;
        var messages = _decoder.Decode(
            [
                new PhotonRequestMessage(
                    1,
                    new Dictionary<byte, object?>
                    {
                        [1] = -121,
                        [2] = (short)2,
                        [3] = (byte)1,
                        [253] = (short)95,
                        [255] = 77,
                    }),
                new PhotonResponseMessage(
                    1,
                    0,
                    null,
                    new Dictionary<byte, object?>
                    {
                        [0] = new byte[] { 135, 9 },
                        [1] = new long[] { 100, 200 },
                        [2] = new long[] { 1_700_000_001, 1_700_000_002 },
                        [253] = (short)95,
                        [255] = 77,
                    }),
            ],
            observedAtUtc);

        Assert.Collection(
            messages,
            request =>
            {
                var decoded = Assert.IsType<AuctionGetItemAverageStatsRequestMessage>(request);
                Assert.Equal(-121, decoded.ItemId);
                Assert.Equal((byte)2, decoded.QualityLevel);
                Assert.Equal((byte)1, decoded.Timescale);
                Assert.Equal((uint)0, decoded.EnchantmentLevel);
                Assert.Equal((ulong)77, decoded.MessageId);
            },
            response =>
            {
                var decoded = Assert.IsType<AuctionGetItemAverageStatsResponseMessage>(response);
                Assert.Equal((ulong)77, decoded.MessageId);
                Assert.Equal(-121, decoded.ItemAmounts[0]);
                Assert.Equal(9L, decoded.ItemAmounts[1]);
                Assert.Equal((ulong)100, decoded.SilverAmounts[0]);
                Assert.Equal((ulong)1_700_000_001, decoded.Timestamps[0]);
            });
    }
}
