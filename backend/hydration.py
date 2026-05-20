"""Dynamic data hydration from public REST APIs.

Strategy:
  1. Pull top-120 countries by population from REST Countries v3.1 (keyless).
  2. For each country, pull top-3 cities by population from GeoDB Cities
     (RapidAPI key required — set RAPIDAPI_KEY env var).
  3. (Optional, keyless) Fetch a thumbnail per city from Wikipedia REST API.

Curated countries (Italy/Japan/France/Thailand/Peru) and their seeded
cities/places are preserved as-is — dynamic hydration only ADDS missing
data, never deletes, so existing place_ids / reviews stay valid.

Runs as a background task after FastAPI startup. State tracked in
`hydration_state` collection so re-runs on restart are no-ops.
Admin can force a re-sync via POST /api/admin/refresh-data.
"""
from __future__ import annotations

import asyncio
import logging
import os
from datetime import datetime, timezone
from typing import Optional

import httpx

log = logging.getLogger("travelreview.hydration")

# --- Config ---------------------------------------------------------------
REST_COUNTRIES_URL = (
    "https://restcountries.com/v3.1/all"
    "?fields=name,cca2,cca3,capital,region,subregion,population,flags"
)
GEODB_BASE = "https://wft-geo-db.p.rapidapi.com/v1/geo"
GEODB_HOST = "wft-geo-db.p.rapidapi.com"
WIKI_SUMMARY_URL = "https://en.wikipedia.org/api/rest_v1/page/summary/{title}"

TOP_N_COUNTRIES = 120
CITIES_PER_COUNTRY = 3
GEODB_REQ_SPACING_SEC = 1.2  # Free tier: 1 req/sec, leave headroom
WIKI_CONCURRENCY = 6

# Curated codes (already seeded — must NOT be overwritten so place_ids stay valid)
CURATED_CODES = {"IT", "JP", "FR", "TH", "PE"}


# --- HTTP helpers ---------------------------------------------------------
async def _fetch_rest_countries(client: httpx.AsyncClient) -> list[dict]:
    r = await client.get(REST_COUNTRIES_URL)
    r.raise_for_status()
    return r.json()


async def _fetch_geodb_cities(
    client: httpx.AsyncClient, country_code: str, api_key: str, limit: int
) -> tuple[list[dict], Optional[str]]:
    """Returns (cities, error_kind). error_kind in {None, 'subscription', 'rate_limit', 'other'}."""
    url = f"{GEODB_BASE}/countries/{country_code}/places"
    params = {
        "types": "CITY",
        "limit": limit,
        "sort": "-population",
        "minPopulation": 10000,
    }
    headers = {"X-RapidAPI-Key": api_key, "X-RapidAPI-Host": GEODB_HOST}
    try:
        r = await client.get(url, params=params, headers=headers)
    except Exception as e:
        log.warning("GeoDB network error for %s: %s", country_code, e)
        return [], "other"
    if r.status_code == 200:
        return r.json().get("data", []) or [], None
    text = (r.text or "")[:200]
    if r.status_code == 403 or "not subscribed" in text.lower():
        return [], "subscription"
    if r.status_code == 429:
        return [], "rate_limit"
    log.warning("GeoDB %s -> %d: %s", country_code, r.status_code, text)
    return [], "other"


async def _wiki_thumbnail(client: httpx.AsyncClient, title: str) -> Optional[str]:
    """Best-effort Wikipedia REST API summary lookup for a thumbnail URL.
    Wikipedia requires a descriptive User-Agent per their API policy."""
    try:
        url = WIKI_SUMMARY_URL.format(title=title.replace(" ", "_"))
        headers = {
            "User-Agent": "TravelReviewApp/1.0 (https://github.com/example/travelreview; contact@example.com)",
            "Accept": "application/json",
        }
        r = await client.get(url, timeout=8.0, headers=headers)
        if r.status_code != 200:
            return None
        data = r.json()
        thumb = (data.get("thumbnail") or {}).get("source")
        return thumb
    except Exception:
        return None


# --- Field mapping --------------------------------------------------------
def _country_description(c: dict) -> str:
    region = c.get("region") or ""
    subregion = c.get("subregion") or region
    capital_list = c.get("capital") or []
    capital = capital_list[0] if capital_list else None
    pop = c.get("population") or 0
    parts: list[str] = []
    if subregion:
        parts.append(subregion)
    if capital:
        parts.append(f"capital · {capital}")
    if pop:
        parts.append(f"pop. {pop:,}")
    return " · ".join(parts) or "Discover this country."


def _country_image(c: dict) -> str:
    flags = c.get("flags") or {}
    return flags.get("png") or flags.get("svg") or ""


# --- Main hydration -------------------------------------------------------
async def hydrate(db, *, force: bool = False) -> dict:
    """Idempotent hydration. Returns a summary dict."""
    state = await db.hydration_state.find_one({"key": "v1"})
    if state and state.get("status") == "completed" and not force:
        return {"skipped": True, "reason": "already_completed", "state": _strip_id(state)}
    if state and state.get("status") == "running" and not force:
        return {"skipped": True, "reason": "already_running", "state": _strip_id(state)}

    api_key = (os.environ.get("RAPIDAPI_KEY") or "").strip()
    started_at = datetime.now(timezone.utc)
    await db.hydration_state.update_one(
        {"key": "v1"},
        {"$set": {"key": "v1", "status": "running", "started_at": started_at, "force": force}},
        upsert=True,
    )

    countries_inserted = 0
    cities_inserted = 0
    errors: list[str] = []

    try:
        async with httpx.AsyncClient(timeout=30.0) as cli:
            # 1) Countries -------------------------------------------------
            try:
                rest = await _fetch_rest_countries(cli)
            except Exception as e:
                log.exception("REST Countries fetch failed")
                errors.append(f"rest_countries: {e}")
                rest = []

            rest.sort(key=lambda c: (c.get("population") or 0), reverse=True)
            top = rest[:TOP_N_COUNTRIES]
            log.info("REST Countries: got %d, taking top %d", len(rest), len(top))

            existing_codes = {
                c.get("code")
                for c in await db.countries.find({}, {"code": 1, "_id": 0}).to_list(500)
                if c.get("code")
            }

            new_countries: list[dict] = []
            country_meta: dict[str, dict] = {}  # code -> full rest doc for later use
            for c in top:
                code = (c.get("cca2") or "").upper()
                name = (c.get("name") or {}).get("common")
                if not code or not name:
                    continue
                country_meta[code] = c
                if code in existing_codes:
                    continue
                new_countries.append({
                    "country_id": f"c_{code.lower()}",
                    "name": name,
                    "code": code,
                    "description": _country_description(c),
                    "image": _country_image(c),
                    "population": c.get("population") or 0,
                    "region": c.get("region") or "",
                    "capital": (c.get("capital") or [None])[0],
                    "source": "rest_countries",
                })

            if new_countries:
                # Insert in batches with ordered=False so duplicates don't abort
                try:
                    await db.countries.insert_many(new_countries, ordered=False)
                    countries_inserted = len(new_countries)
                except Exception as e:
                    log.warning("Country bulk insert partial: %s", e)
                    # Fallback: per-doc upsert
                    for doc in new_countries:
                        try:
                            await db.countries.update_one(
                                {"code": doc["code"]}, {"$setOnInsert": doc}, upsert=True
                            )
                            countries_inserted += 1
                        except Exception as e2:
                            errors.append(f"country {doc['code']}: {e2}")
                log.info("Inserted %d new countries", countries_inserted)

            # 2) Cities ----------------------------------------------------
            # Strategy: ALWAYS pre-populate the country's capital from REST
            # Countries data (keyless, guaranteed). Then, if a GeoDB API key
            # is configured and the subscription is active, augment with up
            # to N more major cities. Bails out early on auth/subscription
            # errors to avoid hammering a failing endpoint.
            all_countries = await db.countries.find({}, {"_id": 0}).to_list(500)
            all_countries.sort(key=lambda c: c.get("population", 0) or 0, reverse=True)

            geodb_disabled_reason: Optional[str] = None
            if not api_key:
                geodb_disabled_reason = "RAPIDAPI_KEY not set"
            consecutive_failures = 0

            for country in all_countries:
                code = country.get("code")
                if not code:
                    continue
                existing = await db.cities.count_documents({"country_id": country["country_id"]})
                if existing > 0:
                    continue

                docs: list[dict] = []

                # 2a) Capital city fallback from REST Countries (always works)
                rest_doc = country_meta.get(code)
                capital_name = country.get("capital") or (
                    (rest_doc.get("capital") or [None])[0] if rest_doc else None
                )
                if capital_name:
                    docs.append({
                        "city_id": f"ct_cap_{code.lower()}",
                        "country_id": country["country_id"],
                        "name": capital_name,
                        "description": f"Capital of {country['name']}",
                        "image": "",
                        "latitude": None,
                        "longitude": None,
                        "population": 0,
                        "source": "rest_countries_capital",
                    })

                # 2b) GeoDB augmentation
                if api_key and geodb_disabled_reason is None:
                    cities, err = await _fetch_geodb_cities(
                        cli, code, api_key, CITIES_PER_COUNTRY
                    )
                    if err == "subscription":
                        geodb_disabled_reason = (
                            "GeoDB returned 403 'Not subscribed' — go to "
                            "https://rapidapi.com/wirefreethought/api/geodb-cities/ "
                            "and click 'Subscribe to Test' (Basic / free plan)."
                        )
                        errors.append(geodb_disabled_reason)
                        log.warning(geodb_disabled_reason)
                    elif err == "rate_limit":
                        consecutive_failures += 1
                        if consecutive_failures >= 5:
                            geodb_disabled_reason = (
                                "GeoDB rate limit hit 5x in a row — stopping. "
                                "Free tier is 1 req/sec, 1000 req/month."
                            )
                            errors.append(geodb_disabled_reason)
                            log.warning(geodb_disabled_reason)
                    elif err is None:
                        consecutive_failures = 0
                        # Map GeoDB cities, skipping capital duplicates
                        existing_names = {d["name"].lower() for d in docs}
                        for city in cities:
                            city_id_raw = city.get("id") or city.get("wikiDataId") or ""
                            cname = city.get("name") or city.get("city") or ""
                            if not city_id_raw or not cname:
                                continue
                            if cname.lower() in existing_names:
                                continue
                            existing_names.add(cname.lower())
                            docs.append({
                                "city_id": f"ct_geo_{city_id_raw}",
                                "country_id": country["country_id"],
                                "name": cname,
                                "description": (
                                    (city.get("region") or "")
                                    + (
                                        f" · pop. {city['population']:,}"
                                        if city.get("population")
                                        else ""
                                    )
                                ).strip(" ·"),
                                "image": "",
                                "latitude": city.get("latitude"),
                                "longitude": city.get("longitude"),
                                "population": city.get("population") or 0,
                                "source": "geodb_cities",
                            })

                if docs:
                    # Best-effort Wikipedia thumbnail enrichment
                    sem = asyncio.Semaphore(WIKI_CONCURRENCY)

                    async def _enrich(d: dict, cname: str):
                        async with sem:
                            title = f"{cname}, {country['name']}"
                            thumb = await _wiki_thumbnail(cli, title)
                            if not thumb:
                                thumb = await _wiki_thumbnail(cli, cname)
                            if thumb:
                                d["image"] = thumb

                    await asyncio.gather(*(_enrich(d, d["name"]) for d in docs))

                    try:
                        await db.cities.insert_many(docs, ordered=False)
                        cities_inserted += len(docs)
                    except Exception as e:
                        errors.append(f"insert_cities {code}: {e}")

                # Pacing only when GeoDB is still being attempted
                if api_key and geodb_disabled_reason is None:
                    await asyncio.sleep(GEODB_REQ_SPACING_SEC)

            if geodb_disabled_reason:
                log.info("GeoDB phase disabled: %s", geodb_disabled_reason)

        completed_at = datetime.now(timezone.utc)
        await db.hydration_state.update_one(
            {"key": "v1"},
            {"$set": {
                "status": "completed",
                "completed_at": completed_at,
                "countries_inserted": countries_inserted,
                "cities_inserted": cities_inserted,
                "errors": errors[:50],
                "duration_seconds": (completed_at - started_at).total_seconds(),
            }},
        )
        log.info(
            "Hydration done in %ds: +%d countries, +%d cities, %d errors",
            int((completed_at - started_at).total_seconds()),
            countries_inserted,
            cities_inserted,
            len(errors),
        )
        return {
            "ok": True,
            "countries_inserted": countries_inserted,
            "cities_inserted": cities_inserted,
            "errors": errors[:20],
            "duration_seconds": (completed_at - started_at).total_seconds(),
        }

    except Exception as e:
        log.exception("Hydration failed")
        await db.hydration_state.update_one(
            {"key": "v1"},
            {"$set": {"status": "failed", "error": str(e), "failed_at": datetime.now(timezone.utc)}},
        )
        raise


def _strip_id(d: dict) -> dict:
    d = dict(d)
    d.pop("_id", None)
    # serialize datetimes for JSON
    for k, v in list(d.items()):
        if isinstance(v, datetime):
            d[k] = v.isoformat()
    return d


async def get_status(db) -> dict:
    state = await db.hydration_state.find_one({"key": "v1"})
    counts = {
        "countries": await db.countries.count_documents({}),
        "cities": await db.cities.count_documents({}),
        "places": await db.places.count_documents({}),
    }
    return {"state": _strip_id(state) if state else None, "counts": counts}
