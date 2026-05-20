# TravelReview API — .NET 8 + SQL Server

Drop-in replacement for the FastAPI/Mongo backend. Same routes, same JSON shapes — your Expo frontend talks to it unchanged.

## Stack
- **.NET 8** Web API (Controllers)
- **SQL Server** via **Entity Framework Core 8** (`Microsoft.EntityFrameworkCore.SqlServer`)
- **EF Core Migrations** for versioned schema (with `EnsureCreated` fallback for one-command demos)
- BCrypt.Net-Next (password hashing)
- System.IdentityModel.Tokens.Jwt (JWT)
- DotNetEnv (loads `.env` like python-dotenv)

---

## Quick start — Option A: Docker Compose (no .NET SDK required) ⚡

The easiest way. Spins up SQL Server + the API in one command.

```bash
cd backend-dotnet
cp .env.example .env        # edit JWT_SECRET / SA_PASSWORD if you like
docker compose up --build
```

What happens:
1. SQL Server 2022 container boots and waits until healthy.
2. The API container builds from the local source, connects to SQL Server, applies migrations (if scaffolded) or runs `EnsureCreatedAsync()` as a fallback, then seeds 5 countries / 15 cities / 30 places.
3. API is live at **http://localhost:8001**.

Stop and wipe the DB volume:
```bash
docker compose down -v
```

---

## Quick start — Option B: Local .NET SDK + SQL Server

### Prerequisites
- .NET 8 SDK — https://dotnet.microsoft.com/download
- SQL Server (any of):
  - SQL Server Developer / Express (Windows)
  - Docker: `docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest`
  - macOS: same Docker command (use `mcr.microsoft.com/azure-sql-edge` on Apple Silicon if needed)
- EF Core CLI tools (one-time): `dotnet tool install --global dotnet-ef`

### Run
```bash
cd backend-dotnet
cp .env.example .env                          # edit JWT_SECRET / CONNECTION_STRING
dotnet restore
dotnet ef migrations add Initial              # one-time: scaffold Migrations/  (optional — see below)
dotnet run                                    # auto-applies migrations + seeds data
```
Server listens on `http://0.0.0.0:8001`.

Subsequent schema changes:
```bash
dotnet ef migrations add <Name>
dotnet run                                    # Migrations:ApplyOnStartup=true picks it up
```

### Migrations vs. EnsureCreated
- The startup code prefers EF Core Migrations if you've scaffolded them.
- If no `Migrations/` folder exists, it falls back to `EnsureCreatedAsync()` (controlled by `Migrations:UseEnsureCreatedFallback` in `appsettings.json`, default `true`).
- Use the fallback for demos/dev; **for production, always scaffold migrations** so schema changes are versioned and reversible.

---

## Configuration
Everything lives in **`appsettings.json`** — edit defaults there. Real secrets should come from environment variables or User Secrets and override the file.

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
  "Migrations": {
    "ApplyOnStartup": true,
    "SeedOnStartup": true,
    "UseEnsureCreatedFallback": true
  },
  "Kestrel": { "Endpoints": { "Http": { "Url": "http://0.0.0.0:8001" } } }
}
```

Env-var overrides (take priority over `appsettings.json`):
- `CONNECTION_STRING` — full SQL Server connection string
- `JWT_SECRET` — 32+ char random string
- `SA_PASSWORD` — used by docker-compose for the SQL Server container

---

## Endpoints (all under `/api`)
Auth: `POST /auth/register`, `POST /auth/login`, `POST /auth/google/session`,
`GET /auth/me`, `POST /auth/logout`, `POST /auth/kyc`, `PATCH /auth/profile`

Discovery: `GET /countries`, `GET /countries/{id}/cities`,
`GET /cities/{id}/places?category=`, `GET /places/{id}`, `GET /search?q=`

Reviews: `GET /places/{id}/reviews`, `POST /reviews`, `POST /reviews/{id}/helpful`

Users: `GET /users/{id}`, `GET /users/me/reviews`, `POST /users/{id}/follow`

Admin: `GET /admin/hydration-status`, `POST /admin/refresh-data` (Bearer `ADMIN_TOKEN`)

---

## Dynamic data hydration 🌍
On first startup the API auto-populates a rich, global dataset from public REST APIs (mirrors `backend/hydration.py`):

| Layer | Source | Key? |
|---|---|---|
| **Top 120 countries** (by population) | [REST Countries v3.1](https://restcountries.com) | ❌ |
| **Capital city** per country (guaranteed fallback) | REST Countries `capital` field | ❌ |
| **Top 3 cities** per country (when available) | [GeoDB Cities (RapidAPI)](https://rapidapi.com/wirefreethought/api/geodb-cities) | ✅ `RAPIDAPI_KEY` |
| **City thumbnails** | Wikipedia REST API summary | ❌ |

The 5 curated demo countries (Italy/Japan/France/Thailand/Peru) plus 30 hand-picked places stay seeded so reviews and the existing UX still work.

Hydration runs **in the background** after startup — the API serves the curated seed immediately while dynamic data streams in. State is tracked in the `HydrationState` table so restarts skip already-done work.

**Force a re-sync:**
```bash
curl -X POST -H "Authorization: Bearer $ADMIN_TOKEN" http://localhost:8001/api/admin/refresh-data
```

**Check status:**
```bash
curl http://localhost:8001/api/admin/hydration-status
```

**GeoDB requires a one-click subscribe:** if you set `RAPIDAPI_KEY` but get a 403 `"Not subscribed"` error in the logs, visit https://rapidapi.com/wirefreethought/api/geodb-cities/ → click **"Subscribe to Test"** → Basic / free plan. The capital-city fallback always populates regardless.

### Auth header
`Authorization: Bearer <token>` where `<token>` is either:
- a JWT issued by `/auth/register` or `/auth/login`, **or**
- an Emergent `session_token` (Google OAuth path).

Both are accepted transparently — no flag on the frontend.

---

## Schema notes
- String PKs (e.g. `usr_xxx`, `c_italy`, `p_eiffel`) preserve the original API's id convention.
- `email` is uniquely indexed; foreign-key columns (`country_id`, `city_id`, `place_id`, `user_id`) all have non-unique indexes.
- Collection fields (`countries_visited`, `following`, `photos`, `helpful_voters`) are persisted as JSON in `nvarchar(max)` columns via EF value converters — keeps the schema simple and 1:1 with the document model.
- Sessions don't use a TTL index (SQL Server doesn't support it natively). Expired sessions are filtered on read; add a cleanup job for production.

---

## Frontend hookup (local dev)
In `/frontend/.env`:
```
EXPO_PUBLIC_BACKEND_URL=http://<your-LAN-ip>:8001
```
Then `yarn start` in `/frontend`.

---

## Standalone Docker (without compose)
```bash
docker build -t travelreview-api .
docker run --rm -p 8001:8001 \
  -e CONNECTION_STRING='Server=host.docker.internal,1433;Database=TravelReview;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;' \
  -e JWT_SECRET='replace-me-32+chars' \
  travelreview-api
```

---

## Tests (xUnit — 16 cases mirroring the Python pytest suite)
Requires a running SQL Server reachable from the test process. Each run spins up an isolated `TravelReview_Test_<guid>` database and drops it on completion — production data is never touched.

```bash
# Easiest: start SQL Server via docker-compose first, then run tests against it
docker compose up -d db
cd tests
dotnet test
```

Override the connection string for tests:
```bash
TEST_CONNECTION_STRING='Server=localhost,1433;...' dotnet test
```
