using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Infrastructure.Protocol;
using AlbionMarketCollector.UnitTests.Fixtures;

namespace AlbionMarketCollector.UnitTests.Protocol;

public sealed class PhotonParserTests
{
    private readonly PhotonParser _parser = new();

    [Fact]
    public void Parse_RequestPacket_ReturnsPhotonRequest()
    {
        var packet = PhotonFixtureBuilder.CreateRequestPacket(
            17,
            new Dictionary<byte, object?> { [0] = "3005" });

        var messages = _parser.Parse(packet);

        var request = Assert.IsType<PhotonRequestMessage>(Assert.Single(messages));
        Assert.Equal((byte)17, request.OperationCode);
        Assert.Equal("3005", request.Parameters[0]);
    }

    [Fact]
    public void Parse_ResponseWithDebugStringArray_ReturnsResponseWithMarketOrders()
    {
        var packet = PhotonFixtureBuilder.CreateResponsePacket(
            81,
            0,
            new[] { "{\"Id\":1}" },
            new Dictionary<byte, object?>());

        var messages = _parser.Parse(packet);

        var response = Assert.IsType<PhotonResponseMessage>(Assert.Single(messages));
        var orders = Assert.IsType<string[]>(response.Parameters[0]);
        Assert.Single(orders);
    }

    [Fact]
    public void Parse_ResponsePacket_ReadsReturnCodeLittleEndian()
    {
        var packet = PhotonFixtureBuilder.CreateResponsePacket(
            95,
            258,
            string.Empty,
            new Dictionary<byte, object?> { [255] = 77UL });

        var messages = _parser.Parse(packet);

        var response = Assert.IsType<PhotonResponseMessage>(Assert.Single(messages));
        Assert.Equal((short)258, response.ReturnCode);
        Assert.Equal(77L, Assert.IsType<long>(response.Parameters[255]));
    }

    [Fact]
    public void Parse_EventPacket_ReturnsPhotonEvent()
    {
        var packet = PhotonFixtureBuilder.CreateEventPacket(
            1,
            new Dictionary<byte, object?> { [252] = (short)475, [0] = 42 });

        var messages = _parser.Parse(packet);

        var @event = Assert.IsType<PhotonEventMessage>(Assert.Single(messages));
        Assert.Equal((byte)1, @event.EventCode);
        Assert.Equal(475, Convert.ToInt32(@event.Parameters[252]));
        Assert.Equal(42, Convert.ToInt32(@event.Parameters[0]));
    }

    [Fact]
    public void Parse_UnreliablePacket_IsHandledLikeReliable()
    {
        var packet = PhotonFixtureBuilder.CreateRequestPacket(
            81,
            new Dictionary<byte, object?>(),
            unreliable: true);

        var messages = _parser.Parse(packet);

        Assert.IsType<PhotonRequestMessage>(Assert.Single(messages));
    }

    [Fact]
    public void Parse_EncryptedPacket_ReturnsEncryptedMessage()
    {
        var messages = _parser.Parse(PhotonFixtureBuilder.CreateEncryptedPacket());

        Assert.IsType<PhotonEncryptedMessage>(Assert.Single(messages));
    }

    [Fact]
    public void Parse_ShortPacket_ReturnsNoMessages()
    {
        var messages = _parser.Parse([1, 2, 3]);

        Assert.Empty(messages);
    }

    [Fact]
    public void Parse_OutOfOrderFragments_ReassemblesRequest()
    {
        var fragments = PhotonFixtureBuilder.CreateFragmentedRequestPackets(
            95,
            new Dictionary<byte, object?>
            {
                [1] = -121,
                [2] = (byte)2,
                [3] = (byte)1,
                [4] = 0,
                [255] = 77UL,
            },
            fragmentSize: 6);

        for (var index = fragments.Length - 1; index > 0; index--)
        {
            Assert.Empty(_parser.Parse(fragments[index]));
        }

        var request = Assert.IsType<PhotonRequestMessage>(Assert.Single(_parser.Parse(fragments[0])));
        Assert.Equal((byte)95, request.OperationCode);
    }
}
