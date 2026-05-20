"""Backend tests for TravelReview - dynamic data hydration + admin + regression."""
import os
import time
import uuid
import requests

BASE = os.environ.get("EXPO_PUBLIC_BACKEND_URL", "https://wiki-mobile-app.preview.emergentagent.com").rstrip("/") + "/api"
ADMIN_TOKEN = "travelreview-admin-secret-change-me"

results = []  # list of (name, passed, info)


def record(name, passed, info=""):
    status = "PASS" if passed else "FAIL"
    print(f"[{status}] {name} :: {info}")
    results.append((name, passed, info))


def wait_for_hydration(timeout=180):
    """Poll /api/admin/hydration-status until completed or timeout."""
    deadline = time.time() + timeout
    last = None
    while time.time() < deadline:
        try:
            r = requests.get(f"{BASE}/admin/hydration-status", timeout=15)
            if r.status_code == 200:
                data = r.json()
                last = data
                st = (data.get("state") or {}).get("status")
                if st == "completed":
                    return data
        except Exception as e:
            last = {"error": str(e)}
        time.sleep(5)
    return last


def test_hydration_status():
    r = requests.get(f"{BASE}/admin/hydration-status", timeout=15)
    if r.status_code != 200:
        record("hydration-status returns 200", False, f"status={r.status_code} body={r.text[:200]}")
        return None
    data = r.json()
    state = data.get("state") or {}
    counts = data.get("counts") or {}
    record("hydration-status state.status==completed", state.get("status") == "completed",
           f"status={state.get('status')} inserted={state.get('countries_inserted')}")
    record("hydration-status counts.countries>=120", counts.get("countries", 0) >= 120,
           f"countries={counts.get('countries')}")
    record("hydration-status counts.cities>=130", counts.get("cities", 0) >= 130,
           f"cities={counts.get('cities')}")
    return data


def test_list_countries():
    r = requests.get(f"{BASE}/countries", timeout=30)
    if r.status_code != 200:
        record("GET /countries 200", False, f"status={r.status_code}")
        return
    countries = r.json().get("countries", [])
    record("GET /countries returns >=120", len(countries) >= 120, f"got {len(countries)}")
    ids = {c.get("country_id") for c in countries}
    for cid in ["c_italy", "c_japan", "c_france", "c_thailand", "c_peru"]:
        record(f"contains curated {cid}", cid in ids, "")
    # Dynamic ones
    dynamic = [c for c in countries if c.get("source") == "rest_countries"]
    record("has dynamic countries (source=rest_countries)", len(dynamic) >= 100, f"got {len(dynamic)}")
    sample_ids = {c.get("country_id") for c in dynamic}
    for expected in ["c_us", "c_in", "c_de", "c_br"]:
        record(f"dynamic contains {expected}", expected in sample_ids, "")
    # field validation on dynamic sample
    if dynamic:
        s = dynamic[0]
        record("dynamic country has name+code+image", all(s.get(k) for k in ["name", "code", "image"]),
               f"sample={s.get('country_id')}")


def test_us_cities():
    r = requests.get(f"{BASE}/countries/c_us/cities", timeout=15)
    if r.status_code != 200:
        record("GET c_us cities 200", False, f"status={r.status_code}")
        return
    cities = r.json().get("cities", [])
    record("c_us cities >=1", len(cities) >= 1, f"got {len(cities)}")
    if cities:
        has_capital = any(c.get("source") == "rest_countries_capital" for c in cities)
        record("c_us has capital city with source=rest_countries_capital", has_capital, "")
        # Wikipedia image
        wiki_img = any("wikipedia" in (c.get("image") or "").lower() or "wikimedia" in (c.get("image") or "").lower() for c in cities)
        record("c_us city has Wikipedia/Wikimedia image url", wiki_img,
               f"images={[c.get('image') for c in cities[:3]]}")


def test_italy_cities():
    r = requests.get(f"{BASE}/countries/c_italy/cities", timeout=15)
    if r.status_code != 200:
        record("GET c_italy cities 200", False, f"status={r.status_code}")
        return
    cities = r.json().get("cities", [])
    names = {c.get("name") for c in cities}
    for n in ["Rome", "Florence", "Amalfi Coast"]:
        record(f"c_italy has curated city {n}", n in names, "")
    record("c_italy has at least 3 cities", len(cities) >= 3, f"got {len(cities)}")


def test_admin_refresh_auth():
    # No auth header
    r = requests.post(f"{BASE}/admin/refresh-data", timeout=15)
    record("POST /admin/refresh-data no-auth → 401", r.status_code == 401, f"got {r.status_code}")
    # Wrong token
    r = requests.post(f"{BASE}/admin/refresh-data", headers={"Authorization": "Bearer wrong-token-123"}, timeout=15)
    record("POST /admin/refresh-data wrong-token → 403", r.status_code == 403, f"got {r.status_code}")
    # Correct token
    r = requests.post(f"{BASE}/admin/refresh-data", headers={"Authorization": f"Bearer {ADMIN_TOKEN}"}, timeout=120)
    if r.status_code != 200:
        record("POST /admin/refresh-data correct-token → 200", False, f"got {r.status_code} body={r.text[:200]}")
    else:
        data = r.json()
        record("POST /admin/refresh-data correct-token → 200 with summary", isinstance(data, dict),
               f"keys={list(data.keys())[:8]}")


def test_regression():
    # Register fresh user with realistic-looking data
    suffix = uuid.uuid4().hex[:8]
    email = f"laura.bianchi.{suffix}@travelreview.app"
    password = "Roma2026!"
    name = "Laura Bianchi"
    r = requests.post(f"{BASE}/auth/register", json={"email": email, "password": password, "name": name}, timeout=15)
    if r.status_code != 200:
        record("POST /auth/register", False, f"status={r.status_code} body={r.text[:200]}")
        return
    record("POST /auth/register 200", True, f"email={email}")
    token = r.json().get("token")

    # Login
    r = requests.post(f"{BASE}/auth/login", json={"email": email, "password": password}, timeout=15)
    record("POST /auth/login 200", r.status_code == 200, f"status={r.status_code}")
    if r.status_code == 200:
        token = r.json().get("token")

    headers = {"Authorization": f"Bearer {token}"}

    # /auth/me
    r = requests.get(f"{BASE}/auth/me", headers=headers, timeout=15)
    ok = r.status_code == 200 and r.json().get("user", {}).get("email") == email
    record("GET /auth/me returns user", ok, f"status={r.status_code}")

    # KYC
    r = requests.post(f"{BASE}/auth/kyc", json={"document_type": "passport", "image_base64": "ZmFrZQ=="}, headers=headers, timeout=15)
    ok = r.status_code == 200 and r.json().get("user", {}).get("verified") is True
    record("POST /auth/kyc sets verified=true", ok, f"status={r.status_code}")

    # Create review
    r = requests.post(f"{BASE}/reviews", json={"place_id": "p_colosseum", "rating": 5, "text": "Amazing!"}, headers=headers, timeout=15)
    if r.status_code != 200:
        record("POST /reviews on p_colosseum", False, f"status={r.status_code} body={r.text[:200]}")
        return
    review_id = r.json().get("review", {}).get("review_id")
    record("POST /reviews on p_colosseum 200", True, f"review_id={review_id}")

    # GET reviews
    r = requests.get(f"{BASE}/places/p_colosseum/reviews", timeout=15)
    if r.status_code == 200:
        ids = {x.get("review_id") for x in r.json().get("reviews", [])}
        record("GET /places/p_colosseum/reviews includes new review", review_id in ids, f"count={len(ids)}")
    else:
        record("GET /places/p_colosseum/reviews", False, f"status={r.status_code}")


def test_search_united():
    r = requests.get(f"{BASE}/search", params={"q": "United"}, timeout=15)
    if r.status_code != 200:
        record("GET /search?q=United", False, f"status={r.status_code}")
        return
    countries = r.json().get("countries", [])
    names = {c.get("name") for c in countries}
    expected_any = ["United States", "United Kingdom", "United Arab Emirates"]
    found = [n for n in expected_any if n in names]
    record("search 'United' finds expected countries", len(found) >= 2, f"found={found} total={len(countries)}")


def summary():
    total = len(results)
    fails = [r for r in results if not r[1]]
    print("\n========== SUMMARY ==========")
    print(f"Total: {total} | Pass: {total - len(fails)} | Fail: {len(fails)}")
    if fails:
        print("\nFAILURES:")
        for n, _, info in fails:
            print(f" - {n}: {info}")


if __name__ == "__main__":
    print(f"Using BASE={BASE}")
    print("Waiting for hydration to complete (up to 3 min)...")
    h = wait_for_hydration(180)
    print(f"Hydration final state: { (h or {}).get('state') }")
    test_hydration_status()
    test_list_countries()
    test_us_cities()
    test_italy_cities()
    test_admin_refresh_auth()
    test_regression()
    test_search_united()
    summary()
