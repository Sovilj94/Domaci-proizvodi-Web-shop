# E piljara — Web shop (Phase 1)

Customer web shop + admin panel for **E piljara**, a domestic-food e-commerce platform
delivering products from Manjača to families in Banja Luka.

This is the **Next.js 15** application (Phase 1 of a three-app platform: this web app,
an ASP.NET Core 9 API, and a React Native driver app). It ships with a built-in mock API
so it runs standalone today, and is wired to swap onto the real backend by changing one
environment variable.

## Tech

- **Next.js 15** (App Router) · TypeScript · **Tailwind CSS v4**
- **TanStack Query** for server state
- Cart in `localStorage`; mock JWT auth
- Two route trees in one app:
  - `(shop)` — customer-facing shop at `/`
  - `admin` — internal, auth-gated panel at `/admin`

## Getting started

```bash
npm install
npm run dev          # http://localhost:3000
```

No backend needed — mock API route handlers under `src/app/api/**` serve seeded data.

### Demo accounts

| Role  | Email             | Password |
|-------|-------------------|----------|
| Admin | admin@epiljara.ba | admin123 |
| Kupac | mujo@example.com  | mujo123  |

## Project structure

```
src/
  app/
    (shop)/            # customer shop: home, products, cart, checkout, orders, auth
    admin/             # admin panel: dashboard, orders, products, routes
    api/               # MOCK backend (route handlers) — delete when the real API is live
    layout.tsx         # root: fonts + providers
    providers.tsx      # QueryClient + Auth + Cart + Toast
  components/
    ui/                # button, field, badges, toast, spinner, smart-image…
    shop/              # product card/grid, category chips, cart button, address input
    admin/             # sidebar, guard, stat card, product form
    layout/            # logo, shop navbar/footer
  lib/
    api/               # client (envelope), typed endpoints, contracts, query hooks
    auth/ cart/        # React context providers
    orders.ts utils.ts # status metadata, formatting
  server/mock/         # in-memory DB + seed powering the mock API
  types/domain.ts      # domain contracts shared with the backend
```

## API contract

Every endpoint returns the same envelope:

```jsonc
{ "data": <T> | null, "error": { "code", "message", "details?" } | null, "meta?": { } }
```

`src/lib/api/endpoints.ts` is the single, typed API surface every screen calls. It targets
`NEXT_PUBLIC_API_BASE_URL` (default `/api`).

## Going live against the real backend

1. Implement the same REST resources in the ASP.NET Core API (same paths + envelope).
2. Set `NEXT_PUBLIC_API_BASE_URL=https://api.epiljara.ba`.
3. Delete `src/app/api` and `src/server/mock`.

Nothing else changes — screens depend only on `endpoints.ts` and `types/domain.ts`.

## Environment

See `.env.example`. Optional `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY` enables Google Places
autocomplete on the checkout address field (degrades gracefully without it).
