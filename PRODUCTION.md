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
UPDATE "Users" SET "Role" = 2 WHERE "Email" = 'you@yourdomain.com';  -- 2 = Admin
```

(Roles: `0` Customer, `1` Admin, `2` Driver — confirm against `UserRole` before running.)

## Security posture (enforced in code)

- **No demo data / default credentials in production** — seed is gated to Development.
- **JWT** — fail-fast without a strong key; HS256 pinned (`ValidAlgorithms`); issuer/audience/lifetime validated.
- **Passwords** — PBKDF2-SHA256, 100k iterations, per-user salt, constant-time compare.
- **Social login** — Google: verified-email only; Facebook: `debug_token` proves the token was issued for this app.
- **Rate limiting** — auth endpoints 10/min per IP, global 300/min.
- **HSTS** in non-development. **Terminate TLS** (HTTPS) at your host/reverse proxy.

## Viber notifications (order confirmation fallback)

When a customer leaves **no email**, the order confirmation is sent over **Viber** instead
(the phone is always present). Today this is a **stub** (`LoggingViberService`) that only logs
the message — sending a Viber message to an arbitrary number requires a paid **Viber Business
Messages** provider (BSP: Infobip, Twilio, …) with a registered sender.

To enable real sending: implement `IViberService` against your BSP's API (reuse
`ViberMessage.Build`), fill the `Viber` config section (`Provider`/`ApiKey`/`Sender`, via
`Viber__ApiKey` env var), and swap the DI registration in `DependencyInjection.cs` — nothing
else in the order flow changes.

## Still your call (operational)

- **Migrations** run automatically on startup. For multi-instance deploys, consider applying them
  once as a separate step instead.
- **Map/geocoding** uses free OpenStreetMap + Nominatim (checkout address picker). Nominatim's policy
  is ~1 req/sec + attribution; for real traffic use a paid geocoder or self-host.
- Consider server-side **token revocation** if you need forced logout (currently stateless JWT, valid until expiry).
