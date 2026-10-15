# E piljara — Backend API (ASP.NET Core 9)

REST API for the E piljara platform — the single backend shared by the Next.js web shop,
the admin panel, and (Phase 2) the React Native driver app.

## Architecture (Clean Architecture)

```
src/
  EPiljara.Domain          entities, enums, domain rules (order state machine) — no dependencies
  EPiljara.Application      CQRS use cases (MediatR), DTOs, FluentValidation, interfaces
  EPiljara.Infrastructure   EF Core + PostgreSQL, JWT, password hashing, email, route optimizer, seed
  EPiljara.API             controllers, JWT auth, role policies, rate limiting, Swagger, envelope middleware
```

Dependency rule: `API → Infrastructure → Application → Domain`.

## Key decisions

- **Response envelope** `{ data, error, meta }` on every endpoint (success and failure).
- **Price snapshots**: `OrderItem` copies name/unit/price at order time — never changes later.
- **Soft delete** on `Product` (global query filter), so order history stays intact.
- **Order state machine** lives in the domain (`Order.Transitions`); illegal transitions are rejected.
- **Role-scoped JWT** — `Customer` / `Admin` / `Driver`. The driver app gets a `scope: driver`
  token and its own `/driver/*` endpoints, so the mobile app is a first-class consumer from day one.
- **Swappable services** behind interfaces: `IEmailService` (logs now → SMTP later),
  `IRouteOptimizer` (zone grouping now → Google Routes VRP later).

## Run it

```bash
# 1. Database (Postgres 16 on :5432)
docker compose up -d postgres

# 2. API (applies migrations + seeds demo data on startup in Development)
dotnet run --project src/EPiljara.API      # http://localhost:5080  (Swagger at /swagger)
```

Or run everything in Docker: `docker compose up -d` (API on :5080).

### Demo accounts

| Role   | Email             | Password |
|--------|-------------------|----------|
| Admin  | admin@epiljara.ba | admin123 |
| Kupac  | marko@example.com  | marko123  |
| Vozač  | vozac@epiljara.ba | vozac123 |

## Endpoints (base `http://localhost:5080`)

**Public** `GET /products` · `GET /products/{slug}` · `GET /categories` · `GET /producers` ·
`GET /delivery/zones` · `GET /delivery/slots` · `POST /orders` (guest checkout)

**Auth** `POST /auth/register` · `POST /auth/login` · `GET /auth/me`

**Customer** `GET /orders/mine` · `GET /orders/{id}`

**Admin** (role Admin) `GET /admin/dashboard` · `GET /admin/orders` ·
`PATCH /admin/orders/{id}/status` · `GET|POST /admin/products` · `PUT /admin/products/{id}` ·
`PATCH /admin/products/{id}/availability` · `GET /admin/drivers` · `GET|POST /admin/routes` ·
`POST /admin/routes/{id}/optimize`

**Driver** (role Driver, mobile app) `GET /driver/route?date=` ·
`PATCH /driver/orders/{id}/status`

## Migrations

```bash
dotnet tool restore   # once, restores the pinned dotnet-ef
dotnet dotnet-ef migrations add <Name> -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations
dotnet dotnet-ef database update       -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure
```

The design-time factory reads `EPILJARA_CONNECTION` (falls back to the local dev connection).

## Configuration (`appsettings.json` / env)

| Key | Env override | Notes |
|-----|--------------|-------|
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | PostgreSQL |
| `Jwt:SigningKey` | `Jwt__SigningKey` | **≥ 32 bytes**, change in production |
| `Cors:Origins` | `Cors__Origins__0` | allowed web origins |

## Connecting the frontend

Point the Next.js app at this API:

```bash
# frontend/.env.local
NEXT_PUBLIC_API_BASE_URL=http://localhost:5080
```

Remove that line to fall back to the frontend's built-in mock API.
