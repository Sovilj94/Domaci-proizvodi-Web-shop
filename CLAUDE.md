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
`FakeCurrentUser`, `FakeNotifications`) nad SQLite in-memory bazom sa stvarno enforsovanim stranim
ključevima, i handler testovi: `AddressCommandsTests`, `DriverCommandsTests`, `GuestOrderTests`,
`OrderTrackingTests`.
**`TestDb` holds one long-lived `DbContext` shared between a test's setup and the handler call it
exercises**, so EF relationship fixup populates navigation properties from entities already tracked in
setup **regardless of whether the handler's own query includes them**. A test asserting on an
`Include`-populated field is a false green unless it calls `t.Db.ChangeTracker.Clear()` between setup and
the call — see `AdminKorpiceTests`, `ObjavaSadrzajaTests`, `KorpiceQueriesTests` for the pattern, each with
a comment explaining it caused a real false pass in the korpice plan. Don't add `Clear()` reflexively to
every test, though — one asserting only scalar columns gets nothing from it.

**Frontend nema test setup** — provjerava se sa `npx tsc --noEmit`, `npm run lint`, **`npm run build`**
i prolaskom kroz tok uz pokrenut stek. `npm run build` nije opcion: `tsc` i `eslint` ne vide greške koje
nastaju tek u Next-ovom prerender prolazu. Konkretno, `useSearchParams()` bez `<Suspense>` omotača obara
build (`/login`, `/register` i `/admin/dostava` su zato izvučene u internu komponentu uvijenu u
`<Suspense fallback={<LoadingBlock />}>`) — a oba prethodna alata su prolazila čisto. Zeleni `tsc` +
`lint` nisu dokaz da se frontend može isporučiti.

Demo accounts: `admin@epiljara.ba` / `admin123` · `mujo@example.com` / `mujo123` · `vozac@epiljara.ba` / `vozac123`.

## The contract is the architecture

Everything hinges on one shared HTTP contract, so understand this before touching either side:

- **Response envelope.** *Every* endpoint (success and error) returns `{ data, error, meta }`. The
  backend produces it (`ApiControllerBase` helpers + `ExceptionHandlingMiddleware`); the frontend
  unwraps it (`apiFetch` returns `data` or throws `ApiRequestError`).
- **The API omits null properties entirely rather than serializing them as `null`**
  (`JsonSerializerOptions.DefaultIgnoreCondition = WhenWritingNull` in `Program.cs`, global, not just the
  geocoding `{}` case below). A nullable backend field is therefore *absent* on the wire, and TypeScript
  types must declare it `?:`, never `| null` — see `Korpica.imageUrl?:` / `StavkaSadrzaja.imageUrl?:` in
  `frontend/src/types/domain.ts`. A type or a check written as `=== null` for such a field never fires.
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
  mock is the deletable dev-only path). `frontend/src/types/domain.ts` mirrors the backend DTOs 1:1;
  payloads that are not domain entities (`CreateOrderRequest`, `RegisterRequest`, …) live beside it in
  `frontend/src/lib/api/contracts.ts` — put a new request/response shape there, not in `domain.ts`.
  The mock is **already incomplete**: it has no `admin/categories`, `admin/producers`,
  `admin/orders/geocode-missing`, `admin/orders/{id}/coordinates`, the dispatch routes
  (`admin/orders/{id}/driver`, `admin/orders/assign-bulk`), none of the guest-checkout routes
  (`orders/guest-request`, `orders/confirm`, `orders/track/{token}`), `account/*` or any `driver/*` route;
  `admin/drivers` exists as **GET only**, so the Vozači screen reads but cannot write against it. Dostava,
  Vozači, Kategorije, Proizvođači and the whole guest-checkout flow therefore only work against the real
  backend. The gap widens with every feature — treat the mock as a catalog/login demo, not as a second
  runtime you can develop a delivery feature on.
- **Validation error keys are camelCased full property paths** (`Customer.Name` → `customer.name`; a
  nested `Input.Name` → `input.name`). Frontend forms read `err.fieldErrors["input.name"]` etc. to show
  inline errors — match this casing exactly.
- **A guest checkout is two calls, not one.** `POST /orders` is `[Authorize]`-only — an anonymous caller
  is rejected outright, not merely discouraged, because the whole confirmation-email mechanism below would
  otherwise be bypassable with one POST. A guest instead calls `POST /orders/guest-request`, which stores a
  `GuestOrderRequest` with
  prices snapshotted at submit time and a 15-minute `ExpiresAt` (`GuestOrderRequest.TrajanjeMinuta`) and
  creates **no `Order`**. The order is created only when the guest clicks the emailed link, hitting
  `POST /orders/confirm`; that call creates the `Order`, decrements stock, and deletes the request row, all
  in one `SaveChangesAsync`. An expired or already-consumed token there is a 409 `guest_request_expired`,
  not a 404 — the request already existed. `guest-request` itself rejects a logged-in caller with
  `already_authenticated`: an authenticated customer's order must go through `POST /orders` so it shows up
  under `orders/mine`. Every `Order` (guest or account) also carries a permanent `PublicToken`, unrelated to
  the request's 15-minute one, that `GET /orders/track/{token}` resolves with no auth at all.
- **There are two unrelated "address lists" and mixing them up is easy.** `GET /orders/addresses`
  (`GetMyAddressesQuery`) returns **plain strings** — the addresses of the caller's *delivered* orders,
  de-duplicated, most recent first. That is what checkout shows: `AddressChooser` lists them and falls back
  to the map picker for a guest or a customer with no history. `GET|POST|PUT|DELETE /account/addresses`
  (`Features/Account`) is the real **Adresar**: an `Address` entity per row, with label, pin, zone and a
  default flag. Both are `[Authorize]`, both are about "where this customer lives", and they share nothing.
  A saved checkout string carries **no coordinates** — `AddressChooser` explicitly pushes `null` upward when
  one is picked, so the order is geocoded later rather than inheriting some other address's pin.
- **The Adresar's only frontend is `/moj-nalog/adrese`,** added in P2a — before that the backend had been
  complete and unreachable from any screen since September. `accountApi` in `endpoints.ts` is its single
  surface; nothing else under `frontend/src/` calls `/account/addresses`. The screen **reflects** the three
  backend rules rather than re-implementing them: at ten addresses the add tile is disabled with an
  explanation, the default is marked rather than chosen client-side, and a saved address whose zone was later
  switched off carries a visible warning because checkout will reject it. Client-side copies of those rules
  would drift from the server's, and the server is the one that decides.
- **The Adresar's form (`components/nalog/adresa-forma.tsx`) is the map picker, not a text field** — since
  P2c it embeds the same `AddressPicker` checkout uses, so a saved address carries a pin and the route
  optimizer stops falling back to zone order for it. The pin stays **optional**: Nominatim misses real
  addresses, and the card shows a muted "bez tačke na mapi" line rather than a warning. Two consequences
  worth keeping: the panel's body is a separate component mounted with a `key` (state seeded in `useState`,
  no reset effect) because the picker reads `value`/`initialCoordinates` **on mount** — seeding it through an
  effect made the picker see an empty field first and then treat the real address as the customer typing,
  dropping the pin; and the zone hint comes from `lib/zone.ts` (`pogodiZonu`), the same helper checkout
  calls, so the neighbourhood→zone guess exists once.
- **The backend owns which address is the default, in both directions.** The first address on an empty
  account comes back `isDefault: true`; setting another as default flips the previous one off; and deleting
  the default **promotes another address automatically**. UI code that picks a replacement after a delete is
  fighting the server, not helping it. The default also always sorts first in `GET /account/addresses`.

## Moj nalog i korpice kupca (P2a, P2b)

- **`/moj-nalog` is a nested route whose guard lives in `layout.tsx`, once.** The sections do not repeat it;
  the shape follows `app/admin/layout.tsx`. The guard shows an invitation to log in rather than redirecting,
  because a redirect throws away where the customer was headed. It is deliberately **not** `?sekcija=`:
  `useSearchParams()` without a `<Suspense>` boundary passes `tsc` and `eslint` cleanly and then breaks
  `npm run build` in the prerender pass — which happened twice in P1.
- **`/moj-nalog` is a literal prefix of every section route,** so the admin panel's active-state helper
  (`pathname === href || pathname.startsWith(href + "/")`) would light up "Pregled" on every subpage
  alongside the real item. `nalog-nav.tsx` exempts the root href from the prefix branch.
- **`PUT /account/profile` changes name and phone only** and returns the same `AuthUserDto` that `auth/me`
  returns, so the client refreshes its context from the response instead of making a second call. E-mail,
  password and account deletion are deliberately outside it — each is its own piece of work, and the P2a spec
  says why.
- **`useAuth().azurirajProfil` is the only path that changes `user` in the context outside login.** It calls
  the endpoint and then `setUser` with the server's echo; the navbar's name and any "is this form dirty"
  comparison depend on that. A second writer added later needs to go through it too.
- **The orders screen lives at `/moj-nalog/narudzbe`; `/orders` is a server-side `redirect()`** kept only so
  old links and bookmarks survive. Internal links were repointed by meaning, not mechanically: the navbar's
  user chip and the mobile "Profil" tab mean *my account* and go to `/moj-nalog`, while the footer, the mobile
  menu item, the post-order button and the post-login destinations mean *my orders* and go to
  `/moj-nalog/narudzbe`.
- **`DELETE /account/addresses/{id}` is the only endpoint in the API that returns a bodiless `204`.**
  `apiFetch` returns early on that status; without it `res.json()` throws on the empty body and a *successful*
  delete surfaces as `ApiRequestError(204)`.

### P2b — the box a customer already bought

- **`Order.Godina` / `Order.Sedmica` are the ISO year and week of the DELIVERY date,** not of the order date,
  and come from `SedmicaHelper.SedmicaZaNarudzbu` — the first week whose ordering deadline has not passed.
  Before the deadline that is this week; after it, the next. Orders from before P2b were backfilled from
  `CreatedAt`, which is **approximate** for them and exact for everything since; none of them carries korpica
  content, so the approximation hangs nothing.
- **Three places construct an `Order`, and anything that must hold for every order has to be added to all
  of them.** `CreateOrder.cs` (a logged-in or guest checkout through the normal flow),
  `GuestOrderRequests.cs` (a guest request confirmed later, from its own 15-minute token) and
  `DbSeeder.cs` (demo data). P2b was written against the first alone, which left guests' korpice with no
  week and no content snapshot — invisible to every packing list, which is the exact bug P2b exists to fix.
  The shared part now lives in `Features/Korpice/SnapshotKorpice.cs` and both real sites call it; the
  seeder never orders a korpica, so it stays out. **Before adding an order-level invariant, run
  `grep -rn "new Order {" backend/src --include=*.cs` and decide about each hit explicitly.**
- **`StavkaKorpiceNarudzbe` is not `StavkaSadrzaja`, and confusing them is the easiest mistake here.**
  `StavkaSadrzaja` is the weekly template, shared by every customer. `StavkaKorpiceNarudzbe` belongs to one
  `OrderItem` and the customer may edit it. The content is **copied** at order time, never referenced — an
  admin editing the template afterwards must not change what somebody already bought. It hangs off
  `OrderItem` rather than `Order` because one order can carry two different korpice.
- **A korpica with no published content for its week cannot be ordered** (`korpica_bez_sadrzaja`). A korpica
  is just a `Product` and checkout checks no deadline, so before P2b one could be bought for a week whose
  content was already locked and whose packing list was already printed. This guard is the only thing
  stopping that; the public `/korpice` page still talks about the *current* calendar week and does not know
  about this rule.
- **The packing list sums orders, not the template.** An empty list means zero orders, and the screen says
  the number out loud for that reason. Before P2b it summed `StavkeSadrzaja`, so forty orders and one gave
  identical quantities and procurement bought for a single box.
- **`KorpicaPodobnost` is the single definition of "may go into a korpica".** The generator narrows it to the
  korpica's own categories; the customer's editor does not, because P2b's spec lets the customer pick from
  the whole seasonal catalogue. Its split into `UpitPodobnih` (SQL) and `USezoni` (in memory) is not
  stylistic: `Product.Mjeseci` is `int[]` in C# but CSV in the database, so the season filter cannot be
  translated and must run on an already-materialised list.
- **`PUT /account/korpice/{orderItemId}` accepts only `productIds`.** The server decides every quantity — an
  item in that week's template keeps the template's quantity, anything else gets 1 in its own unit — and
  re-reads prices itself. Accepting quantities would let a client write itself ten kilos at the price of one.
- **`CiljnaVrijednost` is frozen at publish; `TrenutnaVrijednost` is summed from the order's rows.** They are
  equal the moment the content is copied and diverge the moment the customer edits, which is the entire
  budget rule (95–100%). Computing both from the same source makes the ratio permanently 100% and the rule
  decorative — and no fixture built from ordinary data will catch it, because at snapshot time they agree.
- **Rule order in the editor is load-bearing:** owner → order status → duplicates → deadline → eligibility →
  budget → item count. Budget before count is why removing one item from a full box reports `budzet_premali`
  rather than `premalo_artikala`; the screen's helper text has to agree with that or it contradicts the
  server.

## Pretplate (P3)

- **There is one path to an `Order`, and the subscription job walks it.** `PretplatneNarudzbe` sends a
  `CreateOrderCommand` through MediatR instead of building an `Order` itself, which is why a subscription
  order gets the basket snapshot, the ISO week, the stock decrement and the confirmation mail for free. The
  command therefore carries `CustomerId` and `PretplataId`, **neither of which is a wire input** — no
  controller fills them, because anyone could then order in someone else's name. They exist for the one
  caller with no `ICurrentUser`: the background job. Before touching anything order-shaped, run
  `grep -rn "new Order" backend/src --include=*.cs` and decide about each hit; P2b learned that the hard way.
- **Idempotency is two checks plus one transaction.** `Pretplata.Poslednja{Godina,Sedmica}` is the cheap
  filter, an `Orders.AnyAsync` on `(PretplataId, Godina, Sedmica)` is the truth that survives a lost write,
  and the order plus that marker are written inside **one** transaction. Split them and a crash in between
  hands the customer two boxes and two cash-on-delivery bills next pass. A pass that finds an order whose
  marker is missing repairs the marker instead of ordering again.
- **The job must survive one broken row.** Every subscription is processed in its own `try`, and the catch
  **reloads the entity** — a rolled-back transaction leaves the in-memory `Poslednja*` edits in the context,
  and the next subscription's `SaveChanges` would flush them, permanently skipping that week for a customer
  who never got an order.
- **`BrojIsporuka` goes up in exactly one place.** `OrderStatus.Delivered` is set from both `AdminOrders` and
  `DriverFeature`, so `BrojacIsporuka.ZabiljeziAsync` is called from both, **before** the transition (it
  reads the old status so a second "Delivered" cannot count twice). The discount is earned by *deliveries*,
  never by orders, so nothing increments when the order is created.
- **The discount is earned backwards and lives in `PretplatnaCijena`.** `PragPopusta` counts deliveries
  already made, so with the default 5 the sixth delivery is the first cheaper one. Payment is cash on
  delivery, so a discount given up front would be a gift to whoever quits after two boxes. The rule is
  server-side only: `KorpicaDto.CijenaSaPretplatom` exists precisely so the shop page does not multiply by
  0.9 itself and drift away from what is actually charged.
- **A subscription that could not pass checkout is rejected when it is created**, not quietly skipped every
  week — zone minimum above the basket price, inactive zone, a product that is not a basket. The unique
  index `(UserId, ProductId)` is **filtered on non-cancelled**, so a customer can come back to a package
  they once cancelled.
- **Content missing means no delivery, by choice.** If the week's `SedmicniSadrzaj` is not `Objavljen` or
  `Zakljucan`, the job skips that week and logs it; an order with no content is a box nobody can pack.
  `GET /admin/pretplate` surfaces exactly that: how many baskets are promised for the next week and how many
  of them are still waiting for content.

## Početna (P4)

- **The home page sells the korpica and types no numbers of its own.** The deadline, the delivery day and
  the subscription discount in the hero, the FAQ and the footer all come from the server:
  `/korpice/sedmica` for the first two, `KorpicaDto.CijenaSaPretplatom`/`IsporukaDoPopusta` for the third.
  The footer used to say "Narudžbe do utorka u 12h" as a literal that matched nothing in `DeliverySlot`;
  a new sentence that needs a number reads it through `lib/cesta-pitanja.ts` (`kontekstPitanja`) or
  `lib/sedmica.ts`, never from a string.
- **After the deadline the home page talks about next week, `/korpice` still about this one.**
  `SedmicaDto` carries `SljedeciRok`/`SljedecaIsporuka`, and `aktivniKrug()` in `lib/sedmica.ts` picks the
  round checkout actually sells (`SedmicaZaNarudzbu`). The `/korpice` showcase and its deadline bar were
  deliberately left on the current week (P4 spec, O7) — that inconsistency is a known open finding, not a
  regression.
- **Day names live in `lib/sedmica.ts`** — accusative after "u", genitive after "do", indexed by
  `Date.getDay()`. `rokTekst` prints minutes when the deadline is not on the hour; "u 16h" for a 16:30
  deadline was a lie the old banner told.
- **Testimonials (`Utisak`) are entered by an admin at `/admin/utisci`, and none are seeded — not even in
  the demo seed.** A made-up testimonial shown as real on a `Seed:Demo=true` deploy is worse than no
  section; `Utisci` renders nothing (not even the section frame) until one is published. `GET /utisci`
  returns only `Objavljen`, ordered by `Redoslijed` then `Kreiran` (`PoRedu`, shared with the admin list).
- **`ProducerDto.Artikli`** is filled only by `GetProducersHandler` (one query, available ordinary products,
  four per producer by name); every other `ToDto()` caller gets `[]`.
- **The countdown reads the clock only in an effect.** The home page is a client component but Next still
  prerenders it, so `Date.now()` in render is a hydration mismatch. At zero it invalidates
  `["korpice","sedmica"]` and the server decides the round has moved on.

## Sadržaj (P5)

- **Lab analyses (`Analiza`) are never seeded, not even in the demo seed.** A made-up lab result is a lie
  about food. A non-compliant result (`NijeUskladjeno`) is still published when `Objavljena`; whether to
  publish is the admin's call, never a filter in the query. A producer with analyses cannot be deleted
  (`producer_in_use`, 409; FK is `Restrict`).
- **Recipes (`Recept` + `ReceptSastojak`) are seeded only in the demo seed** (`DbSeeder.SeedReceptiAsync`,
  its own idempotent guard so it also runs on an already-seeded DB). A recipe's `Slug` is made once from the
  title and **never changes** on edit, so old links keep working.
- **An ingredient offers a product only if it is an ordinary, non-deleted `Product`**
  (`ReceptPravila.SmijeUPrikaz`) — a korpica in a recipe would bypass `/korpice` and its deadline. "Dodaj
  sastojke u korpu" adds only available linked products, one unit each.
- **Updating a recipe or analysis accepts product ids that are already linked even if the product has since
  been soft-deleted**; only *new* ids must exist. Without that, one deleted product locked every recipe that
  used it.
- **`POST /partneri/prijave` is the only unauthenticated write besides checkout.** It sits behind the `auth`
  rate-limit policy and has a honeypot field `web`: when filled, the handler returns the same
  `{ primljena: true }` as a real submission and writes nothing. The validator never mentions `web`.
- **Enums cross the wire as PascalCase strings** (`ToString()` out; `string` in, validated with
  `Enum.TryParse(…, ignoreCase: true)` **and** `Enum.IsDefined` — `TryParse` alone accepts `"99"`).
- **Every new command validator starts with `RuleFor(x => x.Input).NotNull()`.** With
  `SuppressModelStateInvalidFilter`, a body that fails JSON binding (e.g. `""` for a `DateOnly`) arrives as a
  null `Input`, `SetValidator` skips it, and the handler would throw a 500.
- **Admin-entered URLs:** `Analiza.PdfUrl` is rendered as a link only for `http(s)://` or a site path; images
  go through `SmartImage`. There is no upload anywhere — admins paste URLs.
- **Contact data lives in `frontend/src/lib/kontakt.ts`** (footer and `/o-nama#kontakt`). Public copy on the
  P5 pages states only what the app actually does; check a new sentence against the code before adding it.

## Ocjena isporuke (P6a)

- **`OrderDto.mozeSeOcijeniti` and `ocjena` come from the `Order.Ocjena` navigation.** The three customer
  queries (`GetMyOrders`, `GetOrderById`, `TrackOrder`) `Include` it, each with its own test. A new
  customer-facing order query that forgets the `Include` would offer a second rating for a rated order
  (the server still refuses it with 409, but the screen lies). Admin/driver paths don't include it and
  don't use the field.
- **One final rating per order, only when `Delivered`.** The owner rates via `POST /orders/{id}/ocjena`
  (a foreign order is a 404, never a 403); a guest via `POST /orders/track/{token}/ocjena` (rate-limited).
  The unique index on `OrderId` is the race guard; a `DbUpdateException` is translated to 409 only after
  re-checking that the row really exists — anything else is rethrown, not disguised as "already rated".
- **A rating becomes a testimonial only with explicit consent and a comment of at least 10 characters**,
  and only through the admin's „Napravi utisak". The `Utisak` is created **unpublished**, signed
  „Ime P." (first word + initial of the last word) with the delivery zone as place; a full surname is never
  written. `Utisak.OcjenaId` (filtered unique, `SetNull`) links them; deleting the testimonial frees the
  rating.

## Promo kodovi (P6b)

- **All promo rules live in `PromoKodPravila`** (`Features/PromoKodovi`): `Normalizuj`, `Popust`,
  `ProvjeriAsync`. The checkout preview (`POST /promo-kodovi/provjera`), `CreateOrderHandler`,
  `CreateGuestOrderRequestHandler` and `ConfirmGuestOrderHandler` all call it — never re-implement a rule.
  Failures are `DomainException(..., "promo_kod_nevazeci")`; the guest confirm turns one into a 409 and
  deletes the request.
- **Money:** the discount applies to goods only and never exceeds `Subtotal`; the zone minimum and the code
  minimum compare against `Subtotal` *before* the discount; `TotalAmount = Subtotal − Popust + DeliveryFee`.
  `Order`/`GuestOrderRequest` snapshot `PromoKodId`, `PromoKod` (text) and `Popust`. Any new screen that
  shows an order's breakdown must show the `Popust` row.
- **Cancelled orders never consume a code** (limit, once-per-customer, new-customers-only), but **any**
  order — cancelled included — blocks deleting the code (`Order.PromoKodId` FK is `Restrict`; the DTO's
  `mozeSeObrisati` mirrors that). The code text is immutable after creation.
- **Dates** (`VaziOd`/`VaziDo`, `DateOnly`, inclusive) compare against `LokalniDatum.Danas()` (Sarajevo).
- **`POST /orders` binds `CreateOrderRequest`, not `CreateOrderCommand`.** The command carries
  `CustomerId`/`PretplataId` for the subscription job only; binding it from the body let a logged-in user
  order on someone else's account or at subscription prices. Never bind a command that has server-only
  fields straight from the wire.
- The checkout asks the server for the discount (query only on step 3, `staleTime` 60 s). The preview sits
  under the shared `"auth"` rate limit, so never fire it per keystroke.

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
`MediatR.Unit` and `EPiljara.Domain.Enums.Unit` (the product measurement unit) collide — CS0104 — in any
file that `using`s both namespaces, which any handler touching `Product.Unit` will. Qualify the enum fully
(`EPiljara.Domain.Enums.Unit.Kom`, as `GenerisiSedmicniSadrzajHandler` does) rather than aliasing or
dropping the MediatR import.

**Error handling:** throw domain exceptions; the middleware maps them to status + envelope
(`DomainException`→400, `NotFoundException`→404, `ForbiddenException`→403, `ConflictException`→409,
`UnauthorizedException`→401). Don't hand-build error responses in controllers. Give the exception a
specific `code` as its second constructor argument — `ExceptionHandlingMiddleware` sends `ex.Code` to the
client as the envelope's error code, so one thrown with only a message reaches callers as the generic
`domain_error`/`conflict` fallback instead of something a frontend `catch` can branch on.

**Domain invariants worth preserving:**
- Order status is a **state machine** (`Order.Transitions` / `CanTransitionTo`); illegal transitions throw
  `invalid_transition`. Don't set `Status` directly to bypass it.
- `OrderItem` **snapshots** name/unit/price at order time — never recompute from the live product.
- `Product` uses **soft delete** (`IsDeleted` + global query filter) so order history stays intact.
  Deleting a product is soft. Deleting a Category/Producer is a *hard* delete and is blocked with 409 if
  any product (including soft-deleted ones — check via `IgnoreQueryFilters()`) still references it.
- A **`Driver` is a dispatch record, not an account.** `CreateDriverHandler` leaves `UserId` null on
  purpose — the row exists so the dispatcher can plan a day; the login for the Android app is attached
  separately. A driver who has ever driven cannot be deleted: `DeleteDriverHandler` throws 409
  `driver_in_use` when any `DeliveryRoute` points at them, because routes are the day's history. Retiring
  is `Aktivan = false`, which also stops new assignments (see `MaxStanicaPoDanu` below).
- **At most one default address per user, enforced by the database.** `IX_Addresses_UserId_IsDefault` is
  unique on `UserId` with `HasFilter("\"IsDefault\"")`; the filter is load-bearing — without it the index
  would demand a unique `UserId` across *all* rows and cap every customer at one address. Because Postgres
  checks it **per statement**, `SetDefaultAddressHandler` and `DeleteAddressHandler` each run **two
  `SaveChangesAsync` calls inside one explicit transaction** (clear the old flag / delete the row first,
  set the new flag second). One `SaveChanges` lets EF order the `UPDATE`s by key, and a GUID ordering that
  happened to turn the new default on first hit the index. This is the opposite trade from
  `UQ_RouteStops_RouteId_Sequence`, which solves the same class of problem by being `DEFERRABLE` — know
  which one you are touching. `IAppDbContext` exposes `DatabaseFacade Database` only so handlers can open
  that transaction.
- **A customer keeps at most 10 addresses** (`CreateAddressHandler.MaxAdresa`), counted in the handler —
  validators do not see the database. Create/Update also verify `DeliveryZoneId` exists, because the FK is
  `Restrict` and an unknown GUID would surface as a 500 instead of a 400 `invalid_zone`. An **inactive**
  zone stays allowed on purpose; `AddressDto.DeliveryZoneActive` is how the client learns to stop
  suggesting it.
- **`Order.Code` (`EP-yyMM-NNNN`) is generated from `Orders.Count() + 41`** (`Features/Orders/OrderCode.cs`,
  shared by `CreateOrder` and guest confirmation), while `Code` carries a **unique index**. So the counter
  is a running total, not a monthly one — the `yyMM` part rolls over but `NNNN` does not reset — and two
  orders created in the same instant compute the same number and the second one dies on the unique index
  with no retry anywhere. Low-traffic shop, so it has not bitten yet; do not build anything on the number
  being sequential-per-month, and if concurrency ever becomes real, replace it with a sequence.
- **A korpica (curated weekly produce box) is a `Product` with `Vrsta = Korpica`, not a separate
  entity.** That is why it travels the existing cart, checkout, `OrderItem`, zones, slots and routing
  untouched. A korpica must never be an item inside another korpica —
  `Product.ProvjeriInvarijantuKorpice()` encodes the rule (korpica needs `TipKorpice` + `Velicina` and
  `MozeUKorpicu == false`; the seeder calls it, nothing else does yet) and `IzmijeniNacrtHandler` enforces
  it at write time, rejecting a korpica among a draft's own items with `DomainException("korpica_u_korpici")`.
- **`SedmicniSadrzaj.Status` is a state machine** (`Nacrt → Objavljen → Zakljucan`), like `Order.Status`.
  It only moves forward, only through `Predji(...)` (`SedmicniSadrzaj.Prelazi`) — never assign `Status`
  directly. A `Nacrt` is never visible to a public endpoint: `GetSadrzajKorpiceHandler` throws
  `NotFoundException("sadrzaj_nije_objavljen")` for one, and the frontend matches that exact string
  (`sadrzaj-modal.tsx`, `korpice/[slug]/page.tsx`) because `NotFoundException` is `sealed` and hard-codes
  `not_found` as its `Code` — the specific identifier has nowhere to live but the message.
- **`SedmicniSadrzaj.CiljnaVrijednost` is frozen at publish**, as `Σ(Kolicina × JedinicnaCijena)` over the
  final draft, and is `0` while still a draft. Anything needing a target for a draft must use the
  generator's own target instead (`Product.Price × KoeficijentVrijednosti`), which
  `GetSadrzajPregledHandler` and the generator both expose as `CiljGenerisanja` (`ciljGenerisanja` on the
  wire). Confusing the two — using `CiljnaVrijednost` before publish — reads as `0` and broke a screen
  during implementation.
- **The public catalog excludes korpice.** `GetProductsHandler` and `GetProductBySlugHandler`
  (`Features/Catalog/CatalogQueries.cs`) filter `Vrsta == Obican`; korpice are sold only through
  `/korpice`. The admin product query (`GetAdminProductsHandler`) deliberately does **not** filter — the
  admin manages korpica stock from the same products screen. Because of that split, anything that
  reconciles a cart against the catalog must union `/products` with `/korpice` or it silently empties
  korpice from customers' carts — `useCart().reconcile()` prunes items absent from the list it's handed,
  and `checkout/page.tsx` plus `admin/orders/new/page.tsx` both pass it the union; a new consumer that
  forgets this drops every korpica from a saved cart with no error.
- **Korpica stock is not component stock.** `Product.Stock` on a korpica is how many boxes are available
  that week; the stock of the articles making up its content is **not decremented per order**, because
  the composition varies per customer once the editor ships (P2). The source of truth for procurement is
  the packing list (`GET /admin/korpice/pakerska-lista`, `GetPakerskaListaHandler`), not any single
  article's `Stock` — an article sold both standalone and inside a korpica shows only its standalone sales
  in `Stock`.
- **The weekly cycle (delivery day + ordering cutoff) comes from `DeliverySlot`, not from config.**
  `KorpiceQueries.UcitajRokAsync` reads the slot with the **lowest `DayOfWeek`** and uses its `DayOfWeek`,
  `TimeFrom` and `CutoffHours`. `KorpiceOpcije.RokSati` / `DanIsporukeRezerva` / `VrijemeIsporukeRezerva`
  are **fallbacks only**, read solely when the database has no `DeliverySlot` row at all; while any slot
  exists they are not consulted. `SedmicaHelper.DatumIsporuke` builds the delivery instant from day
  **and** `TimeFrom` and marks it `DateTimeKind.Utc` explicitly — it used to return bare midnight
  (`ISOWeek.ToDateTime` with `Kind.Unspecified`) and ignore `TimeFrom` entirely, which put the real
  cutoff up to a day earlier than the slot says and made the frontend's countdown disagree with the
  backend by the UTC offset once it started rendering the hour. A malformed `TimeFrom` (free-text field,
  no model validation) falls back to midnight rather than throwing — see the comment on
  `SedmicaHelper.ParsirajVrijeme`.
- **`DeliverySlot.TimeFrom` carries no timezone, and korpice read it as UTC.** The column is a bare
  string (`"16:00"`) and the slot's own `Label` ("Četvrtak 16–20h") is rendered verbatim in the shop —
  i.e. these times are written as **local Banja Luka time**. `SedmicaHelper.DatumIsporuke` nevertheless
  marks the instant it builds as `Utc`, so a slot saying 16:00 yields 18:00 local and the korpica
  ordering cutoff closes two hours later than the slot nominally states. This is a **known, accepted
  gap from P1**, not an oversight: backend and frontend agree with each other (the countdown does not
  lie about itself), the ambiguity predates korpice and the existing checkout shares it, and the owner
  tunes `CutoffHours` to the behaviour they want anyway. Fixing it properly means giving `DeliverySlot`
  an explicit timezone, or storing UTC and rendering local — do that before anything else starts
  depending on these instants.
- **`Product.Mjeseci` is `int[]` in C# but a CSV string in the database**, via a `ValueConverter` (the
  converter exists because tests run on SQLite, which has no array type) plus a `ValueComparer`
  (`AppDbContext.OnModelCreating`). The comparer is load-bearing: without it EF does not notice an
  in-place mutation of the array and silently skips the `UPDATE`. Because the column is text, season
  filtering for korpica generation happens **in memory** after loading the pool
  (`GenerisiSedmicniSadrzajHandler`) — do not try to push `Mjeseci.Contains(...)` into the SQL `WHERE`.
- **Korpica DATA lives in a migration, not in `DbSeeder`**, same shape as `Magacin` above: the
  `KorpicaPodaci` migration (not `DbSeeder`) inserts the `KorpicaKategorije` category links, the 15
  korpica `Product` rows, and a `MozeUKorpicu = true` backfill for existing ordinary products — the
  `Korpice` migration before it only added columns/tables, schema with no data. `DbSeeder` bails on any
  existing `Category` and only runs in Development or under `Seed:Demo=true`, so real production would
  otherwise come up with the schema but zero korpica rows: `GET /korpice` returns `[]` and "Generiši"
  can never produce anything, silently. The migration is idempotent (guards every insert so it is safe
  to run on a database `DbSeeder` already populated) and resolves the korpica rows' placeholder
  category/producer dynamically (first one that exists), not to a hardcoded id.
- **`Product` soft-delete (`!IsDeleted` global query filter) silently drops rows out of a required
  `Include`, not just the navigation.** `StavkaSadrzaja.ProductId` is non-nullable, so EF treats
  `StavkaSadrzaja → Product` as a required relationship and compiles `Include(Stavke).ThenInclude(Product)`
  as an INNER JOIN — when `Product` fails the filter, the whole `StavkaSadrzaja` row disappears from the
  parent's collection, it is **not** merely `Product == null` on a surviving row. `ObjaviSadrzajHandler`
  and `GetPakerskaListaHandler` both need `IgnoreQueryFilters()` because of this; without it, publishing a
  draft with a soft-deleted article can under-count `Stavke` enough to look like an empty draft instead of
  surfacing the actual "artikal obrisan" error.

**Enums are stored as strings** (`HasConversion<string>()` for `Role`, `Status`, `Unit`), so raw SQL
compares text: `WHERE "Role" = 'Admin'`, not a number.

**Auth:** JWT bearer, roles `Customer`/`Admin`/`Driver`. Admin endpoints use `[Authorize(Roles="Admin")]`;
the driver app is a first-class consumer with its own endpoints — `GET /driver/route`,
`PATCH /driver/orders/{id}/status` (accepts only `Delivered` or `FailedDelivery`), and
`POST /driver/route/reoptimize`. That last one takes the driver's live position as a parameter, uses it for
one computation, and **never stores or logs it**; see the logging notes under Route optimization.
Swappable services live behind interfaces (`IEmailService`, `IRouteOptimizer`, `IGeocodingService`,
`IViberService`, `ISocialAuthVerifier`).

**Rate limiting: three limiters, and only two of them are per visitor.** The global limiter is 300/min
**partitioned by `RemoteIpAddress`**. The `auth` policy is 10/min, also partitioned by IP — it is applied
at class level on `AuthController` (so `/auth/me`, which every page load hits, spends from it) and on
`orders/guest-request`, `orders/confirm` and `orders/track/{token}`. It was **not** partitioned at first,
which meant 10 requests/min for the whole site combined; a couple of simultaneous visitors 429'd each
other on login. The `geocode` policy is 30/min and is registered with `AddFixedWindowLimiter`, which
creates **one limiter shared by every caller** — despite what this file used to say, it is *not* per IP.
It is the sitewide ceiling on relaying to Nominatim, so a handful of customers typing
addresses at once can 429 each other. If you want it per visitor, it has to be rewritten as
`AddPolicy(... RateLimitPartition.GetFixedWindowLimiter(ip, ...))` the way `auth` was.

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
the `geocode` rate-limit policy (30/min, **shared across all callers, not per IP** — see Auth above) so
they cannot become an open relay to Nominatim.
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
- **The shop's route list**: `/` `/products` `/products/[slug]` `/cart` `/checkout` `/checkout/nalog`
  (prijava / registracija / bez naloga, before the form) `/potvrda/[token]` (guest clicks the emailed
  link — this is what creates the order) `/narudzba/[token]` (permanent tracking page, no login)
  `/orders` `/login` `/register` `/confirm-email`. The admin panel's nav (`components/admin/admin-nav.tsx`)
  is Pregled · Narudžbe · Proizvodi · Kategorije · Proizvođači · **Vozači** (`/admin/drivers`, full CRUD
  through `adminApi`) · Dostava · Magacin (`/admin/settings`); `/admin/routes` stays out of it, see below.
- **Checkout's address field is `AddressChooser`, not `AddressPicker`.** It shows a logged-in customer
  their previously delivered addresses (from `GET /orders/addresses`) with "Nova adresa" revealing the map;
  guests get the map straight away. Admin *Nova narudžba* still uses `AddressPicker` directly.
  **House number and flat reach the geocoder through `querySuffix`, never through the input's value.**
  Both components take that optional prop, append it **only to the query sent to `/geocode`**, and leave
  the displayed value and `onChange` bound to `form.ulica` alone. Concatenating "ulica broj" into the value
  was self-poisoning: every keystroke in the street field re-appended the old number, so it grew without
  bound. Editing the number still moves the pin because the geocoding effect depends on `querySuffix`.
  Picking a saved address pushes `onCoordinates(null)` — a history string has no pin of its own, and the
  abandoned one must not travel with it.
- **A point the picker was *given* is not a point the picker *owns*.** `AddressPicker` has two rules around
  `initialCoordinates` (the Adresar's saved pin, the warehouse in *Magacin*) and both exist because breaking
  either silently deletes a pin nobody touched: while the text is still the one the point arrived with
  (`polazniTekst`) it does **not** geocode — the mount would otherwise report `null` first and then whatever
  Nominatim thinks of that address; and its unmount cleanup reports `null` only if the picker itself ever
  reported a point (`preuzeoTacku`). Both guards are written to survive running twice — React's StrictMode
  mounts, cleans up and mounts again in dev, which is exactly how both bugs showed up. A screen that hands
  over `initialCoordinates` therefore keeps its point until the customer edits the address or moves the pin.
- **Address entry** (customer checkout, admin *Nova narudžba* at `admin/orders/new`, Adresar form) uses a keyless
  **OpenStreetMap + Leaflet** map picker (`components/shop/address-picker.tsx`): type → `geocodeApi`
  (our `/geocode`, see Geocoding) → draggable pin → reverse-geocode via `/geocode/reverse`. No Google Maps key. **Coordinates are persisted** alongside the address
  (`Order.Latitude`/`Longitude`, both nullable) and drive route optimization — an earlier version of this
  file said only the address string was kept, which stopped being true when the driver app gained a map.
  Nominatim does not resolve every real address, so `null` coordinates are normal and must stay nullable
  end to end; `0,0` is in the Atlantic and this project has shipped that bug once already. Orders left
  without coordinates are fixed by admins via `POST /admin/orders/geocode-missing` (batch retry) and
  `PUT /admin/orders/{id}/coordinates` (manual pin).
- **Order management (admin):** the orders list uses expandable cards (`components/admin/order-card.tsx`) with a
  status dropdown of valid state-machine transitions. Admins create orders via `orderApi.create` — the same
  endpoint a **logged-in** customer's checkout uses. Checkout branches: it calls `orderApi.create` when
  there is a session and `orderApi.guestRequest` when there is not, so the admin screen never touches the
  guest path (it has a session, and `guest-request` would reject it with `already_authenticated`).
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
