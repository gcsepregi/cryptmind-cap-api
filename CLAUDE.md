# CryptMindCapAPI — Claude Working Guide

## What this project is

A **C# / .NET 10 port** of the production Python FastAPI backend that serves both the CryptMind and Mystweld applications. The original backend lives at `/Users/gcsepregi/Projects/cryptmind-platform/backend/` and is the authoritative source of truth for API contracts, business logic, and data schemas.

The goal is a **functionally identical replacement**: same API surface, same environment variables, same MariaDB schema, same CouchDB interaction patterns. Nothing changes from the caller's perspective.

Full context in:
- `CryptMindCapAPI/docs/cryptmind-platform.md` — production monorepo overview, backend structure, routes, DB layout, deployment
- `CryptMindCapAPI/docs/backend-migration-feasibility.md` — migration rationale, component-by-component assessment, risk areas, chosen stack

---

## Current state

The solution (`CryptMindCapAPI.sln`) contains a single project (`CryptMindCapAPI/`) that is a **vanilla ASP.NET scaffold** — it still has the default `WeatherForecast` boilerplate. Nothing from the FastAPI backend has been ported yet.

Target framework: **net10.0** (the migration doc says .NET 8 but the generated project targets .NET 10 — work against net10.0).

---

## Architecture to implement

### Project layout (to be created)

```
CryptMindCapAPI/
├── Program.cs                        # Entry point, DI wiring, middleware pipeline
├── appsettings.json                  # Config (mirror env vars from .env.example)
│
├── Middleware/
│   ├── ZeroKnowledgeAuthMiddleware.cs  # ECDSA P-256 request signing
│   ├── LegacyAuthBridgeMiddleware.cs
│   └── SecurityHeadersMiddleware.cs
│
├── Apps/
│   ├── CryptMind/
│   │   └── Endpoints/
│   │       ├── AuthEndpoints.cs        # PoW challenge + health
│   │       ├── BillingEndpoints.cs     # Stripe subscriptions
│   │       ├── PracticesEndpoints.cs
│   │       ├── ProPaymentsEndpoints.cs
│   │       ├── SpacesEndpoints.cs
│   │       ├── LogsEndpoints.cs
│   │       ├── FeaturesEndpoints.cs
│   │       ├── FeedbackEndpoints.cs
│   │       └── MigrationEndpoints.cs
│   └── Mystweld/
│       └── Endpoints/
│           └── OracleEndpoints.cs
│
├── Libs/
│   ├── Platform/
│   │   ├── Auth/                      # ZK auth implementation
│   │   ├── Database/                  # EF Core contexts (3 MariaDB DBs) + CouchDB HttpClient
│   │   ├── Config/                    # Settings records bound from IConfiguration
│   │   └── Utils/                     # Crypto helpers, challenge store, DB helpers
│   └── Shared/
│       ├── Models/                    # EF entities + request/response records
│       └── Services/                  # Feature flags, storage, coaching, oracle
│
├── BackgroundServices/
│   └── LogAggregatorService.cs        # IHostedService replacing the 5-min cron
│
└── Migrations/                        # EF Core migrations (or Flyway SQL files)
    ├── Storage/
    ├── Logs/
    └── Flags/
```

### Three MariaDB databases → three EF Core DbContexts

| Database | DbContext | Purpose |
|---|---|---|
| `cryptmind_flags` | `FlagsDbContext` | Feature flags, entitlements |
| `cryptmind_storage` | `StorageDbContext` | Spaces, practices, invitations |
| `cryptmind_logs` | `LogsDbContext` | Log aggregation, attack detection |

### CouchDB

Accessed via plain `HttpClient` — no ORM. Matches the Python `httpx` usage.

---

## Routing

Each app module declares a `Slug` (e.g. `"cryptmind"`, `"mystweld"`). `Program.cs` builds every module's root group as `/{slug}/v1`, so all routes are namespaced by app.

**Production** — nginx maps domains to path prefixes before the request reaches the backend:
```
https://cryptmind.app/v1/...   →  backend: /cryptmind/v1/...
https://mystweld.app/v1/...    →  backend: /mystweld/v1/...
```

**Local development** — call the prefixed paths directly (no nginx):
```
http://localhost:5045/cryptmind/v1/...
http://localhost:5045/mystweld/v1/...
```

Adding a new app: implement `IAppModule` with a new slug, add to the array in `Program.cs`. No other changes needed.

**CryptMind routes** (under `/cryptmind/v1`):
```
GET  /auth/pow-challenge
GET  /auth/health
POST /spaces/personal/invite
GET  /practices/invites/validate/{token}
POST /spaces/personal/accept
POST /spaces/personal/credentials
     /billing/*
     /features/*
     /logs/*
     /migration/*
```

**Mystweld routes** (under `/mystweld/v1`):
```
/oracle/*
```

---

## Chosen stack (from migration doc)

| Concern | Library |
|---|---|
| Web framework | ASP.NET Core Minimal APIs |
| ORM | Entity Framework Core (Pomelo for MariaDB) |
| Lightweight SQL | Dapper |
| DB migrations | EF Core Migrations |
| Stripe | Stripe.net |
| Email | Resend .NET SDK or HttpClient |
| ECDSA auth | `System.Security.Cryptography` (built-in) |
| Background jobs | `IHostedService` (built-in) |
| CouchDB | `HttpClient` (built-in) |

---

## Key risks — handle with care

1. **ECDSA auth (ZK middleware)**: Python verifies ECDSA-SHA256 on `{timestamp}:{path}:{body}`. Supports both DER and raw 64-byte signature formats. The format detection logic must be ported exactly and tested thoroughly — this gates every authenticated request.

2. **Stripe webhook idempotency**: Preserve the deduplication / event handling state machine (trialing → active → past_due → canceled → feature flag update).

3. **Rate limiting**: Python uses `cachetools` TTL cache in-memory. Port TTL semantics exactly — use `ConcurrentDictionary` + timestamps, no external library needed.

4. **PoW replay window**: Challenge TTL and the used-challenge store must match the Python behaviour.

5. **Log aggregation regexes**: Port the attack-detection regex patterns from `log_aggregator.py` character-for-character; test against real log samples.

---

## Environment variables (same names as Python backend)

```bash
# CouchDB
COUCH_URL, COUCH_ADMIN_USER, COUCH_ADMIN_PASS

# MariaDB
USE_MARIADB=true
MARIADB_HOST, MARIADB_USER, MARIADB_PASSWORD
MARIADB_FLAGS_DB=cryptmind_flags
MARIADB_STORAGE_DB=cryptmind_storage
MARIADB_LOGS_DB=cryptmind_logs

# Stripe
STRIPE_SECRET_KEY, STRIPE_WEBHOOK_SECRET
STRIPE_PRICE_BASIC_MONTH, STRIPE_PRICE_PRO_MONTH, STRIPE_PRICE_PRO_PLUS_MONTH
STRIPE_PRICE_BASIC_YEAR, STRIPE_PRICE_PRO_YEAR, STRIPE_PRICE_PRO_PLUS_YEAR

# Email
RESEND_API_KEY
SMTP_FROM_EMAIL=noreply@cryptmind.io

# App
APP_BASE_URL
CRYPTMIND_BASE_URL=https://cryptmind.app
MYSTWELD_BASE_URL=https://mystweld.app
INVITE_TOKEN_SECRET
IP_HASH_SECRET
```

Map these to a typed `Settings` record via `IConfiguration` / `appsettings.json` / environment variable override.

---

## Deployment target (unchanged from Python)

- Systemd service `fourshards-api`, runs as `fourshards` user
- Port `8081`, listen on `127.0.0.1`
- Nginx reverse proxy in front
- Ansible playbook `infrastructure/ansible/fastapi.yaml` will be updated to deploy the .NET binary instead of uvicorn
- Log aggregation runs as `IHostedService` inside the process (replaces the 5-min cron)

---

## Coding standards

- Always use braces `{}` for all control flow blocks (`if`, `else`, `for`, `foreach`, `while`, etc.), even when the body is a single statement.
