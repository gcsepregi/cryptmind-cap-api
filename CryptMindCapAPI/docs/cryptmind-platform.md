# CryptMind Platform — Production Codebase

**Location**: `/Users/gcsepregi/Projects/cryptmind-platform`
**Version**: 0.65.0
**Status**: Production monorepo containing both Angular frontends and the FastAPI backend.

---

## Overview

This is the actual production codebase. It is an NX 22 monorepo containing:
- Two Angular 20 frontends (CryptMind + Mystweld)
- A single FastAPI backend serving both as a unified gateway
- Ansible infrastructure automation
- Azure Pipelines CI/CD

The older standalone Angular app at `/Users/gcsepregi/Projects/cryptmind-react/cryptmind-angular` is a historical reference. The platform monorepo is the authoritative source.

---

## Monorepo Structure

```
cryptmind-platform/
├── apps/
│   ├── cryptmind/          # Coach-facing Angular app (Electron + PWA)
│   └── mystweld/           # Client-facing Angular app (PWA)
├── libs/
│   ├── platform/           # Shared Angular utilities
│   └── ui/components/      # Shared UI component library
├── backend/                # FastAPI unified gateway
├── infrastructure/
│   ├── ansible/            # Deployment playbooks
│   └── certs/              # SSL certificates
└── azure-pipelines.yml     # CI/CD pipeline
```

---

## Backend

**Location**: `backend/`
**Framework**: FastAPI 0.111+ / Uvicorn / Python 3.11+
**Entry point**: `backend/app.py` — unified gateway, registers routers for both apps

### Directory Layout

```
backend/
├── app.py                              # Main entry point (43 lines)
├── requirements.txt
├── .env.example                        # Full configuration template
├── docker-compose.yml                  # Local dev only
│
├── apps/
│   ├── cryptmind/
│   │   ├── app.py
│   │   └── endpoints/
│   │       ├── auth.py           # PoW challenge (46 lines)
│   │       ├── billing.py        # Stripe subscriptions (975 lines)
│   │       ├── practices.py      # Practice spaces (882 lines)
│   │       ├── pro_payments.py   # Pro tier payments (540 lines)
│   │       ├── spaces.py         # Shared spaces (296 lines)
│   │       ├── logs.py           # Log aggregation / analytics (1034 lines)
│   │       ├── features.py       # Feature flags (29 lines)
│   │       ├── feedback.py       # User feedback (99 lines)
│   │       ├── migration.py      # Legacy auth bridge (235 lines)
│   │       └── shared.py         # Shared utilities (154 lines)
│   │
│   └── mystweld/
│       ├── app.py
│       └── endpoints/
│           └── oracle.py
│
├── middleware/
│   ├── zero_knowledge_auth.py    # ECDSA P-256 auth middleware
│   ├── legacy_auth_bridge.py     # Legacy compatibility
│   └── security_headers.py
│
├── libs/
│   ├── platform/
│   │   ├── auth/middleware.py    # ZK auth implementation
│   │   ├── database/             # CouchDB + MariaDB connections
│   │   ├── config/settings.py
│   │   └── utils/               # crypto, challenge_store, db helpers
│   └── shared/
│       ├── models/               # practices, spaces, oracle, feedback, provision
│       └── services/             # features, storage, coaching, oracle_service
│
├── migrations_logs/              # Alembic migrations for cryptmind_logs DB
└── migrations_storage/           # Alembic migrations for cryptmind_storage DB
```

### API Routes

**CryptMind** (10 routers, base `/v1`):
```
GET  /auth/pow-challenge              # Proof-of-work challenge
GET  /auth/health
POST /spaces/personal/invite          # Coach creates invitation
GET  /practices/invites/validate/{token}  # Client validates token (no auth)
POST /spaces/personal/accept          # Client accepts invitation, retrieves key
POST /spaces/personal/credentials     # CouchDB credentials for space
     /billing/*                       # Stripe subscription management
     /features/*                      # Feature flag reads
     /logs/*                          # Log aggregation
     /migration/*                     # Legacy auth bridge
```

**Mystweld** (1 router):
```
     /oracle/*                        # Oracle endpoints
```

### Database Architecture

| Database | Type | Purpose |
|---|---|---|
| CouchDB | Document store | Per-user encrypted DBs (`user-<hash>`), per-practice DBs (`practice-<id>`) |
| `cryptmind_flags` | MariaDB | Feature flags, entitlements |
| `cryptmind_storage` | MariaDB | Spaces, practices, invitations |
| `cryptmind_logs` | MariaDB | Nginx/API log aggregation, attack detection |

SQLite fallback available for local dev (`USE_MARIADB=false`). Migrations via Alembic.

### Key Environment Variables

```bash
# CouchDB
COUCH_URL, COUCH_ADMIN_USER, COUCH_ADMIN_PASS

# MariaDB
USE_MARIADB=true, MARIADB_HOST, MARIADB_USER, MARIADB_PASSWORD
MARIADB_FLAGS_DB=cryptmind_flags
MARIADB_STORAGE_DB=cryptmind_storage
MARIADB_LOGS_DB=cryptmind_logs

# Stripe
STRIPE_SECRET_KEY, STRIPE_WEBHOOK_SECRET
STRIPE_PRICE_BASIC_MONTH, STRIPE_PRICE_PRO_MONTH, STRIPE_PRICE_PRO_PLUS_MONTH
STRIPE_PRICE_BASIC_YEAR, STRIPE_PRICE_PRO_YEAR, STRIPE_PRICE_PRO_PLUS_YEAR

# Email
RESEND_API_KEY, SMTP_FROM_EMAIL=noreply@cryptmind.io

# App URLs
APP_BASE_URL, CRYPTMIND_BASE_URL=https://cryptmind.app
MYSTWELD_BASE_URL=https://mystweld.app
INVITE_TOKEN_SECRET, IP_HASH_SECRET
```

---

## Frontend Applications

Both are Angular 20, TypeScript 5.9.

| App | Path | Port | Target |
|---|---|---|---|
| CryptMind | `apps/cryptmind/` | 4200 | Desktop (Electron) + PWA |
| Mystweld | `apps/mystweld/` | 4201 | PWA |

Each has four build environments: development, test, sandbox, production.

---

## Infrastructure & Deployment

### Servers

| Environment | IP | Branches |
|---|---|---|
| Test | `217.154.125.190` | `develop` |
| Sandbox | `217.154.125.190` | `sandbox` |
| Production | `87.106.100.206` | `main` |

All servers: SSH key auth, `deploy` user, Ubuntu/Debian.

### Ansible Playbooks (`infrastructure/ansible/`)

| Playbook | Purpose |
|---|---|
| `site.yaml` | Nginx static site deployment |
| `fastapi.yaml` | FastAPI app + Nginx proxy (548 lines, most complex) |
| `mariadb.yaml` | MariaDB 10.11 setup, databases, backups |
| `couchdb.yaml` | CouchDB + Nginx proxy |
| `gotosocial.yaml` | GoToSocial federated service |

**FastAPI deployment details** (fastapi.yaml):
- System user: `fourshards`
- Service name: `fourshards-api`
- Base directory: `/opt/cryptmind/api`
- Runs via systemd: `uvicorn app:app --host 127.0.0.1 --port 8081 --workers 1`
- Alembic migrations run automatically on deploy (if `USE_MARIADB=true`)
- Cron: log aggregation every 5 minutes
- Systemd hardening: `NoNewPrivileges`, `PrivateTmp`, `ProtectSystem`, `ProtectHome`

**MariaDB details** (mariadb.yaml):
- Three databases created: `cryptmind_flags`, `cryptmind_storage`, `cryptmind_logs`
- InnoDB tuning: `innodb_buffer_pool_size=512M`, `max_connections=200`
- Daily backups to `/opt/cryptmind/backups/mariadb/`, 30-day retention

### Secrets Management

**Local deployments**: Read from flat files in `infrastructure/ansible/secrets/{prod,test,sandbox}/shared/`:
```
couchdb_admin_password.txt
mariadb_root_password.txt
ip_hash_secret.txt
invite_token_secret.txt
```

**CI/CD**: Azure Pipelines Variable Group `test-environment` injects secrets as env vars.

### CI/CD — Azure Pipelines

Triggers on pushes to `main`, `develop`, `sandbox` branches.

| Stage | Runs when |
|---|---|
| CI (build + test) | Always |
| Deploy | `develop` or `sandbox` branch, non-PR only |

Deploy jobs: `deploy_cryptmind` and `deploy_mystweld` run in parallel. Production deploys are manual (not pipeline-automated from `main`).

---

## Backend Rewrite Context

The backend at `backend/` is the source for the planned C# / .NET 8 rewrite. Key points:
- Unified gateway pattern must be preserved (one service, two apps)
- Three separate MariaDB databases must be mapped to three connection strings
- Alembic migrations → Flyway (existing SQL files can be ported directly)
- Log aggregator (`log_aggregator.py`) → `IHostedService` with timer
- Deployment as systemd service (`fourshards-api`) stays the same
