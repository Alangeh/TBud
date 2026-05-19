# TravelReview – PRD (MVP)

## What it is
A KYC-verified travel review mobile app. Travelers discover destinations Country → City → Place and write trusted reviews. Built with Expo (React Native) + FastAPI + MongoDB.

## MVP feature set (delivered)
- **Auth**: email/password (JWT, bcrypt) **and** Emergent Google social login, sharing one MongoDB user collection.
- **Mock KYC**: Upload any government-ID photo → unlocks the "Verified Traveler" badge.
- **Discovery flow**: Country list → cities → places (filterable by attraction / restaurant / hotel).
- **Reviews**: 5-star + text + up to 10 photos (base64). Aggregate rating + review count auto-update per place.
- **Helpful votes**: Toggle per-user.
- **Profile**: Avatar, verified badge, review count, countries visited, follower count, full review history.
- **Search**: Live search across countries, cities and places.
- **Seed data**: 5 countries, 15 cities, 30 curated places.

## Tech
- Frontend: Expo SDK 54, Expo Router (file-based), StyleSheet, lucide via @expo/vector-icons (Ionicons), expo-image-picker, expo-web-browser, expo-linking, expo-secure-store via `@/src/utils/storage`.
- Backend: FastAPI, Motor, bcrypt, PyJWT, httpx (Emergent session verify).
- Mongo: indexes on `users.email`, `user_sessions.session_token` (TTL on `expires_at`), per-entity unique ids.

## Smart business enhancement
Per-place aggregate (`rating`, `review_count`) updated atomically on each new review enables future featured/sponsored placements — the foundation for the wiki's "business listings + featured placements" revenue stream.

## Out of scope / next
- Real KYC provider (Onfido/Stripe Identity), business claim flow, follow feed, push notifications, payments/subscriptions.
