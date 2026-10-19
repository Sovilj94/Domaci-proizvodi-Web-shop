# Production readiness checklist

This app is safe to run locally as-is, but a few things **must** be set for production.
Secrets are read from environment variables (ASP.NET uses `__` for nested keys, e.g.
`Jwt__SigningKey` → `Jwt:SigningKey`). Never commit real secrets.

## Backend — required environment variables

| Variable | Why |
|---|---|
| `ASPNETCORE_ENVIRONMENT=Production` | Disables demo seed + Swagger, enables HSTS. |
| `Jwt__SigningKey` | **Strong random secret, ≥ 32 chars.** The app **refuses to start** in Production without it (no insecure fallback). Generate e.g. `openssl rand -base64 48`. |
| `ConnectionStrings__DefaultConnection` | Production PostgreSQL connection string. |
| `Cors__Origins__0` | Your real frontend origin, e.g. `https://epiljara.ba`. Without it the browser blocks API calls. |
| `Email__SmtpHost` / `Email__SmtpPort` / `Email__UseSsl` / `Email__Username` / `Email__Password` / `Email__From` | Real SMTP (e.g. Gmail 587 + app password, SendGrid, Resend) so order confirmations actually send. Dev uses a local Mailpit catcher. |
| `App__BaseUrl` | Public URL of the frontend. Builds every link the backend emails: the email-confirmation link, the guest order's confirm link (`/potvrda/{token}`), and the order tracking link (`/narudzba/{token}`). Defaults to `http://localhost:3000` if unset — wrong or missing in production and a guest's confirmation link points nowhere real, so they can never confirm and no order is ever created. |
| `Auth__Google__ClientId` | Only if Google login is enabled. |
| `Auth__Facebook__AppId` / `Auth__Facebook__AppSecret` | Only if Facebook login is enabled (both required — the secret backs the `debug_token` app check). |

## Frontend — required environment variables

| Variable | Why |
|---|---|
| `NEXT_PUBLIC_API_BASE_URL` | Production API URL, e.g. `https://api.epiljara.ba`. |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | Only if Google login is enabled (same client id as the backend). |
| `NEXT_PUBLIC_FACEBOOK_APP_ID` | Only if Facebook login is enabled. |

## Demo data for testing

The demo seed (products, zones, and accounts like `admin@epiljara.ba` / `admin123`) runs
automatically in **Development**. To test on a deployed staging instance, opt in with
`Seed__Demo=true`. **Leave it unset in real production** — otherwise the default admin account
is created. It seeds only into an empty database.

## First admin account (real production)

With `Seed__Demo` unset, a fresh production database has **no accounts**. Create the first admin
deliberately, e.g.:
register a normal customer through the app, then promote them once in the DB:

```sql
UPDATE "Users" SET "Role" = 'Admin', "EmailConfirmed" = true WHERE "Email" = 'you@yourdomain.com';
```

(`Role` is stored as **text** — `'Customer'`, `'Admin'` or `'Driver'`, the `UserRole` enum names — not a
number. Setting `EmailConfirmed` spares you the confirmation email if SMTP isn't configured yet; login is
refused until it is `true`.)

## Warehouse location (already set, but check it)

Every route is computed from — and back to — one point: the warehouse. It lives in the database
(one-row `Magacin` table), **not** in `appsettings.json`, so it changes without a rebuild. The
`MagacinUBazi` migration seeds **Stričići on Manjača** (`44.577975, 17.005162`, the village centre
per OpenStreetMap) in every environment, production included — unlike the demo seed.

Set the real point before the first delivery, either way:

- **Admin panel** — *Magacin* in the sidebar (`/admin/settings`): search, drag the pin, save.
- **SQL**, if you prefer:

```sql
UPDATE "Magacin" SET "Naziv" = 'Magacin …', "Latitude" = 44.…, "Longitude" = 17.…;
```

The API **refuses to start** when the row is missing or the point falls outside Bosnia and
Herzegovina — a route silently computed from `0°N 0°E` (the Atlantic) looks perfectly plausible in the
driver's app. It cannot catch a *wrong* point inside the country, so confirm it on the map.

## Security posture (enforced in code)

- **No demo data / default credentials in production** — seed is gated to Development.
- **JWT** — fail-fast without a strong key; HS256 pinned (`ValidAlgorithms`); issuer/audience/lifetime validated.
- **Passwords** — PBKDF2-SHA256, 100k iterations, per-user salt, constant-time compare.
- **Social login** — Google: verified-email only; Facebook: `debug_token` proves the token was issued for this app.
- **Rate limiting** — the `auth` policy (login/register/`/auth/me`, plus the guest checkout endpoints
  `orders/guest-request`, `orders/confirm`, `orders/track/{token}`) is 10/min **per IP**; global 300/min per IP.
  The `geocode` policy on `/geocode` and `/geocode/reverse` is 30/min but **not** partitioned — it is one
  limiter shared by everyone, i.e. a sitewide ceiling on relaying to Nominatim rather than a per-visitor
  limit. It stops the endpoints becoming an open relay; it does not stop one visitor from exhausting the
  quota for the rest. Partition it by IP (as `auth` is) before any campaign-sized traffic.
- **HSTS** in non-development. **Terminate TLS** (HTTPS) at your host/reverse proxy.
- **`GET /orders/track/{token}` is intentionally anonymous** — a guest has no account to authenticate with,
  so the permanent `PublicToken` in the URL *is* the credential, and it returns the customer's name, phone,
  email and delivery address to whoever holds it. Treat that link like a bearer token: it must not end up
  anywhere that logs full request URLs (reverse proxy access logs, error trackers, analytics) — the same
  goes for the guest confirmation link (`/potvrda/{token}`) before it's used.

## Viber notifications (order confirmation fallback)

When a customer leaves **no email**, the order confirmation is sent over **Viber** instead
(the phone is always present). Today this is a **stub** (`LoggingViberService`) that only logs
the message — sending a Viber message to an arbitrary number requires a paid **Viber Business
Messages** provider (BSP: Infobip, Twilio, …) with a registered sender.

To enable real sending: implement `IViberService` against your BSP's API (reuse
`ViberMessage.Build`), fill the `Viber` config section (`Provider`/`ApiKey`/`Sender`, via
`Viber__ApiKey` env var), and swap the DI registration in `DependencyInjection.cs` — nothing
else in the order flow changes.

## Korpice (curated weekly produce boxes)

**The korpica data comes from a migration, not from `DbSeeder`.** The `Korpice` migration
(20261007075847) added the columns and three tables — schema only, left empty by design. The
`KorpicaPodaci` migration (20261008071037) that follows it inserts the actual data: the
`KorpicaKategorije` category links, the 15 korpica `Product` rows (5 types × 3 sizes), and a
backfill (`MozeUKorpicu = true` for existing ordinary products). Without it, on any database
that was never seeded by `DbSeeder` (real production: it bails on any existing `Category` and
only runs in Development or under `Seed:Demo=true`), `GET /korpice` returns `[]`, every tab
shows "nije u ponudi", and "Generiši" can never produce anything — nothing errors, the feature
is just silently empty. Same shape as `Magacin` (see that section above): data a real
deployment needs lives in a migration, not the demo seeder. The migration is idempotent
(guards every insert, safe to run on a database `DbSeeder` already populated) and resolves the
korpica rows' placeholder category/producer to whichever one already exists first on that
database, not a hardcoded id — but it still needs **at least one `Category` and one
`Producer`** to exist before it runs; on a database with neither, the korpica product rows are
skipped (the `KorpicaKategorije` links for categories that do exist still insert) until the
owner adds their first product.

Config section `Korpice` (`appsettings.json`, override via `Korpice__*` env vars):

| Key | Default | Meaning |
|---|---|---|
| `Korpice:KoeficijentVrijednosti` | `1.15` | How many times the content's value exceeds the box's price — the generator's target per box, and what `ciljGenerisanja` on the admin overview reflects. Always in effect. |
| `Korpice:MinimalanBrojArtikala` | `{Mala: 5, Srednja: 7, Velika: 9}` | Minimum item count the generator aims for, per box size. Always in effect; under-filled pools return a warning for the admin to fix by hand, not an error. |
| `Korpice:RokSati` | `48` | Ordering-cutoff hours before delivery. **Fallback only** — used only when the database has no `DeliverySlot` row at all. While any `DeliverySlot` exists, its own `CutoffHours` (on the slot with the lowest `DayOfWeek`) is the sole source of truth and this key is not read. |
| `Korpice:DanIsporukeRezerva` | `Friday` | Delivery day. Same fallback-only caveat as `RokSati` — superseded by the lowest-`DayOfWeek` `DeliverySlot` whenever one exists. |
| `Korpice:VrijemeIsporukeRezerva` | `16:00` | Delivery time-of-day, combined with `DanIsporukeRezerva` into the delivery instant. Same fallback-only caveat — superseded by the lowest-`DayOfWeek` `DeliverySlot`'s own `TimeFrom` whenever one exists. |

`ZakljucavanjeSadrzajaService` (a `BackgroundService`, now in `Infrastructure/BackgroundJobs/`)
runs every **15 minutes** and closes (`Zakljucan`) every published (`Objavljen`) weekly content
whose ordering cutoff — delivery day **and** time, minus `CutoffHours` — has passed. If it is not
running, a content stays editable past its deadline, so the packing list (`GET
/admin/korpice/pakerska-lista`) can still shift after the warehouse already has the printed list in hand —
the failure mode is silent (nothing errors; the content just never locks), so confirm the service is
registered and running rather than inferring it from the absence of complaints.

## Pretplate (recurring basket subscriptions)

Config section `Pretplate` (override via `Pretplate__*` env vars):

| Key | Default | Meaning |
|---|---|---|
| `Pretplate:PopustNedjeljna` / `PopustMjesecna` / `PopustTriMjeseca` | `0.10` / `0.15` / `0.20` | Discount per plan, applied **from the first delivery** (R2). |
| `Pretplate:ObavezaMjesecna` / `ObavezaTriMjeseca` | `4` / `13` | Deliveries the customer commits to. Snapshotted on the subscription at creation; during the commitment only an admin can cancel (`/admin/pretplate`). Cash on delivery means it cannot be enforced financially. |
| `Pretplate:SatiUnaprijed` | `48` | How many hours before the week's ordering cutoff the job creates that week's orders. Lower it and customers lose the window in which they can still pause; raise it and orders exist long before anyone can change their mind. |
| `Pretplate:MaksPauzaSedmica` | `8` | Longest pause a customer can set at once, counted in deliveries. |
| `Pretplate:AlarmSatiPrijeRoka` | `24` | Hours before the cutoff at which every admin (users with role Admin and an e-mail) gets one mail listing baskets with active subscriptions whose week content is still unpublished (A2b). Sent once per basket per week (`Products.AlarmSadrzaja*`). Without published content the job skips that week, and subscribers get the "no delivery this week" mail (A2) after the cutoff. |

`PretplatneNarudzbeService` (a `BackgroundService` in `Infrastructure/BackgroundJobs/`) runs every
**15 minutes** and creates one order per active subscription whose week has come. **If it is not
running, no subscriber gets anything** and nothing errors anywhere — the only visible symptom is
missing orders, so confirm the service is registered and that the log line "Pretplatnih narudžbi
napravljeno" appears in the hours before the weekly cutoff. The command is idempotent, so a restart
mid-pass cannot duplicate an order.

Two operational consequences worth knowing before the first real week:

- **An unpublished weekly content means that subscriber gets nothing that week** — by design, since an
  order with no content is a box nobody can pack. `GET /admin/pretplate` reports `cekajuSadrzaj`: how
  many active subscriptions are still waiting for content for the upcoming week. Watch that number
  before the cutoff, not after.
- **Prices are not frozen at subscription time.** Each order takes the basket's current price minus the
  plan discount, so a price change reaches subscribers with their next delivery.
- **Only vegetable and fruit baskets are on sale** (R2). Household, dairy and cured-meat baskets were set
  unavailable by migration `DodajPlanPretplate`; re-enable them in admin if ever needed. Existing
  subscriptions to a hidden basket are not cancelled automatically — check `/admin/pretplate`.
- **Photos (R1):** hero, banner and basket photos are placeholders from product images — replace them
  per `frontend/public/izgled/README.md` (paths in `frontend/src/lib/izgled.ts`). Do not reuse farmit.rs
  photos or texts.

## Still your call (operational)

- **Migrations** run automatically on startup. For multi-instance deploys, consider applying them
  once as a separate step instead.
- **Geocoding** runs through our own `/geocode` endpoints (the browser no longer calls Nominatim
  directly). The upstream is `Nominatim__BaseUrl`, and it defaults to the **public** Nominatim — so
  customer addresses still reach a third party, and `Nominatim__ThrottleMs=1000` (their usage policy, not
  a tuning knob — violating it gets the IP blocked) makes every search in the app queue behind one
  request per second. **Before real traffic, self-host it:** `backend/nominatim/README.md` has the import
  and its disk/memory cost; then set `Nominatim__BaseUrl` to your instance and `Nominatim__ThrottleMs=0`.
  That removes both the disclosure and the rate limit, with no code change. Map tiles still come from
  `tile.openstreetmap.org` and require attribution.
- Consider server-side **token revocation** if you need forced logout (currently stateless JWT, valid until expiry).
