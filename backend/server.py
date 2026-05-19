"""TravelReview backend - FastAPI + MongoDB.

Endpoints:
  Auth:    /api/auth/register, /api/auth/login, /api/auth/google/session,
           /api/auth/me, /api/auth/logout, /api/auth/kyc
  Discover:/api/countries, /api/countries/{id}/cities,
           /api/cities/{id}/places, /api/places/{id}, /api/search
  Reviews: /api/places/{id}/reviews, /api/reviews, /api/reviews/{id}/helpful
  Social:  /api/users/{id}, /api/users/{id}/follow
"""
from fastapi import FastAPI, APIRouter, HTTPException, Header, Request
from dotenv import load_dotenv
from starlette.middleware.cors import CORSMiddleware
from motor.motor_asyncio import AsyncIOMotorClient
from pydantic import BaseModel, EmailStr, Field
from pathlib import Path
from datetime import datetime, timezone, timedelta
from typing import List, Optional
import os
import uuid
import logging
import bcrypt
import jwt
import httpx

ROOT_DIR = Path(__file__).parent
load_dotenv(ROOT_DIR / ".env")

MONGO_URL = os.environ["MONGO_URL"]
DB_NAME = os.environ["DB_NAME"]
JWT_SECRET = os.environ.get("JWT_SECRET", "travelreview-dev-secret-change-me")
JWT_ALGO = "HS256"

client = AsyncIOMotorClient(MONGO_URL)
db = client[DB_NAME]

app = FastAPI(title="TravelReview API")
api = APIRouter(prefix="/api")

log = logging.getLogger("travelreview")
logging.basicConfig(level=logging.INFO)


# ---------- helpers ----------
def now_utc() -> datetime:
    return datetime.now(timezone.utc)


def make_id(prefix: str) -> str:
    return f"{prefix}_{uuid.uuid4().hex[:12]}"


def hash_pw(pw: str) -> str:
    return bcrypt.hashpw(pw.encode(), bcrypt.gensalt()).decode()


def check_pw(pw: str, hashed: str) -> bool:
    try:
        return bcrypt.checkpw(pw.encode(), hashed.encode())
    except Exception:
        return False


def create_jwt(user_id: str) -> str:
    payload = {"user_id": user_id, "exp": now_utc() + timedelta(days=7), "iat": now_utc()}
    return jwt.encode(payload, JWT_SECRET, algorithm=JWT_ALGO)


async def get_current_user(authorization: Optional[str]) -> dict:
    if not authorization or not authorization.lower().startswith("bearer "):
        raise HTTPException(status_code=401, detail="Missing bearer token")
    token = authorization.split(" ", 1)[1].strip()
    # Try JWT first
    try:
        payload = jwt.decode(token, JWT_SECRET, algorithms=[JWT_ALGO])
        user = await db.users.find_one({"user_id": payload["user_id"]}, {"_id": 0})
        if not user:
            raise HTTPException(status_code=401, detail="User not found")
        return user
    except jwt.PyJWTError:
        pass
    # Else try Emergent session token
    sess = await db.user_sessions.find_one({"session_token": token}, {"_id": 0})
    if not sess:
        raise HTTPException(status_code=401, detail="Invalid token")
    exp = sess.get("expires_at")
    if isinstance(exp, datetime) and exp.tzinfo is None:
        exp = exp.replace(tzinfo=timezone.utc)
    if exp and exp < now_utc():
        raise HTTPException(status_code=401, detail="Session expired")
    user = await db.users.find_one({"user_id": sess["user_id"]}, {"_id": 0})
    if not user:
        raise HTTPException(status_code=401, detail="User not found")
    return user


def public_user(u: dict) -> dict:
    return {
        "user_id": u["user_id"],
        "email": u.get("email"),
        "name": u.get("name"),
        "picture": u.get("picture"),
        "bio": u.get("bio", ""),
        "verified": u.get("verified", False),
        "review_count": u.get("review_count", 0),
        "follower_count": u.get("follower_count", 0),
        "following_count": u.get("following_count", 0),
        "countries_visited": u.get("countries_visited", []),
        "created_at": u.get("created_at").isoformat() if isinstance(u.get("created_at"), datetime) else u.get("created_at"),
    }


# ---------- schemas ----------
class RegisterIn(BaseModel):
    email: EmailStr
    password: str = Field(min_length=6)
    name: str


class LoginIn(BaseModel):
    email: EmailStr
    password: str


class GoogleSessionIn(BaseModel):
    session_token: str  # session_token from session-data endpoint


class KycIn(BaseModel):
    document_type: str  # passport, drivers_license, national_id
    image_base64: str


class ReviewIn(BaseModel):
    place_id: str
    rating: int = Field(ge=1, le=5)
    text: str = Field(min_length=1, max_length=2000)
    photos: List[str] = []  # base64 strings


class ProfileUpdateIn(BaseModel):
    name: Optional[str] = None
    bio: Optional[str] = None


# ---------- auth ----------
@api.post("/auth/register")
async def register(body: RegisterIn):
    existing = await db.users.find_one({"email": body.email.lower()}, {"_id": 0})
    if existing:
        raise HTTPException(status_code=400, detail="Email already registered")
    uid = make_id("usr")
    doc = {
        "user_id": uid,
        "email": body.email.lower(),
        "name": body.name,
        "password_hash": hash_pw(body.password),
        "picture": None,
        "bio": "",
        "verified": False,
        "review_count": 0,
        "follower_count": 0,
        "following_count": 0,
        "countries_visited": [],
        "following": [],
        "auth_provider": "email",
        "created_at": now_utc(),
    }
    await db.users.insert_one(doc)
    token = create_jwt(uid)
    return {"token": token, "user": public_user(doc)}


@api.post("/auth/login")
async def login(body: LoginIn):
    user = await db.users.find_one({"email": body.email.lower()}, {"_id": 0})
    if not user or not user.get("password_hash") or not check_pw(body.password, user["password_hash"]):
        raise HTTPException(status_code=401, detail="Invalid email or password")
    token = create_jwt(user["user_id"])
    return {"token": token, "user": public_user(user)}


@api.post("/auth/google/session")
async def google_session(body: GoogleSessionIn, request: Request):
    """Frontend sends session_id from Emergent auth redirect. We verify with Emergent API,
    upsert user, create our own session and return token to use as Bearer."""
    headers = {"X-Session-ID": body.session_token}
    async with httpx.AsyncClient(timeout=20.0) as cli:
        r = await cli.get("https://demobackend.emergentagent.com/auth/v1/env/oauth/session-data", headers=headers)
    if r.status_code != 200:
        raise HTTPException(status_code=401, detail="Invalid Google session")
    data = r.json()
    email = (data.get("email") or "").lower()
    if not email:
        raise HTTPException(status_code=400, detail="Email missing from Google profile")
    user = await db.users.find_one({"email": email}, {"_id": 0})
    if not user:
        uid = make_id("usr")
        user = {
            "user_id": uid,
            "email": email,
            "name": data.get("name") or email.split("@")[0],
            "picture": data.get("picture"),
            "password_hash": None,
            "bio": "",
            "verified": False,
            "review_count": 0,
            "follower_count": 0,
            "following_count": 0,
            "countries_visited": [],
            "following": [],
            "auth_provider": "google",
            "created_at": now_utc(),
        }
        await db.users.insert_one(user)
    sess_token = data["session_token"]
    await db.user_sessions.update_one(
        {"session_token": sess_token},
        {"$set": {
            "session_token": sess_token,
            "user_id": user["user_id"],
            "expires_at": now_utc() + timedelta(days=7),
            "created_at": now_utc(),
        }},
        upsert=True,
    )
    return {"token": sess_token, "user": public_user(user)}


@api.get("/auth/me")
async def me(authorization: Optional[str] = Header(None)):
    user = await get_current_user(authorization)
    return {"user": public_user(user)}


@api.post("/auth/logout")
async def logout(authorization: Optional[str] = Header(None)):
    if authorization and authorization.lower().startswith("bearer "):
        token = authorization.split(" ", 1)[1].strip()
        await db.user_sessions.delete_one({"session_token": token})
    return {"ok": True}


@api.post("/auth/kyc")
async def submit_kyc(body: KycIn, authorization: Optional[str] = Header(None)):
    """Mock KYC: any uploaded ID grants verified badge."""
    user = await get_current_user(authorization)
    await db.users.update_one(
        {"user_id": user["user_id"]},
        {"$set": {
            "verified": True,
            "kyc_document_type": body.document_type,
            "kyc_submitted_at": now_utc(),
        }},
    )
    user["verified"] = True
    return {"user": public_user(user)}


@api.patch("/auth/profile")
async def update_profile(body: ProfileUpdateIn, authorization: Optional[str] = Header(None)):
    user = await get_current_user(authorization)
    updates = {k: v for k, v in body.dict(exclude_none=True).items()}
    if updates:
        await db.users.update_one({"user_id": user["user_id"]}, {"$set": updates})
    fresh = await db.users.find_one({"user_id": user["user_id"]}, {"_id": 0})
    return {"user": public_user(fresh)}


# ---------- discovery ----------
@api.get("/countries")
async def list_countries():
    items = await db.countries.find({}, {"_id": 0}).to_list(200)
    return {"countries": items}


@api.get("/countries/{country_id}/cities")
async def list_cities(country_id: str):
    items = await db.cities.find({"country_id": country_id}, {"_id": 0}).to_list(200)
    return {"cities": items}


@api.get("/cities/{city_id}/places")
async def list_places(city_id: str, category: Optional[str] = None):
    q: dict = {"city_id": city_id}
    if category:
        q["category"] = category
    items = await db.places.find(q, {"_id": 0}).to_list(500)
    return {"places": items}


@api.get("/places/{place_id}")
async def get_place(place_id: str):
    p = await db.places.find_one({"place_id": place_id}, {"_id": 0})
    if not p:
        raise HTTPException(status_code=404, detail="Place not found")
    city = await db.cities.find_one({"city_id": p["city_id"]}, {"_id": 0})
    country = await db.countries.find_one({"country_id": p["country_id"]}, {"_id": 0}) if p.get("country_id") else None
    return {"place": p, "city": city, "country": country}


@api.get("/search")
async def search(q: str):
    if not q or len(q.strip()) < 1:
        return {"places": [], "cities": [], "countries": []}
    regex = {"$regex": q.strip(), "$options": "i"}
    places = await db.places.find({"name": regex}, {"_id": 0}).limit(20).to_list(20)
    cities = await db.cities.find({"name": regex}, {"_id": 0}).limit(20).to_list(20)
    countries = await db.countries.find({"name": regex}, {"_id": 0}).limit(20).to_list(20)
    return {"places": places, "cities": cities, "countries": countries}


# ---------- reviews ----------
@api.get("/places/{place_id}/reviews")
async def place_reviews(place_id: str):
    items = await db.reviews.find({"place_id": place_id}, {"_id": 0}).sort("created_at", -1).to_list(200)
    # attach minimal user info
    user_ids = list({r["user_id"] for r in items})
    users = await db.users.find({"user_id": {"$in": user_ids}}, {"_id": 0}).to_list(200) if user_ids else []
    umap = {u["user_id"]: u for u in users}
    for r in items:
        u = umap.get(r["user_id"], {})
        r["user_name"] = u.get("name", "Traveler")
        r["user_picture"] = u.get("picture")
        r["user_verified"] = u.get("verified", False)
        if isinstance(r.get("created_at"), datetime):
            r["created_at"] = r["created_at"].isoformat()
    return {"reviews": items}


@api.post("/reviews")
async def create_review(body: ReviewIn, authorization: Optional[str] = Header(None)):
    user = await get_current_user(authorization)
    place = await db.places.find_one({"place_id": body.place_id}, {"_id": 0})
    if not place:
        raise HTTPException(status_code=404, detail="Place not found")
    rid = make_id("rev")
    doc = {
        "review_id": rid,
        "place_id": body.place_id,
        "city_id": place["city_id"],
        "country_id": place.get("country_id"),
        "user_id": user["user_id"],
        "rating": body.rating,
        "text": body.text,
        "photos": body.photos[:10],
        "helpful_count": 0,
        "helpful_voters": [],
        "created_at": now_utc(),
    }
    await db.reviews.insert_one(doc)
    doc.pop("_id", None)
    # Update place rating aggregate
    all_revs = await db.reviews.find({"place_id": body.place_id}, {"_id": 0, "rating": 1}).to_list(2000)
    avg = sum(r["rating"] for r in all_revs) / len(all_revs) if all_revs else 0
    await db.places.update_one(
        {"place_id": body.place_id},
        {"$set": {"rating": round(avg, 2), "review_count": len(all_revs)}},
    )
    # Increment user stats + countries visited
    inc = {"review_count": 1}
    add_country = {}
    if place.get("country_id") and place["country_id"] not in user.get("countries_visited", []):
        add_country = {"$addToSet": {"countries_visited": place["country_id"]}}
    update = {"$inc": inc}
    if add_country:
        update.update(add_country)
    await db.users.update_one({"user_id": user["user_id"]}, update)
    doc["created_at"] = doc["created_at"].isoformat()
    doc["user_name"] = user["name"]
    doc["user_picture"] = user.get("picture")
    doc["user_verified"] = user.get("verified", False)
    return {"review": doc}


@api.post("/reviews/{review_id}/helpful")
async def vote_helpful(review_id: str, authorization: Optional[str] = Header(None)):
    user = await get_current_user(authorization)
    rev = await db.reviews.find_one({"review_id": review_id}, {"_id": 0})
    if not rev:
        raise HTTPException(status_code=404, detail="Review not found")
    if user["user_id"] in rev.get("helpful_voters", []):
        await db.reviews.update_one(
            {"review_id": review_id},
            {"$inc": {"helpful_count": -1}, "$pull": {"helpful_voters": user["user_id"]}},
        )
        voted = False
    else:
        await db.reviews.update_one(
            {"review_id": review_id},
            {"$inc": {"helpful_count": 1}, "$addToSet": {"helpful_voters": user["user_id"]}},
        )
        voted = True
    fresh = await db.reviews.find_one({"review_id": review_id}, {"_id": 0})
    return {"helpful_count": fresh["helpful_count"], "voted": voted}


# ---------- users / social ----------
@api.get("/users/me/reviews")
async def my_reviews(authorization: Optional[str] = Header(None)):
    user = await get_current_user(authorization)
    items = await db.reviews.find({"user_id": user["user_id"]}, {"_id": 0}).sort("created_at", -1).to_list(500)
    place_ids = list({r["place_id"] for r in items})
    places = await db.places.find({"place_id": {"$in": place_ids}}, {"_id": 0}).to_list(500) if place_ids else []
    pmap = {p["place_id"]: p for p in places}
    for r in items:
        p = pmap.get(r["place_id"], {})
        r["place_name"] = p.get("name")
        r["place_image"] = (p.get("photos") or [None])[0]
        if isinstance(r.get("created_at"), datetime):
            r["created_at"] = r["created_at"].isoformat()
    return {"reviews": items}


@api.get("/users/{user_id}")
async def get_user(user_id: str):
    u = await db.users.find_one({"user_id": user_id}, {"_id": 0})
    if not u:
        raise HTTPException(status_code=404, detail="User not found")
    return {"user": public_user(u)}


@api.post("/users/{user_id}/follow")
async def follow_user(user_id: str, authorization: Optional[str] = Header(None)):
    me_user = await get_current_user(authorization)
    if user_id == me_user["user_id"]:
        raise HTTPException(status_code=400, detail="Cannot follow yourself")
    target = await db.users.find_one({"user_id": user_id}, {"_id": 0})
    if not target:
        raise HTTPException(status_code=404, detail="User not found")
    is_following = user_id in me_user.get("following", [])
    if is_following:
        await db.users.update_one({"user_id": me_user["user_id"]}, {"$pull": {"following": user_id}, "$inc": {"following_count": -1}})
        await db.users.update_one({"user_id": user_id}, {"$inc": {"follower_count": -1}})
        return {"following": False}
    else:
        await db.users.update_one({"user_id": me_user["user_id"]}, {"$addToSet": {"following": user_id}, "$inc": {"following_count": 1}})
        await db.users.update_one({"user_id": user_id}, {"$inc": {"follower_count": 1}})
        return {"following": True}


# ---------- seed ----------
SEED_COUNTRIES = [
    {"country_id": "c_italy", "name": "Italy", "code": "IT", "description": "Renaissance art, coastal villages, and culinary mastery.",
     "image": "https://images.unsplash.com/photo-1626628193008-fa2852153d79?w=800"},
    {"country_id": "c_japan", "name": "Japan", "code": "JP", "description": "Neon-lit cities and timeless tradition.",
     "image": "https://images.pexels.com/photos/31376617/pexels-photo-31376617.png?w=800"},
    {"country_id": "c_france", "name": "France", "code": "FR", "description": "Wine country, alpine peaks, and Parisian charm.",
     "image": "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800"},
    {"country_id": "c_thailand", "name": "Thailand", "code": "TH", "description": "Tropical beaches, temples, and street food paradise.",
     "image": "https://images.unsplash.com/photo-1528181304800-259b08848526?w=800"},
    {"country_id": "c_peru", "name": "Peru", "code": "PE", "description": "Ancient ruins, Andean peaks, and Amazon rainforest.",
     "image": "https://images.unsplash.com/photo-1526392060635-9d6019884377?w=800"},
]

SEED_CITIES = [
    # Italy
    {"city_id": "ct_rome", "country_id": "c_italy", "name": "Rome", "description": "The Eternal City.", "image": "https://images.unsplash.com/photo-1552832230-c0197dd311b5?w=800"},
    {"city_id": "ct_florence", "country_id": "c_italy", "name": "Florence", "description": "Cradle of the Renaissance.", "image": "https://images.unsplash.com/photo-1543429776-2782fc8e1acd?w=800"},
    {"city_id": "ct_amalfi", "country_id": "c_italy", "name": "Amalfi Coast", "description": "Cliffside villages over turquoise sea.", "image": "https://images.unsplash.com/photo-1633321702518-7feccafb94d5?w=800"},
    # Japan
    {"city_id": "ct_tokyo", "country_id": "c_japan", "name": "Tokyo", "description": "Where future meets tradition.", "image": "https://images.pexels.com/photos/31376617/pexels-photo-31376617.png?w=800"},
    {"city_id": "ct_kyoto", "country_id": "c_japan", "name": "Kyoto", "description": "Thousand-year-old temples.", "image": "https://images.unsplash.com/photo-1493997181344-712f2f19d87a?w=800"},
    {"city_id": "ct_osaka", "country_id": "c_japan", "name": "Osaka", "description": "Japan's kitchen.", "image": "https://images.unsplash.com/photo-1590559899731-a382839e5549?w=800"},
    # France
    {"city_id": "ct_paris", "country_id": "c_france", "name": "Paris", "description": "City of light.", "image": "https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800"},
    {"city_id": "ct_nice", "country_id": "c_france", "name": "Nice", "description": "Riviera glamour.", "image": "https://images.unsplash.com/photo-1491166617655-0723a0999cfc?w=800"},
    {"city_id": "ct_lyon", "country_id": "c_france", "name": "Lyon", "description": "Gastronomic capital.", "image": "https://images.unsplash.com/photo-1524396309943-e03f5249f002?w=800"},
    # Thailand
    {"city_id": "ct_bangkok", "country_id": "c_thailand", "name": "Bangkok", "description": "Buzzing megacity.", "image": "https://images.unsplash.com/photo-1563492065599-3520f775eeed?w=800"},
    {"city_id": "ct_chiangmai", "country_id": "c_thailand", "name": "Chiang Mai", "description": "Mountains and temples.", "image": "https://images.unsplash.com/photo-1598935898639-81586f7d2129?w=800"},
    {"city_id": "ct_phuket", "country_id": "c_thailand", "name": "Phuket", "description": "Island paradise.", "image": "https://images.unsplash.com/photo-1589394815804-964ed0be2eb5?w=800"},
    # Peru
    {"city_id": "ct_cusco", "country_id": "c_peru", "name": "Cusco", "description": "Inca heartland.", "image": "https://images.unsplash.com/photo-1531968455001-5c5272a41129?w=800"},
    {"city_id": "ct_lima", "country_id": "c_peru", "name": "Lima", "description": "Pacific culinary hub.", "image": "https://images.unsplash.com/photo-1531219432768-9f540ce9714b?w=800"},
    {"city_id": "ct_arequipa", "country_id": "c_peru", "name": "Arequipa", "description": "White volcanic city.", "image": "https://images.unsplash.com/photo-1580551023330-2b1c2a4bd91d?w=800"},
]

SEED_PLACES = [
    # Rome
    {"place_id": "p_colosseum", "city_id": "ct_rome", "country_id": "c_italy", "name": "Colosseum", "category": "attraction", "description": "Iconic Roman amphitheater from 70 AD.", "address": "Piazza del Colosseo, Rome", "photos": ["https://images.unsplash.com/photo-1552832230-c0197dd311b5?w=800"]},
    {"place_id": "p_armando", "city_id": "ct_rome", "country_id": "c_italy", "name": "Armando al Pantheon", "category": "restaurant", "description": "Family-run trattoria serving classic Roman dishes.", "address": "Salita de' Crescenzi, Rome", "photos": ["https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"]},
    # Florence
    {"place_id": "p_uffizi", "city_id": "ct_florence", "country_id": "c_italy", "name": "Uffizi Gallery", "category": "attraction", "description": "Renaissance masterpieces by Botticelli, da Vinci.", "address": "Piazzale degli Uffizi, Florence", "photos": ["https://images.unsplash.com/photo-1543429776-2782fc8e1acd?w=800"]},
    # Amalfi
    {"place_id": "p_positano", "city_id": "ct_amalfi", "country_id": "c_italy", "name": "Positano Beach", "category": "attraction", "description": "Pebbled beach beneath pastel cliffside town.", "address": "Positano, Amalfi", "photos": ["https://images.unsplash.com/photo-1633321702518-7feccafb94d5?w=800"]},
    {"place_id": "p_lesirenuse", "city_id": "ct_amalfi", "country_id": "c_italy", "name": "Le Sirenuse", "category": "hotel", "description": "Iconic pink-hued luxury hotel.", "address": "Via Cristoforo Colombo, Positano", "photos": ["https://images.unsplash.com/photo-1711059985570-4c32ed12a12c?w=800"]},
    # Tokyo
    {"place_id": "p_senso_ji", "city_id": "ct_tokyo", "country_id": "c_japan", "name": "Senso-ji Temple", "category": "attraction", "description": "Tokyo's oldest Buddhist temple.", "address": "Asakusa, Tokyo", "photos": ["https://images.unsplash.com/photo-1583400015750-72d6e1ef3c6c?w=800"]},
    {"place_id": "p_sukiyabashi", "city_id": "ct_tokyo", "country_id": "c_japan", "name": "Sukiyabashi Jiro", "category": "restaurant", "description": "World-famous sushi master Jiro Ono.", "address": "Ginza, Tokyo", "photos": ["https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"]},
    {"place_id": "p_park_hyatt_tokyo", "city_id": "ct_tokyo", "country_id": "c_japan", "name": "Park Hyatt Tokyo", "category": "hotel", "description": "Sky-high luxury made famous by 'Lost in Translation'.", "address": "Shinjuku, Tokyo", "photos": ["https://images.unsplash.com/photo-1711059985570-4c32ed12a12c?w=800"]},
    # Kyoto
    {"place_id": "p_fushimi", "city_id": "ct_kyoto", "country_id": "c_japan", "name": "Fushimi Inari", "category": "attraction", "description": "Thousands of vermillion torii gates.", "address": "Fushimi Ward, Kyoto", "photos": ["https://images.unsplash.com/photo-1493997181344-712f2f19d87a?w=800"]},
    {"place_id": "p_kinkakuji", "city_id": "ct_kyoto", "country_id": "c_japan", "name": "Kinkaku-ji", "category": "attraction", "description": "Golden Pavilion Zen temple.", "address": "Kita Ward, Kyoto", "photos": ["https://images.unsplash.com/photo-1545569310-99c9a9b53e80?w=800"]},
    # Osaka
    {"place_id": "p_dotonbori", "city_id": "ct_osaka", "country_id": "c_japan", "name": "Dotonbori", "category": "attraction", "description": "Neon-lit street food paradise.", "address": "Chuo Ward, Osaka", "photos": ["https://images.unsplash.com/photo-1590559899731-a382839e5549?w=800"]},
    # Paris
    {"place_id": "p_eiffel", "city_id": "ct_paris", "country_id": "c_france", "name": "Eiffel Tower", "category": "attraction", "description": "Wrought-iron icon of Paris.", "address": "Champ de Mars, Paris", "photos": ["https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800"]},
    {"place_id": "p_septime", "city_id": "ct_paris", "country_id": "c_france", "name": "Septime", "category": "restaurant", "description": "Neo-bistro tasting-menu hotspot.", "address": "Rue de Charonne, Paris", "photos": ["https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"]},
    {"place_id": "p_ritz_paris", "city_id": "ct_paris", "country_id": "c_france", "name": "Ritz Paris", "category": "hotel", "description": "Legendary palace hotel on Place Vendôme.", "address": "Place Vendôme, Paris", "photos": ["https://images.unsplash.com/photo-1711059985570-4c32ed12a12c?w=800"]},
    # Nice
    {"place_id": "p_promenade", "city_id": "ct_nice", "country_id": "c_france", "name": "Promenade des Anglais", "category": "attraction", "description": "Iconic seafront promenade.", "address": "Nice", "photos": ["https://images.unsplash.com/photo-1491166617655-0723a0999cfc?w=800"]},
    # Lyon
    {"place_id": "p_paul_bocuse", "city_id": "ct_lyon", "country_id": "c_france", "name": "Paul Bocuse", "category": "restaurant", "description": "Three-Michelin-star French heritage.", "address": "Collonges-au-Mont-d'Or, Lyon", "photos": ["https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"]},
    # Bangkok
    {"place_id": "p_grand_palace", "city_id": "ct_bangkok", "country_id": "c_thailand", "name": "Grand Palace", "category": "attraction", "description": "Royal complex glittering with gold.", "address": "Phra Borom Maha Ratchawang, Bangkok", "photos": ["https://images.unsplash.com/photo-1563492065599-3520f775eeed?w=800"]},
    {"place_id": "p_gaggan", "city_id": "ct_bangkok", "country_id": "c_thailand", "name": "Gaggan Anand", "category": "restaurant", "description": "Progressive Indian dining theatre.", "address": "Sukhumvit, Bangkok", "photos": ["https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"]},
    {"place_id": "p_mandarin_oriental_bk", "city_id": "ct_bangkok", "country_id": "c_thailand", "name": "Mandarin Oriental Bangkok", "category": "hotel", "description": "Riverside grand dame since 1879.", "address": "Charoenkrung Rd, Bangkok", "photos": ["https://images.unsplash.com/photo-1711059985570-4c32ed12a12c?w=800"]},
    # Chiang Mai
    {"place_id": "p_doi_suthep", "city_id": "ct_chiangmai", "country_id": "c_thailand", "name": "Doi Suthep", "category": "attraction", "description": "Mountain temple with city views.", "address": "Doi Suthep, Chiang Mai", "photos": ["https://images.unsplash.com/photo-1598935898639-81586f7d2129?w=800"]},
    # Phuket
    {"place_id": "p_phi_phi", "city_id": "ct_phuket", "country_id": "c_thailand", "name": "Phi Phi Islands", "category": "attraction", "description": "Limestone cliffs and emerald lagoons.", "address": "Phi Phi, Phuket", "photos": ["https://images.unsplash.com/photo-1589394815804-964ed0be2eb5?w=800"]},
    # Cusco
    {"place_id": "p_machu_picchu", "city_id": "ct_cusco", "country_id": "c_peru", "name": "Machu Picchu", "category": "attraction", "description": "Lost city of the Incas.", "address": "Aguas Calientes, Cusco", "photos": ["https://images.unsplash.com/photo-1526392060635-9d6019884377?w=800"]},
    {"place_id": "p_chinchero", "city_id": "ct_cusco", "country_id": "c_peru", "name": "Chinchero Market", "category": "attraction", "description": "Andean weaving traditions.", "address": "Chinchero, Cusco", "photos": ["https://images.unsplash.com/photo-1531968455001-5c5272a41129?w=800"]},
    # Lima
    {"place_id": "p_central", "city_id": "ct_lima", "country_id": "c_peru", "name": "Central", "category": "restaurant", "description": "World's Best Restaurant 2023 - elevation menus.", "address": "Barranco, Lima", "photos": ["https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"]},
    {"place_id": "p_miraflores", "city_id": "ct_lima", "country_id": "c_peru", "name": "Miraflores Cliffs", "category": "attraction", "description": "Pacific-facing parkland.", "address": "Miraflores, Lima", "photos": ["https://images.unsplash.com/photo-1531219432768-9f540ce9714b?w=800"]},
    {"place_id": "p_hotel_b", "city_id": "ct_lima", "country_id": "c_peru", "name": "Hotel B", "category": "hotel", "description": "Belle Époque boutique mansion.", "address": "Barranco, Lima", "photos": ["https://images.unsplash.com/photo-1711059985570-4c32ed12a12c?w=800"]},
    # Arequipa
    {"place_id": "p_santa_catalina", "city_id": "ct_arequipa", "country_id": "c_peru", "name": "Santa Catalina Monastery", "category": "attraction", "description": "Citadel within the city, painted ochre and blue.", "address": "Santa Catalina, Arequipa", "photos": ["https://images.unsplash.com/photo-1580551023330-2b1c2a4bd91d?w=800"]},
    {"place_id": "p_zigzag", "city_id": "ct_arequipa", "country_id": "c_peru", "name": "ZigZag Restaurant", "category": "restaurant", "description": "Andean meats on volcanic stone.", "address": "Zela 210, Arequipa", "photos": ["https://images.pexels.com/photos/27626762/pexels-photo-27626762.png?w=800"]},
    # Extra
    {"place_id": "p_tsukiji", "city_id": "ct_tokyo", "country_id": "c_japan", "name": "Tsukiji Outer Market", "category": "attraction", "description": "Street food and fresh seafood stalls.", "address": "Tsukiji, Tokyo", "photos": ["https://images.unsplash.com/photo-1583400015750-72d6e1ef3c6c?w=800"]},
    {"place_id": "p_seine_cruise", "city_id": "ct_paris", "country_id": "c_france", "name": "Seine River Cruise", "category": "attraction", "description": "Float past Parisian monuments at sunset.", "address": "Pont Neuf, Paris", "photos": ["https://images.unsplash.com/photo-1502602898657-3e91760cbb34?w=800"]},
]


async def seed_data():
    if await db.countries.count_documents({}) == 0:
        await db.countries.insert_many([{**c} for c in SEED_COUNTRIES])
        log.info("Seeded %d countries", len(SEED_COUNTRIES))
    if await db.cities.count_documents({}) == 0:
        await db.cities.insert_many([{**c} for c in SEED_CITIES])
        log.info("Seeded %d cities", len(SEED_CITIES))
    if await db.places.count_documents({}) == 0:
        docs = []
        for p in SEED_PLACES:
            d = {**p, "rating": 0, "review_count": 0, "claimed": False, "created_at": now_utc()}
            docs.append(d)
        await db.places.insert_many(docs)
        log.info("Seeded %d places", len(SEED_PLACES))


async def ensure_indexes():
    await db.users.create_index("email", unique=True)
    await db.users.create_index("user_id", unique=True)
    await db.user_sessions.create_index("session_token", unique=True)
    await db.user_sessions.create_index("expires_at", expireAfterSeconds=0)
    await db.countries.create_index("country_id", unique=True)
    await db.cities.create_index("city_id", unique=True)
    await db.cities.create_index("country_id")
    await db.places.create_index("place_id", unique=True)
    await db.places.create_index("city_id")
    await db.reviews.create_index("review_id", unique=True)
    await db.reviews.create_index("place_id")
    await db.reviews.create_index("user_id")


# ---------- root ----------
@api.get("/")
async def root():
    return {"service": "TravelReview", "status": "ok"}


app.include_router(api)
app.add_middleware(
    CORSMiddleware,
    allow_credentials=True,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.on_event("startup")
async def startup():
    await ensure_indexes()
    await seed_data()


@app.on_event("shutdown")
async def shutdown():
    client.close()
