namespace AlbionMarketCollector.Application.ReferenceData;

public sealed record ReferenceDataParseResult<T>(
    IReadOnlyList<T> Records,
    IReadOnlyList<ReferenceDataParseIssue> Issues);
