using System.Buffers.Binary;
using System.Text;

namespace AlbionMarketCollector.UnitTests.Fixtures;

internal static class PhotonFixtureBuilder
{
    private const byte MessageRequest = 2;
    private const byte MessageResponse = 3;
    private const byte MessageEvent = 4;

    private const byte CommandSendReliable = 6;
    private const byte CommandSendUnreliable = 7;
    private const byte CommandSendFragment = 8;

    private const byte TypeBoolean = 2;
    private const byte TypeByte = 3;
    private const byte TypeString = 7;
    private const byte TypeNull = 8;
    private const byte TypeCompressedInt = 9;
    private const byte TypeCompressedLong = 10;
    private const byte TypeArray = 0x40;

    public static byte[] CreateRequestPacket(
        byte operationCode,
        IReadOnlyDictionary<byte, object?> parameters,
        bool unreliable = false)
    {
        var operationPayload = new List<byte> { operationCode };
        operationPayload.AddRange(EncodeParameterTable(parameters));
        var reliablePayload = CreateReliablePayload(MessageRequest, operationPayload.ToArray());
        return CreatePacket(unreliable ? CommandSendUnreliable : CommandSendReliable, reliablePayload);
    }

    public static byte[] CreateResponsePacket(
        byte operationCode,
        short returnCode,
        object? debugValue,
        IReadOnlyDictionary<byte, object?> parameters)
    {
        var payload = new List<byte> { operationCode };
        payload.AddRange(BitConverter.GetBytes(returnCode));
        payload.AddRange(EncodeValue(debugValue, forceOwnType: true));

        payload.AddRange(EncodeParameterTable(parameters));
        var reliablePayload = CreateReliablePayload(MessageResponse, payload.ToArray());
        return CreatePacket(CommandSendReliable, reliablePayload);
    }

    public static byte[] CreateEncryptedPacket()
    {
        return
        [
            0x00, 0x00,
            0x01,
            0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
    }

    public static byte[] CreateEventPacket(
        byte eventCode,
        IReadOnlyDictionary<byte, object?> parameters)
    {
        var eventPayload = new List<byte> { eventCode };
        eventPayload.AddRange(EncodeParameterTable(parameters));
        var reliablePayload = CreateReliablePayload(MessageEvent, eventPayload.ToArray());
        return CreatePacket(CommandSendReliable, reliablePayload);
    }

    public static byte[][] CreateFragmentedRequestPackets(
        byte operationCode,
        IReadOnlyDictionary<byte, object?> parameters,
        int fragmentSize)
    {
        var operationPayload = new List<byte> { operationCode };
        operationPayload.AddRange(EncodeParameterTable(parameters));
        var reliablePayload = CreateReliablePayload(MessageRequest, operationPayload.ToArray());

        var fragmentCount = (int)Math.Ceiling(reliablePayload.Length / (double)fragmentSize);
        var results = new byte[fragmentCount][];
        const int startSequence = 1000;

        for (var index = 0; index < fragmentCount; index++)
        {
            var offset = index * fragmentSize;
            var sliceLength = Math.Min(fragmentSize, reliablePayload.Length - offset);
            var fragmentPayload = reliablePayload.AsSpan(offset, sliceLength).ToArray();
            results[index] = CreateFragmentPacket(startSequence, fragmentCount, index, reliablePayload.Length, offset, fragmentPayload);
        }

        return results;
    }

    private static byte[] CreatePacket(byte commandType, byte[] commandPayload)
    {
        var commandLength = CommandHeaderLength(commandType) + commandPayload.Length;
        var packet = new byte[12 + commandLength];
        var offset = 0;

        offset += 2;
        packet[offset++] = 0;
        packet[offset++] = 1;
        offset += 8;

        packet[offset++] = commandType;
        offset += 3;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset, 4), (uint)commandLength);
        offset += 8;

        if (commandType == CommandSendUnreliable)
        {
            offset += 4;
        }

        commandPayload.CopyTo(packet.AsSpan(offset));
        return packet;
    }

    private static byte[] CreateFragmentPacket(
        int startSequence,
        int fragmentCount,
        int fragmentNumber,
        int totalLength,
        int fragmentOffset,
        byte[] fragmentPayload)
    {
        var commandLength = 12 + 20 + fragmentPayload.Length;
        var packet = new byte[12 + commandLength];
        var offset = 0;

        offset += 2;
        packet[offset++] = 0;
        packet[offset++] = 1;
        offset += 8;

        packet[offset++] = CommandSendFragment;
        offset += 3;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset, 4), (uint)commandLength);
        offset += 8;

        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset, 4), (uint)startSequence);
        offset += 4;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset, 4), (uint)fragmentCount);
        offset += 4;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset, 4), (uint)fragmentNumber);
        offset += 4;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset, 4), (uint)totalLength);
        offset += 4;
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(offset, 4), (uint)fragmentOffset);
        offset += 4;

        fragmentPayload.CopyTo(packet.AsSpan(offset));
        return packet;
    }

    private static int CommandHeaderLength(byte commandType)
    {
        return commandType == CommandSendUnreliable ? 16 : 12;
    }

    private static byte[] CreateReliablePayload(byte messageType, byte[] payload)
    {
        var result = new byte[payload.Length + 2];
        result[0] = 0;
        result[1] = messageType;
        payload.CopyTo(result.AsSpan(2));
        return result;
    }

    private static byte[] EncodeParameterTable(IReadOnlyDictionary<byte, object?> parameters)
    {
        var bytes = new List<byte>();
        bytes.AddRange(EncodeCompressedUInt((uint)parameters.Count));
        foreach (var pair in parameters)
        {
            bytes.Add(pair.Key);
            bytes.AddRange(EncodeValue(pair.Value, forceOwnType: true));
        }

        return bytes.ToArray();
    }

    private static byte[] EncodeValue(object? value, bool forceOwnType)
    {
        if (value is null)
        {
            return [TypeNull];
        }

        return value switch
        {
            bool booleanValue => forceOwnType ? [TypeBoolean, booleanValue ? (byte)1 : (byte)0] : [booleanValue ? (byte)1 : (byte)0],
            byte byteValue => forceOwnType ? [TypeByte, byteValue] : [byteValue],
            string stringValue => EncodeString(stringValue, forceOwnType),
            short shortValue => EncodeCompressedInt32(shortValue, forceOwnType),
            ushort ushortValue => EncodeCompressedInt32(ushortValue, forceOwnType),
            int intValue => EncodeCompressedInt32(intValue, forceOwnType),
            long longValue => EncodeCompressedInt64(longValue, forceOwnType),
            uint uintValue => EncodeCompressedInt64(uintValue, forceOwnType),
            ulong ulongValue => EncodeCompressedInt64((long)ulongValue, forceOwnType),
            byte[] byteArray => EncodeTypedArray(TypeByte, byteArray.Select(static value => (object?)value).ToArray(), static item => [(byte)item!]),
            string[] stringArray => EncodeTypedArray(TypeString, stringArray.Cast<object?>().ToArray(), item => EncodeString((string)item!, false)),
            int[] intArray => EncodeCompressedIntArray(intArray),
            long[] longArray => EncodeCompressedLongArray(longArray),
            ulong[] ulongArray => EncodeCompressedUnsignedLongArray(ulongArray),
            _ => throw new NotSupportedException($"Unsupported fixture value type {value.GetType().FullName}."),
        };
    }

    private static byte[] EncodeString(string value, bool includeType)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var result = new List<byte>();
        if (includeType)
        {
            result.Add(TypeString);
        }

        result.AddRange(EncodeCompressedUInt((uint)bytes.Length));
        result.AddRange(bytes);
        return result.ToArray();
    }

    private static byte[] EncodeCompressedInt32(int value, bool includeType)
    {
        var result = new List<byte>();
        if (includeType)
        {
            result.Add(TypeCompressedInt);
        }

        result.AddRange(EncodeCompressedUInt((uint)((value << 1) ^ (value >> 31))));
        return result.ToArray();
    }

    private static byte[] EncodeCompressedInt64(long value, bool includeType)
    {
        var result = new List<byte>();
        if (includeType)
        {
            result.Add(TypeCompressedLong);
        }

        result.AddRange(EncodeCompressedULong((ulong)((value << 1) ^ (value >> 63))));
        return result.ToArray();
    }

    private static byte[] EncodeTypedArray(
        byte elementType,
        IReadOnlyList<object?> values,
        Func<object?, byte[]> encoder)
    {
        var result = new List<byte> { (byte)(TypeArray | elementType) };
        result.AddRange(EncodeCompressedUInt((uint)values.Count));
        foreach (var value in values)
        {
            result.AddRange(encoder(value));
        }

        return result.ToArray();
    }

    private static byte[] EncodeCompressedIntArray(IReadOnlyList<int> values)
    {
        var result = new List<byte> { (byte)(TypeArray | TypeCompressedInt) };
        result.AddRange(EncodeCompressedUInt((uint)values.Count));
        foreach (var value in values)
        {
            result.AddRange(EncodeCompressedInt32(value, includeType: false));
        }

        return result.ToArray();
    }

    private static byte[] EncodeCompressedLongArray(IReadOnlyList<long> values)
    {
        var result = new List<byte> { (byte)(TypeArray | TypeCompressedLong) };
        result.AddRange(EncodeCompressedUInt((uint)values.Count));
        foreach (var value in values)
        {
            result.AddRange(EncodeCompressedInt64(value, includeType: false));
        }

        return result.ToArray();
    }

    private static byte[] EncodeCompressedUnsignedLongArray(IReadOnlyList<ulong> values)
    {
        var result = new List<byte> { (byte)(TypeArray | TypeCompressedLong) };
        result.AddRange(EncodeCompressedUInt((uint)values.Count));
        foreach (var value in values)
        {
            result.AddRange(EncodeCompressedInt64(unchecked((long)value), includeType: false));
        }

        return result.ToArray();
    }

    private static IEnumerable<byte> EncodeCompressedUInt(uint value)
    {
        do
        {
            var current = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                current |= 0x80;
            }

            yield return current;
        }
        while (value != 0);
    }

    private static IEnumerable<byte> EncodeCompressedULong(ulong value)
    {
        do
        {
            var current = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                current |= 0x80;
            }

            yield return current;
        }
        while (value != 0);
    }
}
