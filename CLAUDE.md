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
dotnet run --project src/EPiljara.API      # http://localhost:5080, Swagger at /swagger
docker compose up -d                       # or run DB + API together in containers (API on :5080)
```
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
the driver app is a first-class consumer with its own `/driver/*` endpoints. Rate limiting is global plus a
tighter `auth` partition. Swappable services live behind interfaces (`IEmailService`, `IRouteOptimizer`).

**Order confirmation email:** `CreateOrder` calls `IEmailService.SendOrderConfirmationAsync` (failures never
block the order — wrapped in try/catch). The impl is `SmtpEmailService`, which renders `OrderEmailTemplate`
(self-contained HTML) and sends over SMTP using the `Email` section in `appsettings.json`. In dev it targets a
local **Mailpit** catcher — sent mail is viewable at http://localhost:8025 (no real mailbox needed). Only sent
when the order has a customer email; guests without one are skipped.

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
  draggable pin → reverse-geocode. No Google Maps key. Only the address string is persisted (no lat/lng).
- **Order management (admin):** the orders list uses expandable cards (`components/admin/order-card.tsx`) with a
  status dropdown of valid state-machine transitions. Admins create orders via the same public create-order
  endpoint (`orderApi.create`) the customer checkout uses.
- **Admin data mutations** follow a consistent pattern: a TanStack `useMutation` calling `adminApi.*`,
  then `queryClient.invalidateQueries` for the affected keys and a toast. Admin CRUD forms are drawer
  components in `src/components/admin/*-form.tsx`.

@frontend/AGENTS.md
