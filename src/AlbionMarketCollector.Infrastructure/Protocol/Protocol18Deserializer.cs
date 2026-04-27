using System.Buffers.Binary;
using System.Text;
using AlbionMarketCollector.Infrastructure.Protocol.Models;

namespace AlbionMarketCollector.Infrastructure.Protocol;

internal static class Protocol18Deserializer
{
    private const byte TypeUnknown = 0;
    private const byte TypeBoolean = 2;
    private const byte TypeByte = 3;
    private const byte TypeShort = 4;
    private const byte TypeFloat = 5;
    private const byte TypeDouble = 6;
    private const byte TypeString = 7;
    private const byte TypeNull = 8;
    private const byte TypeCompressedInt = 9;
    private const byte TypeCompressedLong = 10;
    private const byte TypeInt1 = 11;
    private const byte TypeInt1Neg = 12;
    private const byte TypeInt2 = 13;
    private const byte TypeInt2Neg = 14;
    private const byte TypeLong1 = 15;
    private const byte TypeLong1Neg = 16;
    private const byte TypeLong2 = 17;
    private const byte TypeLong2Neg = 18;
    private const byte TypeCustom = 19;
    private const byte TypeDictionary = 20;
    private const byte TypeHashtable = 21;
    private const byte TypeObjectArray = 23;
    private const byte TypeOperationRequest = 24;
    private const byte TypeOperationResponse = 25;
    private const byte TypeEventData = 26;
    private const byte TypeBoolFalse = 27;
    private const byte TypeBoolTrue = 28;
    private const byte TypeShortZero = 29;
    private const byte TypeIntZero = 30;
    private const byte TypeLongZero = 31;
    private const byte TypeFloatZero = 32;
    private const byte TypeDoubleZero = 33;
    private const byte TypeByteZero = 34;
    private const byte TypeArray = 0x40;
    private const byte CustomTypeSlimBase = 0x80;

    public static Dictionary<byte, object?> DeserializeParameterTable(ReadOnlySpan<byte> data)
    {
        var offset = 0;
        return ReadParameterTable(data, ref offset);
    }

    public static Dictionary<byte, object?> ReadParameterTable(ReadOnlySpan<byte> data, ref int offset)
    {
        var count = (int)ReadCount(data, ref offset);
        var result = new Dictionary<byte, object?>(count);
        for (var index = 0; index < count && offset < data.Length; index++)
        {
            if (!TryReadByte(data, ref offset, out var key) ||
                !TryReadByte(data, ref offset, out var typeCode))
            {
                break;
            }

            result[key] = DeserializeValue(data, ref offset, typeCode);
        }

        return result;
    }

    public static object? DeserializeValue(ReadOnlySpan<byte> data, ref int offset, byte typeCode)
    {
        if (typeCode >= CustomTypeSlimBase)
        {
            return DeserializeCustom(data, ref offset, typeCode);
        }

        return typeCode switch
        {
            TypeUnknown or TypeNull => null,
            TypeBoolean => ReadByte(data, ref offset) != 0,
            TypeByte => ReadByte(data, ref offset),
            TypeShort => ReadInt16(data, ref offset),
            TypeFloat => ReadSingle(data, ref offset),
            TypeDouble => ReadDouble(data, ref offset),
            TypeString => ReadString(data, ref offset),
            TypeCompressedInt => ReadCompressedInt32(data, ref offset),
            TypeCompressedLong => ReadCompressedInt64(data, ref offset),
            TypeInt1 => (int)ReadByte(data, ref offset),
            TypeInt1Neg => -(int)ReadByte(data, ref offset),
            TypeInt2 => (int)ReadUInt16(data, ref offset),
            TypeInt2Neg => -(int)ReadUInt16(data, ref offset),
            TypeLong1 => (long)ReadByte(data, ref offset),
            TypeLong1Neg => -(long)ReadByte(data, ref offset),
            TypeLong2 => (long)ReadUInt16(data, ref offset),
            TypeLong2Neg => -(long)ReadUInt16(data, ref offset),
            TypeCustom => DeserializeCustom(data, ref offset, null),
            TypeDictionary => DeserializeDictionary(data, ref offset),
            TypeHashtable => DeserializeDictionary(data, ref offset),
            TypeObjectArray => DeserializeObjectArray(data, ref offset),
            TypeOperationRequest => DeserializeNestedOperationRequest(data, ref offset),
            TypeOperationResponse => DeserializeNestedOperationResponse(data, ref offset),
            TypeEventData => DeserializeNestedEventData(data, ref offset),
            TypeBoolFalse => false,
            TypeBoolTrue => true,
            TypeShortZero => (short)0,
            TypeIntZero => 0,
            TypeLongZero => 0L,
            TypeFloatZero => 0f,
            TypeDoubleZero => 0d,
            TypeByteZero => (byte)0,
            TypeArray => DeserializeUntypedArray(data, ref offset),
            _ when (typeCode & TypeArray) == TypeArray => DeserializeTypedArray(data, ref offset, (byte)(typeCode & ~TypeArray)),
            _ => null,
        };
    }

    private static object? DeserializeTypedArray(ReadOnlySpan<byte> data, ref int offset, byte elementType)
    {
        var count = (int)ReadCount(data, ref offset);
        switch (elementType)
        {
            case TypeBoolean:
                return DeserializePackedBooleanArray(data, ref offset, count);
            case TypeByte:
                return ReadBytes(data, ref offset, count);
            case TypeShort:
                return ReadInt16Array(data, ref offset, count);
            case TypeFloat:
                return ReadSingleArray(data, ref offset, count);
            case TypeDouble:
                return ReadDoubleArray(data, ref offset, count);
            case TypeString:
                return ReadStringArray(data, ref offset, count);
            case TypeCompressedInt:
                return ReadCompressedInt32Array(data, ref offset, count);
            case TypeCompressedLong:
                return ReadCompressedInt64Array(data, ref offset, count);
            case TypeInt1:
                return ReadInt1Array(data, ref offset, count);
            case TypeInt1Neg:
                return ReadInt1NegArray(data, ref offset, count);
            case TypeInt2:
                return ReadInt2Array(data, ref offset, count);
            case TypeInt2Neg:
                return ReadInt2NegArray(data, ref offset, count);
            case TypeLong1:
                return ReadLong1Array(data, ref offset, count);
            case TypeLong1Neg:
                return ReadLong1NegArray(data, ref offset, count);
            case TypeLong2:
                return ReadLong2Array(data, ref offset, count);
            case TypeLong2Neg:
                return ReadLong2NegArray(data, ref offset, count);
            case TypeCustom:
                return DeserializeSharedCustomArray(data, ref offset, count);
            default:
                return ReadTypedObjectArray(data, ref offset, count, elementType);
        }
    }

    private static bool[] DeserializePackedBooleanArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var packedByteCount = (count + 7) / 8;
        var packed = ReadBytes(data, ref offset, packedByteCount);
        var result = new bool[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = (packed[index / 8] & (1 << (index % 8))) != 0;
        }

        return result;
    }

    private static object?[] DeserializeSharedCustomArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var customTypeId = ReadByte(data, ref offset);
        var result = new object?[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = DeserializeCustomPayload(data, ref offset, customTypeId);
        }

        return result;
    }

    private static object?[] DeserializeUntypedArray(ReadOnlySpan<byte> data, ref int offset)
    {
        var count = (int)ReadCount(data, ref offset);
        var elementType = ReadByte(data, ref offset);
        return ReadTypedObjectArray(data, ref offset, count, elementType);
    }

    private static object?[] DeserializeObjectArray(ReadOnlySpan<byte> data, ref int offset)
    {
        var count = (int)ReadCount(data, ref offset);
        var result = new object?[count];
        for (var index = 0; index < count; index++)
        {
            var typeCode = ReadByte(data, ref offset);
            result[index] = DeserializeValue(data, ref offset, typeCode);
        }

        return result;
    }

    private static Dictionary<object, object?> DeserializeDictionary(ReadOnlySpan<byte> data, ref int offset)
    {
        var keyType = ReadByte(data, ref offset);
        var valueType = ReadByte(data, ref offset);
        var count = (int)ReadCount(data, ref offset);
        var result = new Dictionary<object, object?>(count);

        for (var index = 0; index < count && offset < data.Length; index++)
        {
            var effectiveKeyType = keyType == 0 ? ReadByte(data, ref offset) : keyType;
            var effectiveValueType = valueType == 0 ? ReadByte(data, ref offset) : valueType;
            var key = DeserializeValue(data, ref offset, effectiveKeyType) ?? $"null_{index}";
            result[key] = DeserializeValue(data, ref offset, effectiveValueType);
        }

        return result;
    }

    private static Dictionary<string, object?> DeserializeNestedOperationRequest(ReadOnlySpan<byte> data, ref int offset)
    {
        var operationCode = ReadByte(data, ref offset);
        var parameters = ReadParameterTable(data, ref offset);
        return new Dictionary<string, object?>
        {
            ["operationCode"] = operationCode,
            ["parameters"] = parameters,
        };
    }

    private static Dictionary<string, object?> DeserializeNestedOperationResponse(ReadOnlySpan<byte> data, ref int offset)
    {
        var operationCode = ReadByte(data, ref offset);
        var returnCode = ReadInt16(data, ref offset);
        string? debugMessage = null;
        if (offset < data.Length)
        {
            var typeCode = ReadByte(data, ref offset);
            debugMessage = DeserializeValue(data, ref offset, typeCode) as string;
        }

        var parameters = ReadParameterTable(data, ref offset);
        return new Dictionary<string, object?>
        {
            ["operationCode"] = operationCode,
            ["returnCode"] = returnCode,
            ["debugMessage"] = debugMessage,
            ["parameters"] = parameters,
        };
    }

    private static Dictionary<string, object?> DeserializeNestedEventData(ReadOnlySpan<byte> data, ref int offset)
    {
        var eventCode = ReadByte(data, ref offset);
        var parameters = ReadParameterTable(data, ref offset);
        return new Dictionary<string, object?>
        {
            ["eventCode"] = eventCode,
            ["parameters"] = parameters,
        };
    }

    private static Protocol18CustomType? DeserializeCustom(ReadOnlySpan<byte> data, ref int offset, byte? slimTypeCode)
    {
        var customTypeId = slimTypeCode.HasValue
            ? (byte)(slimTypeCode.Value & 0x7F)
            : ReadByte(data, ref offset);

        return DeserializeCustomPayload(data, ref offset, customTypeId);
    }

    private static Protocol18CustomType? DeserializeCustomPayload(ReadOnlySpan<byte> data, ref int offset, byte customTypeId)
    {
        var length = (int)ReadCount(data, ref offset);
        if (length < 0 || offset + length > data.Length)
        {
            return null;
        }

        return new Protocol18CustomType(customTypeId, ReadBytes(data, ref offset, length));
    }

    private static short[] ReadInt16Array(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new short[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = ReadInt16(data, ref offset);
        }

        return result;
    }

    private static float[] ReadSingleArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new float[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = ReadSingle(data, ref offset);
        }

        return result;
    }

    private static double[] ReadDoubleArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new double[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = ReadDouble(data, ref offset);
        }

        return result;
    }

    private static string[] ReadStringArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new string[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = ReadString(data, ref offset);
        }

        return result;
    }

    private static int[] ReadCompressedInt32Array(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new int[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = ReadCompressedInt32(data, ref offset);
        }

        return result;
    }

    private static long[] ReadCompressedInt64Array(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new long[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = ReadCompressedInt64(data, ref offset);
        }

        return result;
    }

    private static int[] ReadInt1Array(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new int[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = ReadByte(data, ref offset);
        }

        return result;
    }

    private static int[] ReadInt1NegArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new int[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = -ReadByte(data, ref offset);
        }

        return result;
    }

    private static int[] ReadInt2Array(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new int[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = ReadUInt16(data, ref offset);
        }

        return result;
    }

    private static int[] ReadInt2NegArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new int[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = -ReadUInt16(data, ref offset);
        }

        return result;
    }

    private static long[] ReadLong1Array(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new long[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = ReadByte(data, ref offset);
        }

        return result;
    }

    private static long[] ReadLong1NegArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new long[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = -ReadByte(data, ref offset);
        }

        return result;
    }

    private static long[] ReadLong2Array(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new long[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = ReadUInt16(data, ref offset);
        }

        return result;
    }

    private static long[] ReadLong2NegArray(ReadOnlySpan<byte> data, ref int offset, int count)
    {
        var result = new long[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = -ReadUInt16(data, ref offset);
        }

        return result;
    }

    private static object?[] ReadTypedObjectArray(ReadOnlySpan<byte> data, ref int offset, int count, byte elementType)
    {
        var result = new object?[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = DeserializeValue(data, ref offset, elementType);
        }

        return result;
    }

    private static uint ReadCount(ReadOnlySpan<byte> data, ref int offset)
    {
        return ReadCompressedUInt32(data, ref offset);
    }

    private static byte ReadByte(ReadOnlySpan<byte> data, ref int offset)
    {
        return TryReadByte(data, ref offset, out var value) ? value : (byte)0;
    }

    private static bool TryReadByte(ReadOnlySpan<byte> data, ref int offset, out byte value)
    {
        if (offset >= data.Length)
        {
            value = 0;
            return false;
        }

        value = data[offset];
        offset++;
        return true;
    }

    private static short ReadInt16(ReadOnlySpan<byte> data, ref int offset)
    {
        if (offset + 2 > data.Length)
        {
            offset = data.Length;
            return 0;
        }

        var value = BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);
        offset += 2;
        return value;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, ref int offset)
    {
        if (offset + 2 > data.Length)
        {
            offset = data.Length;
            return 0;
        }

        var value = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
        offset += 2;
        return value;
    }

    private static float ReadSingle(ReadOnlySpan<byte> data, ref int offset)
    {
        if (offset + 4 > data.Length)
        {
            offset = data.Length;
            return 0f;
        }

        var value = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
        offset += 4;
        return value;
    }

    private static double ReadDouble(ReadOnlySpan<byte> data, ref int offset)
    {
        if (offset + 8 > data.Length)
        {
            offset = data.Length;
            return 0d;
        }

        var value = BinaryPrimitives.ReadDoubleLittleEndian(data[offset..]);
        offset += 8;
        return value;
    }

    private static string ReadString(ReadOnlySpan<byte> data, ref int offset)
    {
        var length = (int)ReadCompressedUInt32(data, ref offset);
        if (length <= 0 || offset + length > data.Length)
        {
            return string.Empty;
        }

        var value = Encoding.UTF8.GetString(data.Slice(offset, length));
        offset += length;
        return value;
    }

    private static byte[] ReadBytes(ReadOnlySpan<byte> data, ref int offset, int length)
    {
        if (length <= 0 || offset + length > data.Length)
        {
            offset = Math.Min(data.Length, offset + Math.Max(length, 0));
            return [];
        }

        var result = data.Slice(offset, length).ToArray();
        offset += length;
        return result;
    }

    private static uint ReadCompressedUInt32(ReadOnlySpan<byte> data, ref int offset)
    {
        uint result = 0;
        var shift = 0;
        while (offset < data.Length)
        {
            var current = data[offset++];
            result |= (uint)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
            if (shift >= 35)
            {
                return 0;
            }
        }

        return 0;
    }

    private static ulong ReadCompressedUInt64(ReadOnlySpan<byte> data, ref int offset)
    {
        ulong result = 0;
        var shift = 0;
        while (offset < data.Length)
        {
            var current = data[offset++];
            result |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
            if (shift >= 70)
            {
                return 0;
            }
        }

        return 0;
    }

    private static int ReadCompressedInt32(ReadOnlySpan<byte> data, ref int offset)
    {
        var value = ReadCompressedUInt32(data, ref offset);
        return (int)((value >> 1) ^ (uint)-(int)(value & 1));
    }

    private static long ReadCompressedInt64(ReadOnlySpan<byte> data, ref int offset)
    {
        var value = ReadCompressedUInt64(data, ref offset);
        return (long)((value >> 1) ^ (ulong)-(long)(value & 1));
    }
}
