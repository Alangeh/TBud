# TravelReview API — .NET 8

Drop-in replacement for the FastAPI backend. Same routes, same JSON shapes — your Expo frontend talks to it unchanged.

## Stack
- .NET 8 Web API (Controllers)
- MongoDB.Driver 2.x
- BCrypt.Net-Next (password hashing)
- System.IdentityModel.Tokens.Jwt (JWT)
- DotNetEnv (loads `.env` like python-dotenv)

## Prerequisites
- .NET 8 SDK — https://dotnet.microsoft.com/download
- MongoDB 6+ running locally (or a connection string to one)

## Run
```bash
cd backend-dotnet
cp .env.example .env          # then edit JWT_SECRET
dotnet restore
dotnet run
```
Server listens on `http://0.0.0.0:8001` (matches the Python service it replaces).

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

## Notes
- BSON `_id` is stripped from every response (matches the Python `{"_id": 0}` behaviour).
- Session TTL index on `user_sessions.expires_at` purges expired Google sessions automatically.
- Seed data (5 countries / 15 cities / 30 places) loads on first startup when collections are empty.
- KYC is **mocked** — any uploaded image flips `verified=true`. Swap in Onfido / Stripe Identity for production.

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
  -e MONGO_URL='mongodb://host.docker.internal:27017' \
  -e DB_NAME='travelreview' \
  -e JWT_SECRET='replace-me-32+chars' \
  travelreview-api
```

## Tests (xUnit — 16 cases mirroring the Python pytest suite)
Requires a local MongoDB running on the default port (or set `MONGO_URL`).
```bash
cd tests
dotnet test
```
Each run uses an isolated `travelreview_test_<guid>` database that is dropped on completion — production data is never touched.
