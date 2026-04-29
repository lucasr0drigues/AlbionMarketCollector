namespace AlbionMarketCollector.Application.ReferenceData;

public sealed record ReferenceDataParseIssue(
    int LineNumber,
    string Reason,
    string Line);
