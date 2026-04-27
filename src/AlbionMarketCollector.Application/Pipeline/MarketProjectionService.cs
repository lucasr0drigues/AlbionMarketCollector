using System.Globalization;
using System.Text.Json;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Domain.Enums;
using AlbionMarketCollector.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AlbionMarketCollector.Application.Pipeline;

public sealed class MarketProjectionService
{
    private readonly IMarketStateStore _stateStore;
    private readonly IMarketDataWriter _marketDataWriter;
    private readonly ILogger<MarketProjectionService> _logger;

    public MarketProjectionService(
        IMarketStateStore stateStore,
        IMarketDataWriter marketDataWriter,
        ILogger<MarketProjectionService> logger)
    {
        _stateStore = stateStore;
        _marketDataWriter = marketDataWriter;
        _logger = logger;
    }

    public async Task ApplyAsync(
        IReadOnlyList<AlbionMessage> messages,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
        {
            switch (message)
            {
                case EncryptedMarketDataMessage:
                    _stateStore.SetWaitingForMarketData(false);
                    _logger.LogWarning("Encountered encrypted market data while waiting for market payloads.");
                    break;

                case JoinResponseMessage joinResponse:
                    _stateStore.UpdateCharacter(joinResponse.CharacterId, joinResponse.CharacterName);
                    UpdateLocation(joinResponse.LocationId, joinResponse.ObservedAtUtc, "join response");
                    break;

                case GetGameServerByClusterMessage clusterMessage:
                    UpdateLocation(clusterMessage.ZoneId, clusterMessage.ObservedAtUtc, "cluster lookup");
                    break;

                case AuctionGetOffersRequestMessage:
                    _stateStore.SetWaitingForMarketData(true);
                    break;

                case AuctionGetOffersResponseMessage sellOrders:
                    _stateStore.SetWaitingForMarketData(false);
                    await PersistOrdersAsync(sellOrders.MarketOrders, MarketOrderType.Sell, overwriteLocation: false, sellOrders.ObservedAtUtc, cancellationToken).ConfigureAwait(false);
                    break;

                case AuctionGetRequestsResponseMessage buyOrders:
                    await PersistOrdersAsync(buyOrders.MarketOrders, MarketOrderType.Buy, overwriteLocation: true, buyOrders.ObservedAtUtc, cancellationToken).ConfigureAwait(false);
                    break;

                case AuctionGetItemAverageStatsRequestMessage historyRequest:
                    _stateStore.CachePendingHistoryRequest(new PendingMarketHistoryRequest
                    {
                        MessageId = historyRequest.MessageId,
                        ItemTypeId = NormalizeItemId(historyRequest.ItemId).ToString(CultureInfo.InvariantCulture),
                        QualityLevel = historyRequest.QualityLevel,
                        Timescale = historyRequest.Timescale,
                        CapturedAtUtc = historyRequest.ObservedAtUtc,
                    });
                    break;

                case AuctionGetItemAverageStatsResponseMessage historyResponse:
                    await PersistHistoryAsync(historyResponse, cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
    }

    private void UpdateLocation(
        string? locationId,
        DateTimeOffset observedAtUtc,
        string source)
    {
        var normalizedLocationId = _stateStore.NormalizeLocation(locationId);
        if (string.IsNullOrWhiteSpace(normalizedLocationId))
        {
            return;
        }

        _stateStore.UpdateLocation(normalizedLocationId, observedAtUtc);
        _logger.LogInformation(
            "Updated Albion location to {LocationId} from {LocationSource}.",
            normalizedLocationId,
            source);
    }

    private async Task PersistOrdersAsync(
        IReadOnlyList<string> rawOrders,
        MarketOrderType orderType,
        bool overwriteLocation,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var snapshot = _stateStore.GetSnapshot();
        if (string.IsNullOrWhiteSpace(snapshot.CurrentLocationId))
        {
            if (rawOrders.Count > 0)
            {
                _logger.LogError(
                    "Skipping {OrderCount} {OrderType} market orders because the current Albion location is unknown. Transition to a fresh zone before opening the market.",
                    rawOrders.Count,
                    orderType);
            }

            return;
        }

        var orders = new List<MarketOrder>(rawOrders.Count);
        foreach (var rawOrder in rawOrders)
        {
            var parsed = ParseOrder(rawOrder);
            if (parsed is null)
            {
                continue;
            }

            var locationId = overwriteLocation || string.IsNullOrWhiteSpace(parsed.LocationId)
                ? snapshot.CurrentLocationId
                : parsed.LocationId!;

            if (string.IsNullOrWhiteSpace(locationId))
            {
                continue;
            }

            orders.Add(new MarketOrder
            {
                ServerId = snapshot.CurrentServerId,
                LocationId = locationId,
                AlbionOrderId = parsed.Id,
                ItemTypeId = parsed.ItemTypeId ?? string.Empty,
                ItemGroupTypeId = parsed.ItemGroupTypeId ?? string.Empty,
                QualityLevel = parsed.QualityLevel,
                EnchantmentLevel = parsed.EnchantmentLevel,
                UnitPriceSilver = parsed.UnitPriceSilver,
                Amount = parsed.Amount,
                OrderType = orderType,
                ExpiresAtUtc = ParseDateTime(parsed.Expires),
                ObservedAtUtc = observedAtUtc,
            });
        }

        if (orders.Count == 0)
        {
            return;
        }

        await _marketDataWriter
            .UpsertMarketOrdersAsync(orders, cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("Projected {OrderCount} {OrderType} orders.", orders.Count, orderType);
    }

    private async Task PersistHistoryAsync(
        AuctionGetItemAverageStatsResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!_stateStore.TryTakePendingHistoryRequest(response.MessageId, response.ObservedAtUtc, out var pendingRequest) || pendingRequest is null)
        {
            return;
        }

        var snapshot = _stateStore.GetSnapshot();
        if (string.IsNullOrWhiteSpace(snapshot.CurrentLocationId))
        {
            _logger.LogError(
                "Skipping market history for item {ItemTypeId} message {MessageId} because the current Albion location is unknown. Transition to a fresh zone before opening market history.",
                pendingRequest.ItemTypeId,
                response.MessageId);
            return;
        }

        var count = new[]
        {
            response.ItemAmounts.Count,
            response.SilverAmounts.Count,
            response.Timestamps.Count,
        }.Min();

        var points = new List<MarketHistoryPoint>(count);
        for (var index = 0; index < count; index++)
        {
            var itemAmount = response.ItemAmounts[index];
            if (itemAmount < 0)
            {
                if (itemAmount < -124)
                {
                    continue;
                }

                itemAmount += 256;
            }

            points.Add(new MarketHistoryPoint
            {
                ServerId = snapshot.CurrentServerId,
                LocationId = snapshot.CurrentLocationId,
                ItemTypeId = pendingRequest.ItemTypeId,
                QualityLevel = pendingRequest.QualityLevel,
                Timescale = pendingRequest.Timescale,
                DataTimestampUtc = ParseAlbionTimestamp(response.Timestamps[index]),
                ItemAmount = itemAmount,
                SilverAmount = response.SilverAmounts[index],
                ObservedAtUtc = response.ObservedAtUtc,
            });
        }

        if (points.Count == 0)
        {
            return;
        }

        points.Sort((left, right) => right.DataTimestampUtc.CompareTo(left.DataTimestampUtc));

        await _marketDataWriter
            .UpsertMarketHistoryAsync(points, cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("Projected {HistoryCount} market history points for {ItemTypeId}.", points.Count, pendingRequest.ItemTypeId);
    }

    private static long NormalizeItemId(int itemId)
    {
        return itemId < 0 && itemId > -129
            ? itemId + 256L
            : itemId;
    }

    private static DateTimeOffset ParseAlbionTimestamp(ulong value)
    {
        if (value is >= 621_355_968_000_000_000UL and <= 3_155_378_975_999_999_999UL)
        {
            return new DateTimeOffset((long)value, TimeSpan.Zero);
        }

        if (value >= 1_000_000_000_000)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)value);
        }

        return DateTimeOffset.FromUnixTimeSeconds((long)value);
    }

    private static DateTimeOffset? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static MarketOrderPayload? ParseOrder(string rawOrder)
    {
        try
        {
            return JsonSerializer.Deserialize<MarketOrderPayload>(rawOrder);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class MarketOrderPayload
    {
        public long Id { get; set; }

        public string? ItemTypeId { get; set; }

        public string? ItemGroupTypeId { get; set; }

        public string? LocationId { get; set; }

        public int QualityLevel { get; set; }

        public int EnchantmentLevel { get; set; }

        public long UnitPriceSilver { get; set; }

        public long Amount { get; set; }

        public string? AuctionType { get; set; }

        public string? Expires { get; set; }
    }
}
