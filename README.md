# AlbionMarketCollector

Windows-first .NET 8 worker that captures Albion Online market traffic, decodes Photon payloads, and persists normalized market data into PostgreSQL.

## Requirements

- `.NET 8 SDK`
- `Npcap` installed on Windows for live capture
- PostgreSQL for persisted collector data
- NuGet access or a local package cache for restoring test and capture packages

## Configuration

The worker reads configuration from `AlbionMarketCollector` in `appsettings.json`.

- `Capture.EnableLiveCapture`: enable live device capture
- `Capture.ReplayFixturePath`: optional path to a replay fixture file or directory
- `Capture.ListenDevices`: optional MAC prefixes or device names to capture from
- `Persistence.Provider`: `None` or `PostgreSql`
- `Persistence.PostgreSql.ConnectionString`: PostgreSQL connection string

For live capture on Windows, install Npcap first. For replay-based validation, point `Capture.ReplayFixturePath` at a JSON fixture file or a directory of fixture files.
