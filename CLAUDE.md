# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**E piljara** — a domestic-food e-commerce platform (products from Manjača delivered to families in
Banja Luka). A monorepo of two independently-built apps that share one HTTP contract:

```
frontend/   Next.js 15 web shop + admin panel (TypeScript, Tailwind v4, TanStack Query)
backend/    ASP.NET Core 9 REST API (Clean Architecture, EF Core + PostgreSQL)
```

The Android driver app lives in a separate repository (`../EPiljaraVozac`, .NET MAUI,
net10.0-android). It consumes `/driver/*` over the same HTTP contract and shares no code.

They talk over HTTP only — there is no shared code or build between them. UI text and API messages
are in Serbian; keep new user-facing strings in Serbian.

## Commands

Run frontend commands from `frontend/`, backend commands from `backend/`.

**Frontend** (needs Node):
```bash
npm install
npm run dev      # http://localhost:3000
npm run build
npm run lint     # eslint
npx tsc --noEmit # typecheck (there is no "typecheck" script; use this)
```

**Backend** (needs .NET 9 SDK — pinned to 9.0.310 in global.json; Docker for Postgres):
```bash
docker compose up -d postgres              # Postgres 16 on :5432
docker compose up -d osrm                  # self-hosted routing on :5000 (see below)
dotnet run --project src/EPiljara.API      # http://localhost:5080, Swagger at /swagger
docker compose up -d                       # or run DB + API + OSRM together (API on :5080)
```
The `osrm` service needs its map data prepared once before it will start — `backend/osrm/data/` is
git-ignored and empty on a fresh clone. `backend/osrm/README.md` has the download and preprocessing
commands. Without it the container restarts in a loop and every route silently falls back to zone sorting.
The API **auto-applies migrations and seeds demo data on startup** in Development — no manual DB setup.
The seeder is **idempotent** (it bails if any data exists), so to load changed seed data you must wipe first:
`docker exec epiljara-postgres psql -U epiljara -d epiljara -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"`
then `dotnet dotnet-ef database update …` and restart the API (kill any running `EPiljara.API` first, or the
rebuild fails with a locked-DLL error). Re-seeding gives products **new IDs** — clear the browser cart or it
breaks with `product_unavailable`.

**EF Core migrations** (dotnet-ef is pinned in `.config/dotnet-tools.json`):
```bash
dotnet tool restore   # once
dotnet dotnet-ef migrations add <Name> -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations
dotnet dotnet-ef database update       -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure
```

**There is no automated test suite** (no test projects, no `test` script). Verify changes by running the
stack and exercising the affected flow end-to-end.

Demo accounts: `admin@epiljara.ba` / `admin123` · `mujo@example.com` / `mujo123` · `vozac@epiljara.ba` / `vozac123`.

## The contract is the architecture

Everything hinges on one shared HTTP contract, so understand this before touching either side:

- **Response envelope.** *Every* endpoint (success and error) returns `{ data, error, meta }`. The
  backend produces it (`ApiControllerBase` helpers + `ExceptionHandlingMiddleware`); the frontend
  unwraps it (`apiFetch` returns `data` or throws `ApiRequestError`).
- **Single frontend API surface.** Screens never call `fetch` directly — they call
  `frontend/src/lib/api/endpoints.ts`, the one typed, resource-oriented module. `client.ts` chooses the
  target from `NEXT_PUBLIC_API_BASE_URL`: **unset** → the app's own built-in mock (route handlers under
  `frontend/src/app/api/**` backed by `frontend/src/server/mock/`); **set** (e.g. `http://localhost:5080`)
  → the real backend. `frontend/.env.local` currently points at the real backend.
- **Two implementations of the same contract.** The real ASP.NET API and the Next.js mock both serve the
  same paths/shapes. When you change an endpoint's contract, keep the mock in sync (or accept that the
  mock is the deletable dev-only path). `frontend/src/types/domain.ts` mirrors the backend DTOs 1:1.
- **Validation error keys are camelCased full property paths** (`Customer.Name` → `customer.name`; a
  nested `Input.Name` → `input.name`). Frontend forms read `err.fieldErrors["input.name"]` etc. to show
  inline errors — match this casing exactly.

## Backend (Clean Architecture + CQRS)

Dependencies point inward, enforced by project references: **API → Infrastructure → Application → Domain**.

- **Domain** — entities, enums, business rules with no framework deps.
- **Application** — CQRS use-cases via MediatR, DTOs, FluentValidation, service interfaces.
- **Infrastructure** — EF Core/Postgres, JWT, hashing, email, route optimizer, DB seed.
- **API** — controllers, JWT/role policies, rate limiting, Swagger, envelope middleware.

**Adding a use case:** define `record XxxCommand/Query(...) : IRequest<TDto>` + a handler (and optional
`AbstractValidator`) in `Application/Features/**`, then add a thin controller action that does
`Mediator.Send(...)`. MediatR and FluentValidation are auto-discovered by assembly scan — **no per-handler
DI registration**. Validation runs automatically in the MediatR pipeline (`ValidationBehavior`).

**Error handling:** throw domain exceptions; the middleware maps them to status + envelope
(`DomainException`→400, `NotFoundException`→404, `ForbiddenException`→403, `ConflictException`→409,
`UnauthorizedException`→401). Don't hand-build error responses in controllers.

**Domain invariants worth preserving:**
- Order status is a **state machine** (`Order.Transitions` / `CanTransitionTo`); illegal transitions throw
  `invalid_transition`. Don't set `Status` directly to bypass it.
- `OrderItem` **snapshots** name/unit/price at order time — never recompute from the live product.
- `Product` uses **soft delete** (`IsDeleted` + global query filter) so order history stays intact.
  Deleting a product is soft. Deleting a Category/Producer is a *hard* delete and is blocked with 409 if
  any product (including soft-deleted ones — check via `IgnoreQueryFilters()`) still references it.

**Auth:** JWT bearer, roles `Customer`/`Admin`/`Driver`. Admin endpoints use `[Authorize(Roles="Admin")]`;
the driver app is a first-class consumer with its own endpoints — `GET /driver/route`,
`PATCH /driver/orders/{id}/status` (accepts only `Delivered` or `FailedDelivery`), and
`POST /driver/route/reoptimize`. That last one takes the driver's live position as a parameter, uses it for
one computation, and **never stores or logs it**; see the logging notes under Route optimization. Rate
limiting is global plus a tighter `auth` partition. Swappable services live behind interfaces
(`IEmailService`, `IRouteOptimizer`, `IGeocodingService`).

**Order confirmation email:** `CreateOrder` calls `IEmailService.SendOrderConfirmationAsync` (failures never
block the order — wrapped in try/catch). The impl is `SmtpEmailService`, which renders `OrderEmailTemplate`
(self-contained HTML) and sends over SMTP using the `Email` section in `appsettings.json`. In dev it targets a
local **Mailpit** catcher — sent mail is viewable at http://localhost:8025 (no real mailbox needed). Only sent
when the order has a customer email; guests without one are skipped.

## Route optimization

`IRouteOptimizer` computes the visiting order for a day's deliveries. Two implementations:
`OsrmRouteOptimizer` (real) and `ZoneRouteOptimizer` (fallback, sorts by zone name and knows nothing about
roads). The fallback is registered separately and injected into the real one — **a driver must never be
left without a route because a routing service was down.**

It runs from two places: automatically when an admin assigns a route, and from
`POST /driver/route/reoptimize`, which recomputes the remaining stops from the driver's live position.
Both go through `Optimizacija.PrimijeniAsync` in `Features/Admin/AdminRoutes.cs` — do not duplicate that
logic; the renumbering below lives there and only there.

### OSRM runs in our own container, deliberately

`docker-compose.yml` hosts `osrm/osrm-backend` on `:5000` over a Bosnia and Herzegovina extract.
The public `router.project-osrm.org` demo was replaced because every optimization sent it the exact
coordinates of the warehouse and **every customer**, plus the driver's live GPS position on re-optimize,
under a `User-Agent` naming us — and its usage policy limits it to development anyway.

- `Osrm:BaseUrl` is `http://localhost:5000/` in `appsettings*.json` (API run from the IDE) and
  `http://osrm:5000/` via an environment override on the containerized `api` service — the same pattern
  Postgres already uses.
- `--verbosity WARNING` in the compose command is a **privacy control**, not a noise preference: without it
  `osrm-routed` writes every request URL, coordinates included, to its log.
- `.RemoveAllLoggers()` on the OSRM `HttpClient` in `DependencyInjection.cs` exists for the same reason —
  the default factory loggers write the full URI at Information level. **Do not remove it**, and do not
  replace it with a config filter: a filter lives in an editable file whose most likely edit is "turn up
  HTTP logging to debug the OSRM outage", which is precisely the leak.
- `backend/osrm/data/` is git-ignored (a 152 MB extract plus several hundred MB of processed files).
  `backend/osrm/README.md` has the preprocessing steps and how to refresh a stale map.

### Traps that have already bitten

- **The `/trip` parameter combination is load-bearing.** `source=first&destination=last&roundtrip=false`,
  with the warehouse appended as the final coordinate. `roundtrip=false` with only `source=first` returns
  HTTP 400 `NotImplemented` — the code sent exactly that for weeks, every optimization silently fell back
  to zone sorting, and nothing looked broken because the fallback is decent. The comment in
  `OsrmRouteOptimizer.cs` records this; leave it there.
- **OSRM takes `longitude,latitude`** — the reverse of everything else here. And every interpolated number
  uses `InvariantCulture`; a Serbian locale's decimal comma would split the URL at the wrong place.
- **Closed stops keep their `Sequence`; open ones are renumbered above the highest closed one.** There is
  **no unique index on `RouteStop(RouteId, Sequence)`**, so a collision corrupts silently and also breaks
  the driver app's Google Maps batching downstream.
- **`Status` becomes `Optimized` only when the result carries a distance.** The fallback returns an order
  but no distance, and writing `Optimized` anyway would leave nothing able to tell a real optimization from
  a degraded one. `DuzinaMetara` / `TrajanjeSekundi` / `Geometrija` on `DeliveryRoute` are nullable for the
  same reason — `null` means "not computed", and `0` would render in the driver's app as "0 km · 0 min".
- **`Magacin` is a plain class, not a positional record**, because `AddOptions`/`Configure` constrain
  `TOptions : class, new()`. It is bound with `.Validate(GeoBounds.Contains).ValidateOnStart()` — but that
  only rejects a point outside Bosnia and Herzegovina, not a *wrong* one inside it, and the shipped value is
  still a placeholder.

### Geocoding

`NominatimGeocodingService` resolves addresses through the public Nominatim service, one request per
second, with an identifying `User-Agent` it requires. It still sends **literal customer addresses to a
third party** — the same problem OSRM had before it was brought in-house, and it is still open.

## Frontend (Next.js App Router)

- **Two route trees, one app:** `src/app/(shop)` (customer shop at `/`) and `src/app/admin`
  (role-gated panel at `/admin`, guarded by `AdminGuard` which redirects non-admins to `/login`).
- **Providers** (root `providers.tsx`) nest `QueryClient → Auth → Cart → Toast`. Server state is TanStack
  Query; **cart lives in `localStorage`** (`epiljara.cart`); the JWT is stored in `localStorage`
  (`epiljara.token`) via `auth-context`.
- **Cart stores product IDs**, so after a catalog change / DB re-seed a stale cart makes checkout fail with
  `product_unavailable`. `useCart().reconcile(products)` prunes items no longer in the live catalog; the
  checkout page calls it against `useProducts()` so orders can't submit phantom items.
- **Address entry** (customer checkout and admin *Nova narudžba* at `admin/orders/new`) uses a keyless
  **OpenStreetMap + Leaflet** map picker (`components/shop/address-picker.tsx`): type → Nominatim geocode →
  draggable pin → reverse-geocode. No Google Maps key. **Coordinates are persisted** alongside the address
  (`Order.Latitude`/`Longitude`, both nullable) and drive route optimization — an earlier version of this
  file said only the address string was kept, which stopped being true when the driver app gained a map.
  Nominatim does not resolve every real address, so `null` coordinates are normal and must stay nullable
  end to end; `0,0` is in the Atlantic and this project has shipped that bug once already.
- **Order management (admin):** the orders list uses expandable cards (`components/admin/order-card.tsx`) with a
  status dropdown of valid state-machine transitions. Admins create orders via the same public create-order
  endpoint (`orderApi.create`) the customer checkout uses.
- **Admin data mutations** follow a consistent pattern: a TanStack `useMutation` calling `adminApi.*`,
  then `queryClient.invalidateQueries` for the affected keys and a toast. Admin CRUD forms are drawer
  components in `src/components/admin/*-form.tsx`.

@frontend/AGENTS.md
