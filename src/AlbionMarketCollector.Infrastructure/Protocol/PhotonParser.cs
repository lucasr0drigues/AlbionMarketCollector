using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using System.Buffers.Binary;

namespace AlbionMarketCollector.Infrastructure.Protocol;

public class PhotonParser : IPhotonParser
{
    private const int PhotonHeaderLength = 12;
    private const int CommandHeaderLength = 12;
    private const int FragmentHeaderLength = 20;

    private const byte CommandDisconnect = 4;
    private const byte CommandSendReliable = 6;
    private const byte CommandSendUnreliable = 7;
    private const byte CommandSendFragment = 8;

    private const byte MessageRequest = 2;
    private const byte MessageResponse = 3;
    private const byte MessageEvent = 4;
    private const byte MessageResponseAlt = 7;
    private const byte MessageEncrypted = 131;

    private readonly PhotonFragmentAssembler _fragmentAssembler = new();

    public IReadOnlyList<PhotonMessage> Parse(byte[] payload)
    {
        if (payload.Length < PhotonHeaderLength)
        {
            return Array.Empty<PhotonMessage>();
        }

        var messages = new List<PhotonMessage>();
        var offset = 2;
        var flags = payload[offset++];
        var commandCount = payload[offset++];
        offset += 8;

        if (flags == 1)
        {
            messages.Add(new PhotonEncryptedMessage());
            return messages;
        }

        for (var commandIndex = 0; commandIndex < commandCount; commandIndex++)
        {
            if (!TryHandleCommand(payload, ref offset, messages))
            {
                break;
            }
        }

        return messages;
    }

    private bool TryHandleCommand(
        ReadOnlySpan<byte> source,
        ref int offset,
        ICollection<PhotonMessage> messages)
    {
        if (!HasAvailable(source, offset, CommandHeaderLength))
        {
            return false;
        }

        var commandType = source[offset++];
        offset += 3;
        var commandLength = (int)BinaryPrimitives.ReadUInt32BigEndian(source[offset..]);
        offset += 8;

        var payloadLength = commandLength - CommandHeaderLength;
        if (payloadLength < 0 || !HasAvailable(source, offset, payloadLength))
        {
            return false;
        }

        switch (commandType)
        {
            case CommandDisconnect:
                offset += payloadLength;
                return true;

            case CommandSendUnreliable:
                if (payloadLength < 4)
                {
                    offset += payloadLength;
                    return false;
                }

                offset += 4;
                payloadLength -= 4;
                return TryHandleReliablePayload(source, ref offset, payloadLength, messages);

            case CommandSendReliable:
                return TryHandleReliablePayload(source, ref offset, payloadLength, messages);

            case CommandSendFragment:
                return TryHandleFragment(source, ref offset, payloadLength, messages);

            default:
                offset += payloadLength;
                return true;
        }
    }

    private bool TryHandleReliablePayload(
        ReadOnlySpan<byte> source,
        ref int offset,
        int payloadLength,
        ICollection<PhotonMessage> messages)
    {
        if (payloadLength < 2 || !HasAvailable(source, offset, payloadLength))
        {
            offset += Math.Max(payloadLength, 0);
            return false;
        }

        offset++;
        var messageType = source[offset++];
        payloadLength -= 2;
        if (!HasAvailable(source, offset, payloadLength))
        {
            offset += Math.Max(payloadLength, 0);
            return false;
        }

        if (messageType == MessageEncrypted)
        {
            offset += payloadLength;
            messages.Add(new PhotonEncryptedMessage());
            return true;
        }

        var messagePayload = source.Slice(offset, payloadLength);
        offset += payloadLength;

        switch (messageType)
        {
            case MessageRequest:
                DispatchRequest(messagePayload, messages);
                break;

            case MessageResponse:
            case MessageResponseAlt:
                DispatchResponse(messagePayload, messages);
                break;

            case MessageEvent:
                DispatchEvent(messagePayload, messages);
                break;
        }

        return true;
    }

    private bool TryHandleFragment(
        ReadOnlySpan<byte> source,
        ref int offset,
        int payloadLength,
        ICollection<PhotonMessage> messages)
    {
        if (payloadLength < FragmentHeaderLength || !HasAvailable(source, offset, FragmentHeaderLength))
        {
            offset += Math.Max(payloadLength, 0);
            return false;
        }

        var startSequence = (int)BinaryPrimitives.ReadUInt32BigEndian(source[offset..]);
        offset += 4;
        var fragmentCount = (int)BinaryPrimitives.ReadUInt32BigEndian(source[offset..]);
        offset += 4;
        var fragmentNumber = (int)BinaryPrimitives.ReadUInt32BigEndian(source[offset..]);
        offset += 4;
        var totalLength = (int)BinaryPrimitives.ReadUInt32BigEndian(source[offset..]);
        offset += 4;
        var fragmentOffset = (int)BinaryPrimitives.ReadUInt32BigEndian(source[offset..]);
        offset += 4;
        payloadLength -= FragmentHeaderLength;

        if (payloadLength < 0 || !HasAvailable(source, offset, payloadLength))
        {
            offset += Math.Max(payloadLength, 0);
            return false;
        }

        var fragmentPayload = source.Slice(offset, payloadLength);
        offset += payloadLength;

        var reassembled = _fragmentAssembler.AddFragment(
            startSequence,
            fragmentCount,
            fragmentNumber,
            totalLength,
            fragmentOffset,
            fragmentPayload);

        if (reassembled is not null)
        {
            var reassembledOffset = 0;
            TryHandleReliablePayload(reassembled, ref reassembledOffset, reassembled.Length, messages);
        }

        return true;
    }

    private static void DispatchRequest(
        ReadOnlySpan<byte> data,
        ICollection<PhotonMessage> messages)
    {
        if (data.Length < 1)
        {
            return;
        }

        var operationCode = data[0];
        var parameters = Protocol18Deserializer.DeserializeParameterTable(data[1..]);
        messages.Add(new PhotonRequestMessage(operationCode, parameters));
    }

    private static void DispatchResponse(
        ReadOnlySpan<byte> data,
        ICollection<PhotonMessage> messages)
    {
        if (data.Length < 3)
        {
            return;
        }

        var offset = 0;
        var operationCode = data[offset++];
        var returnCode = BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);
        offset += 2;
        string? debugMessage = null;
        string[]? marketOrders = null;

        if (offset < data.Length)
        {
            var typeCode = data[offset++];
            var debugValue = Protocol18Deserializer.DeserializeValue(data, ref offset, typeCode);
            if (debugValue is string[] marketOrderArray)
            {
                marketOrders = marketOrderArray;
            }
            else if (debugValue is string text)
            {
                debugMessage = text;
            }
        }

        var parameters = Protocol18Deserializer.ReadParameterTable(data, ref offset);
        if (marketOrders is not null)
        {
            parameters[0] = marketOrders;
        }

        messages.Add(new PhotonResponseMessage(operationCode, returnCode, debugMessage, parameters));
    }

    private static void DispatchEvent(
        ReadOnlySpan<byte> data,
        ICollection<PhotonMessage> messages)
    {
        if (data.Length < 1)
        {
            return;
        }

        var eventCode = data[0];
        var parameters = Protocol18Deserializer.DeserializeParameterTable(data[1..]);
        messages.Add(new PhotonEventMessage(eventCode, parameters));
    }

    private static bool HasAvailable(ReadOnlySpan<byte> source, int offset, int count)
    {
        return count >= 0 && offset >= 0 && source.Length - offset >= count;
    }
}
