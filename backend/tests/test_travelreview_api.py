"""TravelReview backend API tests."""
import os
import time
import pytest
import requests

BASE = (os.environ.get("EXPO_PUBLIC_BACKEND_URL") or os.environ.get("EXPO_BACKEND_URL", "")).rstrip("/")
API = f"{BASE}/api"
TS = int(time.time())
EMAIL = f"test_{TS}@travelreview.app"
PWD = "secret123"
NAME = "Test Traveler"

state = {}


@pytest.fixture(scope="module")
def s():
    return requests.Session()


# ---- discovery
def test_countries(s):
    r = s.get(f"{API}/countries"); assert r.status_code == 200
    items = r.json()["countries"]
    assert len(items) == 5
    assert all("_id" not in c for c in items)
    assert {c["country_id"] for c in items} >= {"c_italy", "c_japan", "c_france", "c_thailand", "c_peru"}


def test_italy_cities(s):
    r = s.get(f"{API}/countries/c_italy/cities"); assert r.status_code == 200
    cities = r.json()["cities"]
    assert len(cities) == 3
    assert all("_id" not in c for c in cities)


def test_rome_places_and_filter(s):
    r = s.get(f"{API}/cities/ct_rome/places"); assert r.status_code == 200
    all_places = r.json()["places"]
    assert len(all_places) >= 2
    assert all("_id" not in p for p in all_places)
    r2 = s.get(f"{API}/cities/ct_rome/places", params={"category": "restaurant"})
    assert r2.status_code == 200
    rests = r2.json()["places"]
    assert len(rests) >= 1
    assert all(p["category"] == "restaurant" for p in rests)


def test_place_detail(s):
    r = s.get(f"{API}/places/p_colosseum"); assert r.status_code == 200
    d = r.json()
    assert d["place"]["place_id"] == "p_colosseum"
    assert d["city"]["city_id"] == "ct_rome"
    assert d["country"]["country_id"] == "c_italy"
    for k in ("place", "city", "country"):
        assert "_id" not in d[k]


def test_search(s):
    r = s.get(f"{API}/search", params={"q": "tokyo"}); assert r.status_code == 200
    d = r.json()
    found = any(c["name"].lower() == "tokyo" for c in d["cities"])
    assert found, d


# ---- auth
def test_register_new(s):
    r = s.post(f"{API}/auth/register", json={"email": EMAIL, "password": PWD, "name": NAME})
    assert r.status_code == 200, r.text
    data = r.json()
    assert "token" in data and data["user"]["email"] == EMAIL
    assert "_id" not in data["user"]
    state["token"] = data["token"]
    state["user_id"] = data["user"]["user_id"]


def test_register_duplicate(s):
    r = s.post(f"{API}/auth/register", json={"email": EMAIL, "password": PWD, "name": NAME})
    assert r.status_code == 400


def test_login_ok_and_wrong(s):
    r = s.post(f"{API}/auth/login", json={"email": EMAIL, "password": PWD})
    assert r.status_code == 200 and "token" in r.json()
    r2 = s.post(f"{API}/auth/login", json={"email": EMAIL, "password": "wrongpw"})
    assert r2.status_code == 401


def test_me_with_and_without_token(s):
    r = s.get(f"{API}/auth/me"); assert r.status_code == 401
    r2 = s.get(f"{API}/auth/me", headers={"Authorization": f"Bearer {state['token']}"})
    assert r2.status_code == 200
    assert r2.json()["user"]["email"] == EMAIL


def test_kyc_flips_verified(s):
    r = s.post(f"{API}/auth/kyc", json={"document_type": "passport", "image_base64": "aGVsbG8="},
               headers={"Authorization": f"Bearer {state['token']}"})
    assert r.status_code == 200
    assert r.json()["user"]["verified"] is True


def test_google_session_bogus_401(s):
    r = s.post(f"{API}/auth/google/session", json={"session_token": "bogus_xxx"})
    assert r.status_code == 401


# ---- reviews
def test_create_review_updates_aggregates(s):
    r = s.post(f"{API}/reviews", json={"place_id": "p_colosseum", "rating": 5, "text": "Amazing!"},
               headers={"Authorization": f"Bearer {state['token']}"})
    assert r.status_code == 200, r.text
    rev = r.json()["review"]
    assert rev["place_id"] == "p_colosseum" and rev["rating"] == 5
    assert "_id" not in rev
    state["review_id"] = rev["review_id"]

    p = s.get(f"{API}/places/p_colosseum").json()["place"]
    assert p["review_count"] >= 1 and p["rating"] > 0

    me = s.get(f"{API}/auth/me", headers={"Authorization": f"Bearer {state['token']}"}).json()["user"]
    assert me["review_count"] >= 1
    assert "c_italy" in me["countries_visited"]


def test_place_reviews_enriched(s):
    r = s.get(f"{API}/places/p_colosseum/reviews"); assert r.status_code == 200
    items = r.json()["reviews"]
    assert any(it["review_id"] == state["review_id"] for it in items)
    item = next(it for it in items if it["review_id"] == state["review_id"])
    assert item["user_name"] == NAME and "user_verified" in item
    assert all("_id" not in it for it in items)


def test_helpful_toggle(s):
    h = {"Authorization": f"Bearer {state['token']}"}
    r = s.post(f"{API}/reviews/{state['review_id']}/helpful", headers=h)
    assert r.status_code == 200 and r.json()["voted"] is True and r.json()["helpful_count"] == 1
    r2 = s.post(f"{API}/reviews/{state['review_id']}/helpful", headers=h)
    assert r2.json()["voted"] is False and r2.json()["helpful_count"] == 0


def test_my_reviews(s):
    r = s.get(f"{API}/users/me/reviews", headers={"Authorization": f"Bearer {state['token']}"})
    assert r.status_code == 200
    items = r.json()["reviews"]
    assert any(it["review_id"] == state["review_id"] for it in items)
    item = next(it for it in items if it["review_id"] == state["review_id"])
    assert item.get("place_name") == "Colosseum"
    assert all("_id" not in it for it in items)


def test_logout(s):
    r = s.post(f"{API}/auth/logout", headers={"Authorization": f"Bearer {state['token']}"})
    assert r.status_code == 200 and r.json().get("ok") is True
