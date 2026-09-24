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

Other docs worth knowing: `PRODUCTION.md` (required env vars, first admin, security posture — update it
when you add config or a production-relevant behaviour), `docs/superpowers/specs|plans/` (design specs
and implementation plans of past features — read the matching one before extending route optimization,
the map or the driver flow).

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
docker compose up -d nominatim             # self-hosted geocoding on :8080 (see below)
dotnet run --project src/EPiljara.API      # http://localhost:5080, Swagger at /swagger
# For the Android driver app (../EPiljaraVozac) the API must be reachable from the emulator
# via 10.0.2.2, and the default binds localhost only — use:
dotnet run --project src/EPiljara.API --urls http://0.0.0.0:5080
docker compose up -d                       # or run the whole stack together (API on :5080)
docker run -d --name mailpit -p 1025:1025 -p 8025:8025 axllent/mailpit   # email catcher, NOT in compose
```
The containerized `api` service is wired differently from an IDE run: it points `Nominatim:BaseUrl` at the
`nominatim` container (so geocoding returns nothing until that import finishes), uses its own dev JWT key,
and its `Email:SmtpHost` is still `localhost` — i.e. inside the container, so confirmation mail does not
reach a Mailpit on the host. Prefer `dotnet run` when working on email or geocoding.
Without `Jwt:SigningKey` (empty in `appsettings.json`) the API falls back to a hard-coded insecure key in
Development only and **refuses to start** in any other environment.
The `osrm` service needs its map data prepared once before it will start — `backend/osrm/data/` is
git-ignored and empty on a fresh clone. `backend/osrm/README.md` has the download and preprocessing
commands. Without it the container restarts in a loop and every route silently falls back to zone sorting.
The `nominatim` service is **optional but recommended**: its first start imports the map and takes tens of
minutes (`backend/nominatim/README.md`), and until you point `Nominatim:BaseUrl` at it the app geocodes
against the public Nominatim instead. Neither is needed just to run the shop.
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

**Backend ima testove** (`backend/tests/EPiljara.Application.Tests`, xUnit + SQLite in-memory):
```bash
dotnet test          # iz backend/
```
Pokriveno je domensko pravilo `GeoBounds`, infrastruktura za testiranje handlera (`TestDb`,
`FakeCurrentUser`) nad SQLite in-memory bazom sa stvarno enforsovanim stranim ključevima, i prvi
handler testovi (`AddressCommandsTests`). **Frontend nema test setup** — provjerava se sa
`npx tsc --noEmit`, `npm run lint` i prolaskom kroz tok uz pokrenut stek.

Demo accounts: `admin@epiljara.ba` / `admin123` · `mujo@example.com` / `mujo123` · `vozac@epiljara.ba` / `vozac123`.

## The contract is the architecture

Everything hinges on one shared HTTP contract, so understand this before touching either side:

- **Response envelope.** *Every* endpoint (success and error) returns `{ data, error, meta }`. The
  backend produces it (`ApiControllerBase` helpers + `ExceptionHandlingMiddleware`); the frontend
  unwraps it (`apiFetch` returns `data` or throws `ApiRequestError`).
- **Single frontend API surface.** Screens never call `fetch` directly — they call
  `frontend/src/lib/api/endpoints.ts`, the one typed, resource-oriented module. `client.ts` derives
  `API_BASE` as `NEXT_PUBLIC_API_BASE_URL || "/api"`: **unset/empty** → the app's own built-in mock (route
  handlers under `frontend/src/app/api/**` backed by `frontend/src/server/mock/`, data in `store.ts`);
  **set** (e.g. `http://localhost:5080`) → the real backend. `frontend/.env.local` is git-ignored and
  **does not exist on a fresh clone**, so `npm run dev` talks to the mock until you create it with
  `NEXT_PUBLIC_API_BASE_URL=http://localhost:5080`. If data looks "fake" or a backend change has no effect,
  check this first. `:5080` **matches** what `dotnet run` serves — the older `:5081` mismatch this file
  used to warn about is gone; do not "fix" it back. To demo purely against the mock (no backend/DB), run
  `NEXT_PUBLIC_API_BASE_URL= npm run dev`.
- **Two implementations of the same contract.** The real ASP.NET API and the Next.js mock both serve the
  same paths/shapes. When you change an endpoint's contract, keep the mock in sync (or accept that the
  mock is the deletable dev-only path). `frontend/src/types/domain.ts` mirrors the backend DTOs 1:1.
  The mock is **already incomplete**: it has no `admin/categories`, `admin/producers`,
  `admin/orders/geocode-missing`, `admin/orders/{id}/coordinates`, the dispatch routes
  (`admin/orders/{id}/driver`, `admin/orders/assign-bulk`), `account/*` or any `driver/*` route, so those
  screens (Dostava, Moj nalog included) only work against the real backend.
- **Validation error keys are camelCased full property paths** (`Customer.Name` → `customer.name`; a
  nested `Input.Name` → `input.name`). Frontend forms read `err.fieldErrors["input.name"]` etc. to show
  inline errors — match this casing exactly.
- **A guest checkout is two calls, not one.** `POST /orders` is for an authenticated customer only in
  practice — a guest instead calls `POST /orders/guest-request`, which stores a `GuestOrderRequest` with
  prices snapshotted at submit time and a 15-minute `ExpiresAt` (`GuestOrderRequest.TrajanjeMinuta`) and
  creates **no `Order`**. The order is created only when the guest clicks the emailed link, hitting
  `POST /orders/confirm`; that call creates the `Order`, decrements stock, and deletes the request row, all
  in one `SaveChangesAsync`. An expired or already-consumed token there is a 409 `guest_request_expired`,
  not a 404 — the request already existed. `guest-request` itself rejects a logged-in caller with
  `already_authenticated`: an authenticated customer's order must go through `POST /orders` so it shows up
  under `orders/mine`. Every `Order` (guest or account) also carries a permanent `PublicToken`, unrelated to
  the request's 15-minute one, that `GET /orders/track/{token}` resolves with no auth at all.

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

**Enums are stored as strings** (`HasConversion<string>()` for `Role`, `Status`, `Unit`), so raw SQL
compares text: `WHERE "Role" = 'Admin'`, not a number.

**Auth:** JWT bearer, roles `Customer`/`Admin`/`Driver`. Admin endpoints use `[Authorize(Roles="Admin")]`;
the driver app is a first-class consumer with its own endpoints — `GET /driver/route`,
`PATCH /driver/orders/{id}/status` (accepts only `Delivered` or `FailedDelivery`), and
`POST /driver/route/reoptimize`. That last one takes the driver's live position as a parameter, uses it for
one computation, and **never stores or logs it**; see the logging notes under Route optimization. Rate
limiting is global plus a tighter `auth` partition. Swappable services live behind interfaces
(`IEmailService`, `IRouteOptimizer`, `IGeocodingService`, `IViberService`, `ISocialAuthVerifier`).

**Email confirmation is mandatory for password accounts.** `register` does **not** log in — it emails a
link (`ConfirmEmailTemplate`, built from `App:BaseUrl`) and login throws `email_not_confirmed` until
`POST /auth/confirm-email` consumes the token (which then returns a JWT). `resend-confirmation` always
answers success so it never reveals whether an email is registered — keep it that way. Demo accounts are
seeded pre-confirmed; a user you register yourself in dev needs the link from Mailpit. Social login
(`/auth/google`, `/auth/facebook`) goes through `SocialAuthVerifier`: Google only accepts verified emails,
Facebook checks `debug_token` against our app, so it needs both `AppId` and `AppSecret`.

**Order confirmation:** `CreateOrder` sends it by **email when the order has one, otherwise by Viber** to
the (always present) phone; failures never block the order — wrapped in try/catch. Email goes through
`SmtpEmailService` (renders `OrderEmailTemplate`, uses the `Email` section; in dev a local **Mailpit** on
:1025, viewable at http://localhost:8025 — start it with the command above). Viber is a **stub**:
`LoggingViberService` only logs the message; `PRODUCTION.md` describes swapping in a real provider.

**Know what "never blocks" costs before you rely on it:** the failure is swallowed *twice*.
`SmtpEmailService.SendHtmlAsync` catches every transport exception and logs it, and `CreateOrder`'s own
`catch` around the call is **empty — it does not even log**. So a customer's confirmation can vanish
leaving nothing that tells anyone to react, and the Viber fallback does not rescue it: the `if/else` picks
email *or* Viber by whether an address exists, so a failed email never falls through to Viber.
Registration's confirmation mail (`AuthCommands`, `EmailConfirmationCommands`) has the same shape. The
sibling SeloPlatform project replaced exactly this pattern with a durable outbox; if reliable delivery is
ever required here, that is the precedent to follow rather than adding more try/catch. Note also that
`SmtpEmailService` uses `System.Net.Mail.SmtpClient`, which Microsoft no longer recommends (SeloPlatform
uses MailKit).

**One mail in this system is deliberately not best-effort.** Every send above is wrapped in a swallowing
try/catch because the order it's about already exists — losing the email loses a notification, not the
order. `CreateGuestOrderRequestHandler`'s call to `SendGuestOrderRequestAsync` (the "click this to confirm"
link) is the one exception: it's wrapped in a try/catch that does not swallow — the catch compensates by
deleting the just-inserted `GuestOrderRequest` row, then rethrows the original exception (bare `throw;`),
on purpose, because without that email the guest has no way to ever confirm and no `Order` will ever exist.
So the failure still propagates out of the handler, but without leaving litter behind — it fails the
request instead of quietly producing an order nobody can complete. `ExpiredGuestRequestsCleaner` (the project's first
`BackgroundService`, sweeping every 5 minutes) would eventually delete an orphaned request anyway, but
confirmation never depends on it — `ConfirmGuestOrderHandler` checks `GuestOrderRequest.Istekao` itself.

## Two ways to put an order on a route — pick the right one

Both live under `Features/Admin` and both end up writing `RouteStop` rows, but they answer different
questions and are not interchangeable.

- **`AssignRouteCommand`** (`AdminRoutes.cs`, `POST /admin/routes`) is **declarative**: "these are all of
  this driver's orders for this day." It deletes every open stop the driver has that day, rebuilds the
  routes from the request, and runs OSRM for each one. Closed stops (`Delivered`/`FailedDelivery`/
  `Cancelled`) are never touched — history must stay true. Use it for bulk planning; it returns an
  **array**, since one assignment produces one route per zone.
- **`AssignOrderDriverCommand` / `RemoveOrderDriverCommand`** (`AdminDispatch.cs`,
  `POST`/`DELETE /admin/orders/{id}/driver`) are **pointwise**: one order moves, nothing else changes.
  The POST also *is* the transfer — it lifts the order off whatever route holds it that day first.
  Use these for anything driven by a single click.

Do not emulate one with the other. Driving a per-click UI through `AssignRouteCommand` means tearing down
and rebuilding the driver's whole day plus one OSRM call per route on every click, and a transfer becomes
two requests with the order unassigned in between — nothing left to roll back to if the second fails.

**`AdminDispatch` deliberately does not re-optimize.** The route keeps its existing `DuzinaMetara` /
`TrajanjeSekundi` until someone optimizes it. That is honest — those numbers no longer describe the
changed route — and writing new ones without OSRM would mean inventing them.

**Both dispatch handlers load every route for the day in one query, mutate in memory, and save once.**
Do not split that into several queries: an entity marked for deletion is still in the database until
`SaveChanges`, so a re-query pulls it back into the navigation collection and stop counts come out wrong.
This was hit and fixed; the single-query shape is the fix.

Stops go into the route for the order's zone, a new route is created when that zone has none or the
existing one is full at **10** (`MaxStanicaPoRuti`, the Google Maps link limit). Emptied routes are
deleted, open stops are renumbered *above* the closed ones (closed keep their `Sequence`; a collision now
fails on the deferred `UQ_RouteStops_RouteId_Sequence` at `SaveChanges` — see Route optimization), and
route names follow the same convention `AssignRouteHandler` uses (`Centar`, or `Centar 1`/`Centar 2`).

`Driver.Aktivan` gates assignment: an inactive driver takes no new orders. `Driver.MaxStanicaPoDanu` (30)
is a **per-driver daily capacity** and has nothing to do with `MaxStanicaPoRuti` (10) — do not conflate
them. It is a constant surfaced through `DriverDto.MaxStanica` so the frontend does not hardcode it.

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

- **Keep `Microsoft.EntityFrameworkCore.Relational` pinned to the same version as
  `Microsoft.EntityFrameworkCore`** in `EPiljara.Infrastructure.csproj`. Relational also arrives
  transitively through `Npgsql.EntityFrameworkCore.PostgreSQL`, which pulls an *older* one; the code then
  compiles against the newer version while the older one is copied to `bin`. That mismatch is the
  long-standing `MSB3277` warning, and it sleeps until the first Relational-only call in `OnModelCreating`
  (e.g. `HasDefaultValue`) — at which point **every** request dies, `/auth/login` included, with a
  `FileNotFoundException` naming an assembly version, an error that looks nothing like its cause. A build
  with zero warnings is the signal that the versions still line up.
- **The `/trip` parameter combination is load-bearing.** `source=first&destination=last&roundtrip=false`,
  with the warehouse appended as the final coordinate. `roundtrip=false` with only `source=first` returns
  HTTP 400 `NotImplemented` — the code sent exactly that for weeks, every optimization silently fell back
  to zone sorting, and nothing looked broken because the fallback is decent. The comment in
  `OsrmRouteOptimizer.cs` records this; leave it there.
- **OSRM takes `longitude,latitude`** — the reverse of everything else here. And every interpolated number
  uses `InvariantCulture`; a Serbian locale's decimal comma would split the URL at the wrong place.
- **Closed stops keep their `Sequence`; open ones are renumbered above the highest closed one.** A collision
  used to corrupt silently (and break the driver app's Google Maps batching downstream); `RouteStop(RouteId,
  Sequence)` now carries `UQ_RouteStops_RouteId_Sequence`, **`DEFERRABLE INITIALLY DEFERRED`**. Deferred is
  load-bearing: renumbering permutes one range of numbers and EF writes it as one `UPDATE` per row, so a
  per-statement check would reject every legitimate re-optimization. It lives as raw SQL in the
  `RouteStopSequenceUnique` migration and deliberately **not** in the EF model — the Npgsql fluent API
  cannot express `DEFERRABLE`. Do not "fix" that with `HasIndex(...).IsUnique()`.
- **`Status` becomes `Optimized` only when the result carries a distance.** The fallback returns an order
  but no distance, and writing `Optimized` anyway would leave nothing able to tell a real optimization from
  a degraded one. `DuzinaMetara` / `TrajanjeSekundi` / `Geometrija` on `DeliveryRoute` are nullable for the
  same reason — `null` means "not computed", and `0` would render in the driver's app as "0 km · 0 min".
- **`Magacin` lives in the database, not in configuration** — a one-row `Magacin` table (entity
  `Domain/Entities/Magacin.cs`), so the location changes without a rebuild: admins edit it at
  `/admin/settings` through `GET`/`PUT /admin/warehouse`. `CK_Magacin_JedanRed` allows only
  `Magacin.JediniId`, so "one piljara, one warehouse" is enforced by the DB. The row is inserted by the
  **`MagacinUBazi` migration, not `DbSeeder`** — the seeder bails on any existing category and only runs in
  Development or under `Seed:Demo=true`, so real production would otherwise come up with no warehouse.
  `Program.cs` re-checks `GeoBounds` after migrating and **refuses to start** on a missing row or a point
  outside Bosnia and Herzegovina (the old `ValidateOnStart` guard, moved); it still cannot catch a *wrong*
  point inside the country. An unreachable database is still only logged, as before.

### Geocoding

**All geocoding goes through our own API and only through it**: `GET /geocode?q=` and
`GET /geocode/reverse?lat=&lon=` (`GeocodingController` → `Features/Geocoding`), backed by
`IGeocodingService`. The browser must never call Nominatim itself — `address-picker.tsx` used to, which
meant every keystroke of every customer's address went to a third party from that customer's own IP, and
the admin's *Nova narudžba* screen sent someone else's address the same way. It now calls `geocodeApi`;
do not put a `fetch` to `nominatim.openstreetmap.org` back into any client component.

The endpoints are **anonymous** — guests check out without an account, so they must be — and protected by
the `geocode` rate-limit partition (30/min per IP) so they cannot become an open relay to Nominatim.
A miss returns `200` with no `data`; because responses serialize with `WhenWritingNull`, that arrives as
`{}`, which is why `geocodeApi` coerces with `?? null`.

**The upstream is a config choice, not a code path.** `Nominatim:BaseUrl` (same pattern as
`Osrm:BaseUrl`) plus `Nominatim:ThrottleMs` are the whole switch:

- **Default: the public Nominatim**, with `ThrottleMs: 1000`. Addresses do leave our infrastructure —
  one boundary now instead of one per browser, but the same third party. The 1000 ms is **their usage
  policy, not a tuning knob** (violating it gets the IP blocked), and a static semaphore in
  `NominatimGeocodingService` enforces it, so *all* traffic — background batches and every browser
  search — queues behind one request per second. Address search gets slower as users overlap.
- **Self-hosted:** `docker compose up -d nominatim`, then `BaseUrl` → `http://localhost:8080/` (or
  `http://nominatim:8080/` inside compose, already set for the `api` service) and `ThrottleMs` → `0`.
  Addresses stop leaving, and the rate limit goes with them. First start imports the map and takes tens
  of minutes; `backend/nominatim/README.md` has the disk/memory requirements and the switch-over.
  `nominatim` is deliberately **not** in the `api` service's `depends_on` — a failed geocode returns
  `null` and the screens handle it, so the API must not wait on a tens-of-minutes import.

What stays with a third party either way: map **tiles** load from `tile.openstreetmap.org` in the
browser. That reveals roughly which area is being viewed, not the address string, and it is inherent to
using OSM tiles.

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
  **OpenStreetMap + Leaflet** map picker (`components/shop/address-picker.tsx`): type → `geocodeApi`
  (our `/geocode`, see Geocoding) → draggable pin → reverse-geocode via `/geocode/reverse`. No Google Maps key. **Coordinates are persisted** alongside the address
  (`Order.Latitude`/`Longitude`, both nullable) and drive route optimization — an earlier version of this
  file said only the address string was kept, which stopped being true when the driver app gained a map.
  Nominatim does not resolve every real address, so `null` coordinates are normal and must stay nullable
  end to end; `0,0` is in the Atlantic and this project has shipped that bug once already. Orders left
  without coordinates are fixed by admins via `POST /admin/orders/geocode-missing` (batch retry) and
  `PUT /admin/orders/{id}/coordinates` (manual pin).
- **Order management (admin):** the orders list uses expandable cards (`components/admin/order-card.tsx`) with a
  status dropdown of valid state-machine transitions. Admins create orders via the same public create-order
  endpoint (`orderApi.create`) the customer checkout uses.
- **Dispatch lives on `/admin/dostava`** ("Dostava" in the nav): a tab per active driver plus a
  "Nedodijeljeno" tab, assignment through the pointwise endpoints above, optimistic updates that snapshot
  both lists and restore them on failure. The active tab is kept in the URL (`?vozac=`) so a reload does
  not lose it. `components/admin/dispatch-tabs.tsx` is a hand-rolled tab strip — the project has no tab
  component and this did not justify a dependency.
- **`/admin/routes` is still there, out of the nav**, reachable from a button on the dispatch screen. It
  owns route optimization, `geocode-missing`, and the manual stop-location drawer — none of which the
  dispatch screen replaces. **Its own assignable list still filters on `ASSIGNABLE` and omits `Pending`**,
  which is the bug fixed on the dispatch screen; if you touch that page, fix it there too.
- **Unassigned orders come from `GET /admin/orders?unassigned=true&date=`, and that flag changes what
  `date` means** — with it, `date` is the *delivery* day (is this order on any route that day), without it
  it is the order's creation day. An order placed yesterday is still driven today, so filtering the
  dispatch list by `CreatedAt` would hide it. The unassigned set deliberately includes `Pending`: the shop
  creates every order as `Pending`, and omitting it made new orders invisible to the dispatcher.
- **Admin data mutations** follow a consistent pattern: a TanStack `useMutation` calling `adminApi.*`,
  then `queryClient.invalidateQueries` for the affected keys and a toast. Admin CRUD forms are drawer
  components in `src/components/admin/*-form.tsx`.
- **Visual design system (Wolt-clean).** Design tokens live in `src/app/globals.css` (`@theme`): white
  page bg, cool neutrals (`sand`/`ink`/`muted`/`line`), a **green** brand accent, soft
  `shadow-card`/`shadow-md`/`shadow-lg` and `border-line` hairlines. **Honey is label-only** (badges like
  *Izdvojeno*) — never a CTA; CTAs are green `primary`, or white `secondary` on a colored hero. Prefer
  `shadow-card` / `border border-line` over `ring-1 ring-black/5`, and `font-bold` over `font-extrabold`
  (except the logo wordmark). Animations use the **`motion`** package (Framer Motion for React 19,
  `import { motion } from "motion/react"`) wrapped globally by `<MotionConfig reducedMotion="user">` in
  `providers.tsx`; reusable `components/ui/reveal.tsx` (scroll fade-in) and `lib/cart/fly-to-cart.ts`
  (add-to-cart dot + badge bump). Everything must honor `prefers-reduced-motion`.
- **Catalog imagery is self-hosted, not remote.** Product/category/producer photos live in
  `public/products/<slug>.jpg`, `public/categories/<slug>.jpg`, `public/producers/<name>.jpg`, and
  `DbSeeder` seeds those root-relative paths (`/products/<slug>.jpg`, …). `SmartImage`
  (`components/ui/smart-image.tsx`) renders them via `next/image` (`unoptimized`) and falls back to a leaf
  placeholder on a missing/failed load — so a wrong path degrades gracefully rather than breaking. Any
  *remote* image host must be whitelisted in `next.config.ts` `images.remotePatterns`.

@frontend/AGENTS.md
