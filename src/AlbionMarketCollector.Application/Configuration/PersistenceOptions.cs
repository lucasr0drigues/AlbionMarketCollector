namespace AlbionMarketCollector.Application.Configuration;

public sealed class PersistenceOptions
{
    public string Provider { get; set; } = "None";

    public string? ConnectionString { get; set; }

    public PostgreSqlOptions PostgreSql { get; set; } = new();
}
