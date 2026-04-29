using AlbionMarketCollector.Application.Configuration;
using AlbionMarketCollector.Application.Contracts;
using AlbionMarketCollector.Application.ReferenceData;
using AlbionMarketCollector.Infrastructure.MarketData;
using AlbionMarketCollector.Infrastructure.ReferenceData;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var collectorOptions = builder.Configuration
    .GetSection("AlbionMarketCollector")
    .Get<CollectorOptions>() ?? new CollectorOptions();
var connectionString = collectorOptions.Persistence.PostgreSql.ConnectionString
    ?? collectorOptions.Persistence.ConnectionString;

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("PostgreSQL persistence requires AlbionMarketCollector:Persistence:PostgreSql:ConnectionString.");
}

builder.Services.AddSingleton(collectorOptions);
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<IReferenceSchemaInitializer, PostgreSqlReferenceSchemaInitializer>();
builder.Services.AddSingleton<IReferenceDataImporter, PostgreSqlReferenceDataImporter>();
builder.Services.AddSingleton<IReferenceDataQueryService, PostgreSqlReferenceDataQueryService>();
builder.Services.AddSingleton<IMarketDataQueryService, PostgreSqlMarketDataQueryService>();
builder.Services.AddSingleton<IBlackMarketFlipQueryService, PostgreSqlBlackMarketFlipQueryService>();
builder.Services.AddSingleton<IMarketOrderMaintenanceService, PostgreSqlMarketOrderMaintenanceService>();
builder.Services.AddControllers();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (args.Length > 0 && IsImportCommand(args[0]))
{
    Environment.ExitCode = await RunImportCommandAsync(app.Services, args, app.Logger, app.Lifetime.ApplicationStopping).ConfigureAwait(false);
    return;
}

app.UseCors();
app.MapControllers();
await app.RunAsync().ConfigureAwait(false);

static bool IsImportCommand(string value)
{
    return value.Equals("import-locations", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("import-items", StringComparison.OrdinalIgnoreCase);
}

static async Task<int> RunImportCommandAsync(
    IServiceProvider services,
    string[] args,
    ILogger logger,
    CancellationToken cancellationToken)
{
    if (args.Length < 2)
    {
        logger.LogError("Usage: import-locations <path> or import-items <path>");
        return 2;
    }

    var command = args[0];
    var path = args[1];
    if (!File.Exists(path))
    {
        logger.LogError("Reference data file {Path} was not found.", path);
        return 2;
    }

    await using var scope = services.CreateAsyncScope();
    var importer = scope.ServiceProvider.GetRequiredService<IReferenceDataImporter>();
    var lines = File.ReadLines(path);

    if (command.Equals("import-locations", StringComparison.OrdinalIgnoreCase))
    {
        var parsed = ReferenceDataParser.ParseLocations(lines);
        LogIssues(logger, parsed.Issues);
        var result = await importer.ImportLocationsAsync(parsed.Records, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Imported {ImportedCount} locations with {SkippedCount} skipped lines.",
            result.UpsertedCount,
            parsed.Issues.Count);
        return 0;
    }

    if (command.Equals("import-items", StringComparison.OrdinalIgnoreCase))
    {
        var parsed = ReferenceDataParser.ParseItems(lines);
        LogIssues(logger, parsed.Issues);
        var result = await importer.ImportItemsAsync(parsed.Records, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Imported {ImportedCount} items with {SkippedCount} skipped lines.",
            result.UpsertedCount,
            parsed.Issues.Count);
        return 0;
    }

    logger.LogError("Unknown import command {Command}.", command);
    return 2;
}

static void LogIssues(
    ILogger logger,
    IReadOnlyList<ReferenceDataParseIssue> issues)
{
    foreach (var issue in issues)
    {
        logger.LogWarning(
            "Skipping line {LineNumber}: {Reason} Line={Line}",
            issue.LineNumber,
            issue.Reason,
            issue.Line);
    }
}
