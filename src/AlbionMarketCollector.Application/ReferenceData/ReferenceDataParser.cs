using System.Globalization;

namespace AlbionMarketCollector.Application.ReferenceData;

public static class ReferenceDataParser
{
    public static ReferenceDataParseResult<LocationReference> ParseLocations(IEnumerable<string> lines)
    {
        var records = new List<LocationReference>();
        var issues = new List<ReferenceDataParseIssue>();
        var lineNumber = 0;

        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                issues.Add(new ReferenceDataParseIssue(lineNumber, "Expected format 'map_id: map_name'.", rawLine));
                continue;
            }

            var id = line[..separatorIndex].Trim();
            var name = line[(separatorIndex + 1)..].Trim();
            if (id.Length == 0 || name.Length == 0)
            {
                issues.Add(new ReferenceDataParseIssue(lineNumber, "Location id and name are required.", rawLine));
                continue;
            }

            records.Add(new LocationReference(id, name));
        }

        return new ReferenceDataParseResult<LocationReference>(records, issues);
    }

    public static ReferenceDataParseResult<ItemReference> ParseItems(IEnumerable<string> lines)
    {
        var records = new List<ItemReference>();
        var issues = new List<ReferenceDataParseIssue>();
        var lineNumber = 0;

        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var firstSeparatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (firstSeparatorIndex <= 0 || firstSeparatorIndex == line.Length - 1)
            {
                issues.Add(new ReferenceDataParseIssue(lineNumber, "Expected format 'item_id: unique_name: localized_name'.", rawLine));
                continue;
            }

            var secondSeparatorIndex = line.IndexOf(':', firstSeparatorIndex + 1);
            if (secondSeparatorIndex <= firstSeparatorIndex + 1 || secondSeparatorIndex == line.Length - 1)
            {
                issues.Add(new ReferenceDataParseIssue(lineNumber, "Expected item id, unique name, and localized name.", rawLine));
                continue;
            }

            var idText = line[..firstSeparatorIndex].Trim();
            var uniqueName = line[(firstSeparatorIndex + 1)..secondSeparatorIndex].Trim();
            var localizedName = line[(secondSeparatorIndex + 1)..].Trim();

            if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                issues.Add(new ReferenceDataParseIssue(lineNumber, "Item id must be an integer.", rawLine));
                continue;
            }

            if (uniqueName.Length == 0 || localizedName.Length == 0)
            {
                issues.Add(new ReferenceDataParseIssue(lineNumber, "Unique name and localized name are required.", rawLine));
                continue;
            }

            records.Add(new ItemReference(id, uniqueName, localizedName));
        }

        return new ReferenceDataParseResult<ItemReference>(records, issues);
    }
}
