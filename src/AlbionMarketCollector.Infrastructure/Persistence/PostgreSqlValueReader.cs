namespace AlbionMarketCollector.Infrastructure.Persistence;

internal static class PostgreSqlValueReader
{
    public static DateTimeOffset GetUtcDateTimeOffset(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
