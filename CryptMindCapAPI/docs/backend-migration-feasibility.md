# Backend Migration Feasibility: Python FastAPI → Java or C#

**Date**: 2026-04-22
**Scope**: Migrate `cryptmind-platform/backend` (FastAPI, unified gateway for CryptMind + Mystweld) to C#/.NET 8. API contracts unchanged.

---

## Executive Summary

Migration is feasible and low-to-medium complexity. The backend is stateless, well-structured, and has no Python-specific logic that resists porting. **C# / .NET 8 is the chosen platform**, primarily because of licensing simplicity, async model parity with the existing Python code, and a smaller operational surface area.

The backend is a **unified gateway** serving both CryptMind and Mystweld from a single FastAPI process (port 8081). The C# rewrite must preserve this structure.

---

## Question 1: Java Runtime Legal Requirements

### The Oracle JDK problem

Your concern is valid. Oracle JDK requires a commercial subscription for **any production use** since Oracle changed licensing in 2019 (JDK 17+). The exact boundary of what counts as "production" is deliberately ambiguous in Oracle's terms. This is not a suitable runtime for an independent developer.

### The solution: OpenJDK distributions

The Java ecosystem has fully functional, Oracle-free builds of the OpenJDK reference implementation. These are production-ready, TCK-certified (meaning they are legally and technically Java), and free:

| Distribution | Maintainer | License | LTS Support |
|---|---|---|---|
| **Eclipse Temurin** | Eclipse Adoptium | GPL v2+CE | Yes (8, 11, 17, 21) |
| **Amazon Corretto** | AWS | GPL v2+CE | Yes |
| **Microsoft Build of OpenJDK** | Microsoft | GPL v2+CE | Yes |
| **Azul Zulu Community** | Azul | GPL v2+CE | Yes |
| **GraalVM Community** | Oracle Labs | GPL v2+CE | Yes (+ native compile) |

**Recommendation if using Java**: Eclipse Temurin 21 LTS. Zero Oracle involvement, no licensing ambiguity, Docker images on every major registry, used by millions in production.

The framework layer (Spring Boot, Quarkus, Micronaut) is Apache 2.0 — completely free.

### .NET has no equivalent concern

.NET Core / .NET 5+ is MIT licensed, owned by the .NET Foundation, and has no commercial use restrictions of any kind. No runtime licensing to think about.

---

## Question 2: Operational Stability & Complexity

### Current deployment

```
Production (systemd / bare metal):
  - MariaDB 10.11 (3 databases: cryptmind_flags, cryptmind_storage, cryptmind_logs)
  - CouchDB 3
  - uvicorn (port 8081), systemd service named "fourshards-api", runs as "fourshards" user
  - Log aggregation cron: every 5 minutes (not hourly)
  - Ansible-managed deployment (infrastructure/ansible/fastapi.yaml)

Local development only:
  - Docker Compose (not used in production)
```

Simple and solid. The question is whether the replacement adds operational friction.

### Java (Spring Boot)

**Pros:**
- Self-contained fat JAR — copy one file, run it
- Embedded Tomcat/Netty — no separate web server to manage
- Mature Docker ecosystem (`eclipse-temurin:21-jre-alpine` ~200MB)
- Spring Boot Actuator gives health, metrics, readiness endpoints out of the box

**Cons:**
- JVM warm-up: service restart takes 3–8 seconds to reach peak performance (JIT compilation)
- Memory baseline: ~300–500MB for a small app before handling any requests
- Spring Boot auto-configuration is powerful but can be opaque when something goes wrong
- Spring WebFlux (reactive/async) is a significantly different mental model from Python asyncio

**Mitigation options:**
- GraalVM native image compilation eliminates warm-up and cuts memory to ~50–100MB, but adds build complexity and restrictions (no reflection, no dynamic class loading — this matters for some libraries)
- Spring MVC (thread-per-request, non-reactive) avoids the reactive complexity and is adequate for this traffic level

### C# / .NET 8

**Pros:**
- Fast startup: typically <500ms on service restart, often <100ms
- Low memory baseline: ~100–150MB for a comparable app
- ASP.NET Core Minimal APIs syntax is the closest thing to FastAPI in any compiled language
- `async`/`await` in C# is the same mental model as Python asyncio — porting is mechanical
- Native AOT (ahead-of-time compilation) available in .NET 8 for even smaller/faster deployments
- Background services (`IHostedService`) map directly to the systemd log aggregator
- Runs as a systemd service identically to the current uvicorn setup

**Cons:**
- Microsoft is the primary maintainer — though .NET is genuinely open source with strong community governance, some prefer full independence
- Slightly smaller ecosystem than Java for enterprise-specific tooling (irrelevant here)

---

## Component-by-Component Migration Assessment

### Routes & Middleware

| Python | Java | C# |
|---|---|---|
| FastAPI router + decorators | Spring MVC `@RestController` | ASP.NET Core Minimal APIs |
| FastAPI `Depends()` injection | Spring `@Autowired` / constructor injection | ASP.NET Core DI (constructor injection) |
| Pydantic request models | Spring `@RequestBody` + Bean Validation | `record` types + Data Annotations |
| FastAPI middleware | Spring `HandlerInterceptor` / Filter | ASP.NET Core middleware pipeline |

C# Minimal APIs: `app.MapPost("/v1/share/invite", (InviteRequest req) => ...)` — near-identical to FastAPI syntax.

---

### ECDSA Zero-Knowledge Authentication

**Current Python**: `cryptography` library, verifies ECDSA-SHA256 signature on `{timestamp}:{path}:{body}`, supports both DER and raw 64-byte formats.

| Language | Library | Notes |
|---|---|---|
| Java | `java.security.Signature` (built-in JCA) | DER format native; raw 64-byte needs manual conversion |
| C# | `System.Security.Cryptography.ECDsa` (built-in) | Both formats supported; cleaner API |

**Complexity: Low.** Both standard libraries handle ECDSA natively. The DER vs raw 64-byte format detection logic must be ported carefully — this is the one subtle piece in the auth middleware.

**Rate limiting**: Python uses `cachetools` TTL cache (in-memory).
- Java: `Bucket4j` (Apache 2.0) — feature-equivalent
- C#: `System.Runtime.Caching` or a simple `ConcurrentDictionary` + timestamp — no external library needed for this scale

---

### MariaDB (SQLAlchemy + Alembic)

| Python | Java | C# |
|---|---|---|
| SQLAlchemy ORM | Spring Data JPA / Hibernate | Entity Framework Core |
| SQLAlchemy Core (raw SQL) | jOOQ or JdbcTemplate | Dapper |
| Alembic migrations | Flyway (SQL scripts, Apache 2.0) | EF Core Migrations or Flyway |

Three separate databases (`cryptmind_flags`, `cryptmind_storage`, `cryptmind_logs`) — both Java and C# handle multiple connection strings cleanly.

**Complexity: Low.** The schema is already defined in SQL. Flyway can manage migrations in either language using the existing SQL files directly with minimal adaptation.

---

### CouchDB Client

Python uses `httpx` for direct HTTP calls to CouchDB's REST API. This ports to any language trivially — CouchDB is just HTTP+JSON.

- Java: `Spring WebClient` or `OkHttp`
- C#: `HttpClient` (built-in)

**Complexity: Very Low.**

---

### Stripe Integration

Official, actively maintained SDKs exist for both languages:
- Java: `stripe-java` (Stripe-maintained)
- C#: `Stripe.net` (Stripe-maintained)

Webhook HMAC validation, subscription event handling, customer portal — all covered by the official SDKs.

**Complexity: Low.** The subscription state machine logic (trialing → active → past_due → canceled → feature flag update) must be ported faithfully, but the SDK handles all Stripe protocol details.

---

### Email (Resend)

- Java: Resend Java SDK exists, or plain HTTP POST to Resend API
- C#: Resend .NET SDK, or plain HTTP

**Complexity: Very Low.**

---

### Proof-of-Work Challenges

SHA-256 with leading zeros. Both Java (`MessageDigest`) and C# (`SHA256`) have this built into the standard library.

**Complexity: Very Low.**

---

### Log Aggregator

`log_aggregator.py` — parses JSON nginx/API logs, detects attack patterns, writes aggregations to `cryptmind_logs` MariaDB. Currently runs as a cron job every 5 minutes.

- C#: Implement as `IHostedService` with a timer — runs inside the same process, no separate systemd unit needed.

**Complexity: Low.** Pure parsing + regex + SQL writes.

---

### Feature Flag System

Simple MariaDB reads with in-memory defaults and ETag caching. Trivial in either language.

**Complexity: Very Low.**

---

### Legacy Auth Bridge

90-day migration window with token issuance and ID mapping. Short-lived in-memory token storage + MariaDB lookups. Straightforward to port.

**Complexity: Low.**

---

## Risk Areas

| Risk | Severity | Notes |
|---|---|---|
| ECDSA DER vs raw 64-byte format handling | Medium | Must be tested thoroughly — this is the auth path |
| Stripe webhook idempotency | Medium | Ensure event deduplication logic is preserved |
| Rate limiting correctness | Medium | TTL cache semantics must match existing behaviour exactly |
| PoW challenge TTL and replay window | Low | Simple logic, easy to verify |
| Multiple MariaDB connections | Low | Both languages handle multiple connection strings cleanly |
| Log aggregation attack detection regexes | Low | Port the regex patterns exactly; test against known log samples |

---

## Decision: C# / .NET 8 (Rider)

Chosen. Reasons:
- Zero runtime licensing concerns (MIT)
- `async`/`await` maps directly to Python asyncio — porting is mechanical
- ASP.NET Core Minimal APIs is the closest framework to FastAPI in any compiled language
- Fast service restart, low memory footprint
- Runs as a systemd service identically to the current uvicorn setup

### What does not change

- MariaDB schema — unchanged, migrate with Flyway using existing SQL files
- CouchDB interaction — just HTTP, identical behaviour
- Environment variables — same names, same values
- Local dev Docker Compose — same services, just a different app image
- API contracts — identical by requirement

---

## Chosen Stack

| Concern | Library | License |
|---|---|---|
| Web framework | ASP.NET Core 8 Minimal APIs | MIT |
| ORM | Entity Framework Core 8 | MIT |
| Lightweight SQL | Dapper | Apache 2.0 |
| DB migrations | EF Core Migrations or Flyway | MIT / Apache 2.0 |
| Stripe | Stripe.net | Apache 2.0 |
| Email | Resend .NET SDK or HttpClient | — |
| ECDSA | `System.Security.Cryptography` (built-in) | MIT |
| Background jobs | `IHostedService` (built-in) | MIT |
| Config | `Microsoft.Extensions.Configuration` (built-in) | MIT |
| Health checks | `Microsoft.AspNetCore.Diagnostics.HealthChecks` (built-in) | MIT |
| IDE | JetBrains Rider | Commercial |
