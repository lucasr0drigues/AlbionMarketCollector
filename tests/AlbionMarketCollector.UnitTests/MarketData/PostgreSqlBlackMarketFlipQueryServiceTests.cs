using AlbionMarketCollector.Application.Models;
using AlbionMarketCollector.Infrastructure.MarketData;
using Npgsql;

namespace AlbionMarketCollector.UnitTests.MarketData;

public sealed class PostgreSqlBlackMarketFlipQueryServiceTests
{
    [Fact]
    public void ConfigureFindOpportunitiesCommand_OrdersByMaxTradableAmountFromOuterQuery()
    {
        using var command = new NpgsqlCommand();

        PostgreSqlBlackMarketFlipQueryService.ConfigureFindOpportunitiesCommand(
            command,
            new BlackMarketFlipQuery
            {
                SourceLocationIds = ["4002", "4301"],
                SellingLocationIds = ["3003"],
                Limit = 100,
            });

        var sql = command.CommandText;
        var tradableCteIndex = sql.IndexOf("tradable_opportunities AS", StringComparison.Ordinal);
        var finalFromIndex = sql.IndexOf("FROM tradable_opportunities", StringComparison.Ordinal);
        var tradableFilterIndex = sql.IndexOf("WHERE max_tradable_amount > 0", StringComparison.Ordinal);
        var orderByIndex = sql.IndexOf("ORDER BY (buy_price_silver - sell_price_silver) * max_tradable_amount", StringComparison.Ordinal);

        Assert.True(tradableCteIndex >= 0, "The query should calculate max_tradable_amount in a CTE.");
        Assert.True(finalFromIndex > tradableCteIndex, "The final projection should read from tradable_opportunities.");
        Assert.True(tradableFilterIndex > finalFromIndex, "The final filter should use the projected max_tradable_amount column.");
        Assert.True(orderByIndex > tradableFilterIndex, "The max_tradable_amount alias must be ordered from the outer query.");
        Assert.Contains(command.Parameters, parameter => parameter.ParameterName == "sourceLocationIds");
        Assert.Contains(command.Parameters, parameter => parameter.ParameterName == "sellingLocationIds");
        Assert.Contains(command.Parameters, parameter => parameter.ParameterName == "limit" && (int)parameter.Value! == 100);
    }
}
