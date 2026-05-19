# TravelReview API — .NET 8 + SQL Server

Drop-in replacement for the FastAPI/Mongo backend. Same routes, same JSON shapes — your Expo frontend talks to it unchanged.

## Stack
- **.NET 8** Web API (Controllers)
- **SQL Server** via **Entity Framework Core 8** (Microsoft.EntityFrameworkCore.SqlServer)
- **EF Core Migrations** for versioned schema
- BCrypt.Net-Next (password hashing)
- System.IdentityModel.Tokens.Jwt (JWT)
- DotNetEnv (loads `.env` like python-dotenv)

## Prerequisites
- .NET 8 SDK — https://dotnet.microsoft.com/download
- SQL Server (any of):
  - SQL Server Developer Edition / Express (Windows)
  - Docker: `docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest`
  - SQL Server on macOS via Docker as above (works on Apple Silicon with `--platform linux/amd64` or the `azure-sql-edge` image)
- EF Core CLI tools (one-time): `dotnet tool install --global dotnet-ef`

## Configuration
Everything is in **`appsettings.json`** — copy/edit the values you need. Real secrets should come from environment variables or User Secrets and override the file.

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost,1433;Database=TravelReview;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true;"
  },
  "Jwt": {
    "Secret": "travelreview-dev-secret-change-me-please-32chars",
    "ExpiresDays": 7,
    "Issuer": "travelreview-api",
    "Audience": "travelreview-app"
  },
  "Emergent": { "SessionDataUrl": "https://demobackend.emergentagent.com/auth/v1/env/oauth/session-data" },
  "Cors": { "AllowedOrigins": [ "*" ] },
  "Migrations": { "ApplyOnStartup": true, "SeedOnStartup": true },
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://0.0.0.0:8001" } } }
}
```

Env-var overrides (take priority over `appsettings.json`):
- `CONNECTION_STRING` — full SQL Server connection string
- `JWT_SECRET` — 32+ char random string

## First-time setup
```bash
cd backend-dotnet
cp .env.example .env                              # edit JWT_SECRET (and CONNECTION_STRING if needed)
dotnet restore
dotnet ef migrations add Initial                  # one-time: scaffold the Migrations/ folder
dotnet run                                        # auto-applies migrations + seeds data on startup
```
Server listens on `http://0.0.0.0:8001`.

Subsequent schema changes:
```bash
dotnet ef migrations add <Name>
dotnet run                                        # Migrations:ApplyOnStartup=true picks it up
```

## Endpoints (all under `/api`)
Auth: `POST /auth/register`, `POST /auth/login`, `POST /auth/google/session`,
`GET /auth/me`, `POST /auth/logout`, `POST /auth/kyc`, `PATCH /auth/profile`

Discovery: `GET /countries`, `GET /countries/{id}/cities`,
`GET /cities/{id}/places?category=`, `GET /places/{id}`, `GET /search?q=`

Reviews: `GET /places/{id}/reviews`, `POST /reviews`, `POST /reviews/{id}/helpful`

Users: `GET /users/{id}`, `GET /users/me/reviews`, `POST /users/{id}/follow`

## Auth header
`Authorization: Bearer <token>` where `<token>` is either:
- a JWT issued by `/auth/register` or `/auth/login`, **or**
- an Emergent `session_token` (Google OAuth path).

Both are accepted transparently — no flag on the frontend.

## Schema notes
- String PKs (e.g. `usr_xxx`, `c_italy`, `p_eiffel`) preserve the original API's id convention.
- `email` is uniquely indexed.
- Collection fields (`countries_visited`, `following`, `photos`, `helpful_voters`) are persisted as JSON in `nvarchar(max)` columns via EF value converters — keeps schema simple and 1:1 with the document model.
- Sessions don't have a TTL index (SQL Server doesn't support it natively). Expired sessions are filtered on read; add a cleanup job if needed.

## Frontend hookup (local dev)
In `/frontend/.env`:
```
EXPO_PUBLIC_BACKEND_URL=http://<your-LAN-ip>:8001
```
Then `yarn start` in `/frontend`.

## Docker
```bash
docker build -t travelreview-api .
docker run --rm -p 8001:8001 \
  -e CONNECTION_STRING='Server=host.docker.internal,1433;Database=TravelReview;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;' \
  -e JWT_SECRET='replace-me-32+chars' \
  travelreview-api
```
Make sure your `Migrations/` folder is checked in **before** building — the running container applies them on startup.

## Tests (xUnit — 16 cases mirroring the Python pytest suite)
Requires a running SQL Server reachable from the test process (defaults to the same connection string as the app, but spins up an isolated `TravelReview_Test_<guid>` database per run).
```bash
cd tests
dotnet test
```
Each run drops its test database on completion — production data is never touched.
