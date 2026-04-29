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

## API and reference data

The API project exposes search and flipping endpoints for the frontend:

- `GET /api/items?search=&limit=25`
- `GET /api/locations?search=&limit=25`
- `GET /api/market-orders?locationId=&itemSearch=&orderType=&maxAgeMinutes=&limit=100`
- `GET /api/flips/black-market?sourceLocationId=&blackMarketLocationId=&maxAgeMinutes=60&minProfitSilver=1`

Reference data imports are CLI-style commands on the API host:

```powershell
dotnet run --project src\AlbionMarketCollector.Api -- import-locations path\to\locations.txt
dotnet run --project src\AlbionMarketCollector.Api -- import-items path\to\items.txt
```

The Angular MVP lives under `web/albion-market-frontend` and expects the API at `http://localhost:5000`.
