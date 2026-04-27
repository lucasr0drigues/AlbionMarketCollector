using System.Globalization;
using System.Text;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Infrastructure.AlbionProtocol.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlbionMarketCollector.Infrastructure.AlbionProtocol;

public sealed class AlbionProtocolDecoder : IAlbionProtocolDecoder
{
    private readonly ILogger<AlbionProtocolDecoder> _logger;

    public AlbionProtocolDecoder(ILogger<AlbionProtocolDecoder>? logger = null)
    {
        _logger = logger ?? NullLogger<AlbionProtocolDecoder>.Instance;
    }

    public IReadOnlyList<AlbionMessage> Decode(
        IReadOnlyList<PhotonMessage> messages,
        DateTimeOffset observedAtUtc)
    {
        var results = new List<AlbionMessage>(messages.Count);
        foreach (var message in messages)
        {
            var resultCountBeforeMessage = results.Count;
            switch (message)
            {
                case PhotonEncryptedMessage:
                    results.Add(new EncryptedMarketDataMessage(observedAtUtc));
                    break;

                case PhotonRequestMessage request:
                    DecodeRequest(request, observedAtUtc, results);
                    if (results.Count == resultCountBeforeMessage)
                    {
                        LogUnhandledRequest(request);
                    }
                    break;

                case PhotonResponseMessage response:
                    DecodeResponse(response, observedAtUtc, results);
                    if (results.Count == resultCountBeforeMessage)
                    {
                        LogUnhandledResponse(response);
                    }
                    break;

                case PhotonEventMessage @event:
                    LogUnhandledEvent(@event);
                    break;
            }
        }

        return results;
    }

    private static void DecodeRequest(
        PhotonRequestMessage message,
        DateTimeOffset observedAtUtc,
        ICollection<AlbionMessage> results)
    {
        switch (ResolveOperationCode(message.OperationCode, message.Parameters))
        {
            case AlbionOperationCodes.GetGameServerByCluster:
                if (TryGetString(message.Parameters, 0, out var zoneId))
                {
                    results.Add(new GetGameServerByClusterMessage(zoneId, observedAtUtc));
                }
                break;

            case AlbionOperationCodes.AuctionGetOffers:
                results.Add(new AuctionGetOffersRequestMessage(observedAtUtc));
                break;

            case AlbionOperationCodes.AuctionGetItemAverageStats:
                if (TryGetInt32(message.Parameters, 1, out var itemId) &&
                    TryGetByte(message.Parameters, 2, out var qualityLevel) &&
                    TryGetByte(message.Parameters, 3, out var timescale) &&
                    TryGetUInt64(message.Parameters, 255, out var messageId))
                {
                    TryGetUInt32(message.Parameters, 4, out var enchantmentLevel);
                    results.Add(new AuctionGetItemAverageStatsRequestMessage(
                        itemId,
                        qualityLevel,
                        timescale,
                        enchantmentLevel,
                        messageId,
                        observedAtUtc));
                }
                break;
        }
    }

    private static void DecodeResponse(
        PhotonResponseMessage message,
        DateTimeOffset observedAtUtc,
        ICollection<AlbionMessage> results)
    {
        switch (ResolveOperationCode(message.OperationCode, message.Parameters))
        {
            case AlbionOperationCodes.Join:
                results.Add(new JoinResponseMessage(
                    TryReadCharacterId(message.Parameters, 1),
                    GetString(message.Parameters, 2),
                    GetLocationCandidate(message.Parameters),
                    observedAtUtc));
                break;

            case AlbionOperationCodes.AuctionGetOffers:
                if (TryGetStringArray(message.Parameters, 0, out var sellOrders))
                {
                    results.Add(new AuctionGetOffersResponseMessage(sellOrders, observedAtUtc));
                }
                break;

            case AlbionOperationCodes.AuctionGetRequests:
            case AlbionOperationCodes.AuctionBuyOffer:
                if (TryGetStringArray(message.Parameters, 0, out var buyOrders))
                {
                    results.Add(new AuctionGetRequestsResponseMessage(buyOrders, observedAtUtc));
                }
                break;

            case AlbionOperationCodes.AuctionGetItemAverageStats:
                if (TryGetLongArray(message.Parameters, 0, out var itemAmounts) &&
                    TryGetULongArray(message.Parameters, 1, out var silverAmounts) &&
                    TryGetULongArray(message.Parameters, 2, out var timestamps) &&
                    TryGetUInt64(message.Parameters, 255, out var messageId))
                {
                    results.Add(new AuctionGetItemAverageStatsResponseMessage(
                        itemAmounts,
                        silverAmounts,
                        timestamps,
                        messageId,
                        observedAtUtc));
                }
                break;
        }
    }

    private static byte ResolveOperationCode(
        byte envelopeOperationCode,
        IReadOnlyDictionary<byte, object?> parameters)
    {
        if (TryGetUInt16(parameters, 253, out var parameterOperationCode))
        {
            return NormalizeOperationCode((byte)(parameterOperationCode & 0xFF), parameterOperationCode);
        }

        return envelopeOperationCode;
    }

    private static ushort ResolveEventCode(
        byte eventCode,
        IReadOnlyDictionary<byte, object?> parameters)
    {
        if (TryGetUInt16(parameters, 252, out var parameterEventCode))
        {
            return NormalizeEventCode(parameterEventCode);
        }

        return eventCode;
    }

    private static byte NormalizeOperationCode(byte envelopeOperationCode, ushort parameterOperationCode)
    {
        if (parameterOperationCode <= byte.MaxValue)
        {
            return (byte)parameterOperationCode;
        }

        var swapped = (ushort)((parameterOperationCode << 8) | (parameterOperationCode >> 8));
        if (swapped <= byte.MaxValue)
        {
            return (byte)swapped;
        }

        if ((parameterOperationCode & 0x00FF) == 0 && (parameterOperationCode >> 8) <= byte.MaxValue)
        {
            return (byte)(parameterOperationCode >> 8);
        }

        return envelopeOperationCode;
    }

    private static ushort NormalizeEventCode(ushort parameterEventCode)
    {
        if (parameterEventCode <= byte.MaxValue)
        {
            return parameterEventCode;
        }

        var swapped = (ushort)((parameterEventCode << 8) | (parameterEventCode >> 8));
        if (swapped <= byte.MaxValue)
        {
            return swapped;
        }

        if ((parameterEventCode & 0x00FF) == 0 && (parameterEventCode >> 8) <= byte.MaxValue)
        {
            return (ushort)(parameterEventCode >> 8);
        }

        return parameterEventCode;
    }

    private static string? GetLocationCandidate(IReadOnlyDictionary<byte, object?> parameters)
    {
        if (TryGetString(parameters, 8, out var locationId))
        {
            return locationId;
        }

        return ExtractLocationCandidate(parameters.Values);
    }

    private static string? ExtractLocationCandidate(IEnumerable<object?> values)
    {
        foreach (var value in values)
        {
            switch (value)
            {
                case string text when LooksLikeLocation(text):
                    return text;

                case byte[] bytes:
                    {
                        var text = Encoding.UTF8.GetString(bytes);
                        if (LooksLikeLocation(text))
                        {
                            return text;
                        }

                        break;
                    }

                case IReadOnlyDictionary<byte, object?> nestedByByte:
                    {
                        var nested = ExtractLocationCandidate(nestedByByte.Values);
                        if (nested is not null)
                        {
                            return nested;
                        }

                        break;
                    }

                case IReadOnlyDictionary<string, object?> nestedByString:
                    {
                        var nested = ExtractLocationCandidate(nestedByString.Values);
                        if (nested is not null)
                        {
                            return nested;
                        }

                        break;
                    }

                case IEnumerable<object?> list:
                    {
                        var nested = ExtractLocationCandidate(list);
                        if (nested is not null)
                        {
                            return nested;
                        }

                        break;
                    }
            }
        }

        return null;
    }

    private static bool LooksLikeLocation(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var trimmed = candidate.Trim().Trim(',', '.');
        if (trimmed.Length is >= 3 and <= 6 && trimmed.All(char.IsDigit))
        {
            return true;
        }

        return trimmed.Contains("@player-island", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Contains("@island-", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("BLACKBANK-", StringComparison.Ordinal) ||
               trimmed.EndsWith("-HellDen", StringComparison.Ordinal) ||
               trimmed.EndsWith("-Auction2", StringComparison.Ordinal) ||
               trimmed.Contains("@ISLAND@", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadCharacterId(
        IReadOnlyDictionary<byte, object?> parameters,
        byte key)
    {
        if (!parameters.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        if (value is byte[] bytes && bytes.Length >= 16)
        {
            return DecodeMixedEndianUuid(bytes);
        }

        if (value is object?[] objectArray)
        {
            var array = objectArray
                .Select(Convert.ToByte)
                .ToArray();
            if (array.Length >= 16)
            {
                return DecodeMixedEndianUuid(array);
            }
        }

        return null;
    }

    private static string DecodeMixedEndianUuid(byte[] value)
    {
        return new Guid(value.Take(16).ToArray()).ToString();
    }

    private static string? GetString(
        IReadOnlyDictionary<byte, object?> parameters,
        byte key)
    {
        return TryGetString(parameters, key, out var value) ? value : null;
    }

    private static bool TryGetString(
        IReadOnlyDictionary<byte, object?> parameters,
        byte key,
        out string? value)
    {
        if (parameters.TryGetValue(key, out var raw) && raw is string text)
        {
            value = text;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetStringArray(
        IReadOnlyDictionary<byte, object?> parameters,
        byte key,
        out IReadOnlyList<string> values)
    {
        if (parameters.TryGetValue(key, out var raw))
        {
            switch (raw)
            {
                case string[] array:
                    values = array;
                    return true;

                case object?[] objectArray:
                    values = objectArray
                        .OfType<string>()
                        .ToArray();
                    return true;
            }
        }

        values = Array.Empty<string>();
        return false;
    }

    private static bool TryGetLongArray(
        IReadOnlyDictionary<byte, object?> parameters,
        byte key,
        out IReadOnlyList<long> values)
    {
        if (parameters.TryGetValue(key, out var raw))
        {
            switch (raw)
            {
                case long[] longs:
                    values = longs;
                    return true;

                case int[] ints:
                    values = ints.Select(static value => (long)value).ToArray();
                    return true;

                case short[] shorts:
                    values = shorts.Select(static value => (long)value).ToArray();
                    return true;

                case byte[] bytes:
                    values = bytes.Select(static value => (long)unchecked((sbyte)value)).ToArray();
                    return true;

                case object?[] objectArray:
                    values = objectArray.Select(Convert.ToInt64).ToArray();
                    return true;
            }
        }

        values = Array.Empty<long>();
        return false;
    }

    private static bool TryGetULongArray(
        IReadOnlyDictionary<byte, object?> parameters,
        byte key,
        out IReadOnlyList<ulong> values)
    {
        if (parameters.TryGetValue(key, out var raw))
        {
            switch (raw)
            {
                case ulong[] ulongs:
                    values = ulongs;
                    return true;

                case long[] longs:
                    values = longs.Select(Convert.ToUInt64).ToArray();
                    return true;

                case int[] ints:
                    values = ints.Select(Convert.ToUInt64).ToArray();
                    return true;

                case object?[] objectArray:
                    values = objectArray.Select(Convert.ToUInt64).ToArray();
                    return true;
            }
        }

        values = Array.Empty<ulong>();
        return false;
    }

    private static bool TryGetInt32(
        IReadOnlyDictionary<byte, object?> parameters,
        byte key,
        out int value)
    {
        if (parameters.TryGetValue(key, out var raw))
        {
            switch (raw)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                    value = (int)longValue;
                    return true;
                case short shortValue:
                    value = shortValue;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryGetByte(
        IReadOnlyDictionary<byte, object?> parameters,
        byte key,
        out byte value)
    {
        if (parameters.TryGetValue(key, out var raw))
        {
            switch (raw)
            {
                case byte byteValue:
                    value = byteValue;
                    return true;
                case int intValue when intValue is >= byte.MinValue and <= byte.MaxValue:
                    value = (byte)intValue;
                    return true;
                case short shortValue when shortValue is >= byte.MinValue and <= byte.MaxValue:
                    value = (byte)shortValue;
                    return true;
                case ushort ushortValue when ushortValue <= byte.MaxValue:
                    value = (byte)ushortValue;
                    return true;
                case long longValue when longValue is >= byte.MinValue and <= byte.MaxValue:
                    value = (byte)longValue;
                    return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryGetUInt32(
        IReadOnlyDictionary<byte, object?> parameters,
        byte key,
        out uint value)
    {
        if (parameters.TryGetValue(key, out var raw))
        {
            switch (raw)
            {
                case uint uintValue:
                    value = uintValue;
                    return true;
                case int intValue when intValue >= 0:
                    value = (uint)intValue;
                    return true;
                case short shortValue when shortValue >= 0:
                    value = (uint)shortValue;
                    return true;
                case ushort ushortValue:
                    value = ushortValue;
                    return true;
                case long longValue when longValue >= 0:
                    value = (uint)longValue;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryGetUInt64(
        IReadOnlyDictionary<byte, object?> parameters,
        byte key,
        out ulong value)
    {
        if (parameters.TryGetValue(key, out var raw))
        {
            switch (raw)
            {
                case ulong ulongValue:
                    value = ulongValue;
                    return true;
                case long longValue when longValue >= 0:
                    value = (ulong)longValue;
                    return true;
                case int intValue when intValue >= 0:
                    value = (ulong)intValue;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
                case string text when ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    value = parsed;
                    return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryGetUInt16(
        IReadOnlyDictionary<byte, object?> parameters,
        byte key,
        out ushort value)
    {
        if (parameters.TryGetValue(key, out var raw))
        {
            switch (raw)
            {
                case ushort ushortValue:
                    value = ushortValue;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
                case short shortValue:
                    value = unchecked((ushort)shortValue);
                    return true;
                case int intValue when intValue is >= ushort.MinValue and <= ushort.MaxValue:
                    value = (ushort)intValue;
                    return true;
                case uint uintValue when uintValue <= ushort.MaxValue:
                    value = (ushort)uintValue;
                    return true;
                case long longValue when longValue is >= ushort.MinValue and <= ushort.MaxValue:
                    value = (ushort)longValue;
                    return true;
                case ulong ulongValue when ulongValue <= ushort.MaxValue:
                    value = (ushort)ulongValue;
                    return true;
                case string text when ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    value = parsed;
                    return true;
            }
        }

        value = 0;
        return false;
    }

    private void LogUnhandledRequest(PhotonRequestMessage request)
    {
        var resolvedOperationCode = ResolveOperationCode(request.OperationCode, request.Parameters);
        _logger.LogDebug(
            "Unhandled Albion request. EnvelopeOp={EnvelopeOp}, ResolvedOp={ResolvedOp}, ParameterKeys=[{ParameterKeys}], ParameterSummary={ParameterSummary}",
            request.OperationCode,
            resolvedOperationCode,
            string.Join(", ", request.Parameters.Keys.OrderBy(static key => key)),
            SummarizeParameters(request.Parameters));
    }

    private void LogUnhandledResponse(PhotonResponseMessage response)
    {
        var resolvedOperationCode = ResolveOperationCode(response.OperationCode, response.Parameters);
        _logger.LogDebug(
            "Unhandled Albion response. EnvelopeOp={EnvelopeOp}, ResolvedOp={ResolvedOp}, ReturnCode={ReturnCode}, ParameterKeys=[{ParameterKeys}], DebugMessage={DebugMessage}, ParameterSummary={ParameterSummary}",
            response.OperationCode,
            resolvedOperationCode,
            response.ReturnCode,
            string.Join(", ", response.Parameters.Keys.OrderBy(static key => key)),
            response.DebugMessage,
            SummarizeParameters(response.Parameters));
    }

    private void LogUnhandledEvent(PhotonEventMessage @event)
    {
        var resolvedEventCode = ResolveEventCode(@event.EventCode, @event.Parameters);
        _logger.LogDebug(
            "Unhandled Albion event. EventCode={EventCode}, ResolvedEvent={ResolvedEvent}, ParameterKeys=[{ParameterKeys}], ParameterSummary={ParameterSummary}",
            @event.EventCode,
            resolvedEventCode,
            string.Join(", ", @event.Parameters.Keys.OrderBy(static key => key)),
            SummarizeParameters(@event.Parameters));
    }

    private static string SummarizeParameters(IReadOnlyDictionary<byte, object?> parameters)
    {
        if (parameters.Count == 0)
        {
            return "<empty>";
        }

        return string.Join(
            "; ",
            parameters
                .OrderBy(static pair => pair.Key)
                .Select(static pair => $"{pair.Key}:{DescribeValue(pair.Value)}"));
    }

    private static string DescribeValue(object? value)
    {
        return value switch
        {
            null => "null",
            string text => $"string(len={text.Length})",
            string[] array => $"string[{array.Length}]",
            byte[] bytes => $"byte[{bytes.Length}]",
            short[] array => $"short[{array.Length}]",
            int[] array => $"int[{array.Length}]",
            long[] array => $"long[{array.Length}]",
            ulong[] array => $"ulong[{array.Length}]",
            float[] array => $"float[{array.Length}]",
            double[] array => $"double[{array.Length}]",
            bool[] array => $"bool[{array.Length}]",
            object?[] array => $"object[{array.Length}]",
            IReadOnlyDictionary<byte, object?> dictionary => $"dict<byte,object>({dictionary.Count})",
            IReadOnlyDictionary<string, object?> dictionary => $"dict<string,object>({dictionary.Count})",
            IEnumerable<object?> sequence => $"sequence({sequence.Count()})",
            _ => value.GetType().Name,
        };
    }
}
