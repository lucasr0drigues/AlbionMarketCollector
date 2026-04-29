using AlbionMarketCollector.Application.ReferenceData;
using Xunit;

namespace AlbionMarketCollector.UnitTests.ReferenceData;

public sealed class ReferenceDataParserTests
{
    [Fact]
    public void ParseLocations_AcceptsValidLinesAndSkipsBlanks()
    {
        var result = ReferenceDataParser.ParseLocations(
            [
                "0004: Swamp Cross",
                "",
                "ISLAND-PLAYER-0001a: ISLAND-PLAYER-0001a_ISL_DL_T1_NON",
            ]);

        Assert.Empty(result.Issues);
        Assert.Collection(
            result.Records,
            location =>
            {
                Assert.Equal("0004", location.Id);
                Assert.Equal("Swamp Cross", location.Name);
            },
            location =>
            {
                Assert.Equal("ISLAND-PLAYER-0001a", location.Id);
                Assert.Equal("ISLAND-PLAYER-0001a_ISL_DL_T1_NON", location.Name);
            });
    }

    [Fact]
    public void ParseLocations_ReportsInvalidLines()
    {
        var result = ReferenceDataParser.ParseLocations(["missing-separator"]);

        Assert.Empty(result.Records);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(1, issue.LineNumber);
    }

    [Fact]
    public void ParseItems_AcceptsValidLinesWithLocalizedNames()
    {
        var result = ReferenceDataParser.ParseItems(
            [
                "8924: T7_2H_POLEHAMMER@3 : Grandmaster's Polehammer",
                "8931: T4_2H_HAMMER : Adept's Great Hammer",
            ]);

        Assert.Empty(result.Issues);
        Assert.Collection(
            result.Records,
            item =>
            {
                Assert.Equal(8924, item.Id);
                Assert.Equal("T7_2H_POLEHAMMER@3", item.UniqueName);
                Assert.Equal("Grandmaster's Polehammer", item.LocalizedName);
            },
            item =>
            {
                Assert.Equal(8931, item.Id);
                Assert.Equal("T4_2H_HAMMER", item.UniqueName);
                Assert.Equal("Adept's Great Hammer", item.LocalizedName);
            });
    }

    [Fact]
    public void ParseItems_ReportsInvalidItemIdsAndMissingFields()
    {
        var result = ReferenceDataParser.ParseItems(
            [
                "abc: T4_BAG : Adept's Bag",
                "8931: : Adept's Great Hammer",
                "8932: T4_2H_HAMMER",
            ]);

        Assert.Empty(result.Records);
        Assert.Equal(3, result.Issues.Count);
    }
}
