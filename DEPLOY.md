# Besplatan test deploy — Vercel + Render + Neon

Test okruženje za nekoliko ljudi, **nije produkcija**. Frontend (Next.js) ide na **Vercel Hobby**,
API (ASP.NET Core, Docker) na **Render free**, a baza na **Neon Free**. Render Postgres se ne koristi
jer se free baza briše nakon 30 dana.

Šta je u repou: `render.yaml` (Blueprint), `backend/Dockerfile`, `backend/.dockerignore`,
`frontend/.env.example`. Za produkciju vidi i `PRODUCTION.md`.

Redoslijed je bitan jer API mora znati URL frontenda (CORS), a frontend URL API-ja:
**Neon → Render → Vercel → nazad na Render.**

---

## 1. Neon — baza

1. Na <https://neon.tech> napravi nalog, pa **New Project**.
   - Region: **AWS Europe Central (Frankfurt)**, isti kao Render u `render.yaml`.
   - Postgres verzija: 16 ili novija.
2. Na **Dashboard → Connect** isključi **Connection pooling** i kopiraj *direktni* connection string.
   Migracije su sigurnije bez PgBouncer-a, a za nekoliko testera pooling ne treba.
3. Oba oblika rade, pa uzmi koji god hoćeš:
   - URI, tačno kako ga Neon da:
     `postgresql://neondb_owner:LOZINKA@ep-xxxx.eu-central-1.aws.neon.tech/neondb?sslmode=require`
   - Npgsql oblik:
     `Host=ep-xxxx.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=LOZINKA;SSL Mode=Require;Trust Server Certificate=true`

   API sam prevodi URI u Npgsql oblik. Parametar `channel_binding` se pritom ignoriše.

## 2. Render — API

1. Napravi JWT ključ (min. 32 znaka), na primjer u PowerShell-u:
   ```powershell
   [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
   ```
   ili `openssl rand -base64 48`.
2. Na <https://render.com> otvori **New → Blueprint**, poveži GitHub repo i izaberi granu.
   Render pročita `render.yaml` i ponudi servis `epiljara-api`.
3. Render pita za vrijednosti sa `sync: false`:

   | Varijabla | Vrijednost |
   |---|---|
   | `ConnectionStrings__DefaultConnection` | string iz Neona (korak 1) |
   | `Jwt__SigningKey` | ključ iz koraka 2.1 |
   | `CORS_ORIGINS` | za sada prazno; Vercel URL ide u koraku 4 |
   | `App__BaseUrl` | za sada prazno; Vercel URL ide u koraku 4 |
   | `RUN_MIGRATIONS` | `true` |
   | `SEED_DATA` | `true` ako hoćeš demo proizvode i demo naloge (preporučeno za test) |
   | `Email__*` | prazno, ili vidi „Email" niže |

4. **Apply**. Prvi build traje nekoliko minuta. Kad se završi, otvori
   `https://epiljara-api.onrender.com/health` — mora vratiti `{"status":"ok"}`.
   Tačan URL piše na vrhu stranice servisa.
5. U logu servisa (**Logs**) ne smije biti `Database initialization failed`. Ako ga ima,
   connection string nije dobar.

`RUN_MIGRATIONS` može ostati `true`, jer je servis jedna instanca, a migracije koje su već
primijenjene se preskaču. Seed upisuje podatke samo u praznu bazu.

## 3. Vercel — frontend

1. Na <https://vercel.com> otvori **Add New → Project** i importuj isti repo.
2. **Root Directory: `frontend`**. Framework se sam prepozna kao Next.js, a build komande
   ostavi podrazumijevane.
3. **Environment Variables**:

   | Varijabla | Vrijednost |
   |---|---|
   | `NEXT_PUBLIC_API_URL` | `https://epiljara-api.onrender.com` (bez `/` na kraju) |
   | `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | opciono, isti kao `Auth__Google__ClientId` na backendu |

   > Bez `NEXT_PUBLIC_API_URL` sajt se **normalno otvori, ali radi nad ugrađenim mock podacima**,
   > ne nad pravim API-jem. Ako vidiš „lažne" podatke, ovo provjeri prvo. Vrijednost se ugrađuje
   > u build, pa poslije svake izmjene treba **Redeploy**.
4. **Deploy**. Zapiši dobijeni domen, npr. `https://epiljara.vercel.app`.

## 4. Nazad na Render — CORS i linkovi

Na Renderu otvori **epiljara-api → Environment** i postavi:

- `CORS_ORIGINS` = `https://epiljara.vercel.app`. Više domena se odvaja zarezom, a kosa crta
  na kraju ne smeta.
- `App__BaseUrl` = `https://epiljara.vercel.app`. Od ovoga se prave linkovi u mejlovima
  (potvrda naloga, potvrda gost-narudžbe, praćenje narudžbe).

**Save Changes** pokreće novi deploy. Poslije toga se sa Vercel sajta prijavi kao
`marko@example.com` / `marko123` (ako je `SEED_DATA=true`) da provjeriš cijeli lanac.

Vercel *preview* deploy (svaki PR ili grana) ima drugi domen i CORS ga odbija, osim ako
i njega dodaš u `CORS_ORIGINS`. Za testere dijeli samo produkcijski Vercel domen.

## 5. Napomena za testere

> Server je na besplatnom planu i uspava se nakon ~15 minuta bez posjeta. **Prvi zahtjev nakon
> pauze traje do ~1 minute** — stranica tada izgleda kao da „visi" ili prikaže grešku. Sačekaj
> i osvježi stranicu, pa sve dalje radi normalno. Baza se poslije pauze budi par sekundi.

Demo nalozi (uz `SEED_DATA=true`): `admin@epiljara.ba` / `admin123`, `marko@example.com` /
`marko123`, `vozac@epiljara.ba` / `vozac123`. Lozinke su javne (u repou su), pa ovo okruženje ne
koristi za stvarne podatke.

---

## Poznata ograničenja ovog okruženja

- **Google ne indeksira test sajt, namjerno.** Bez `SEO_INDEX=true` fajl `robots.txt`
  zabranjuje sve, a svaka stranica nosi `noindex`. Na pravom domenu na Vercelu postavi
  `NEXT_PUBLIC_SITE_URL=https://…` i `SEO_INDEX=true`, uradi Redeploy, pa prijavi
  `/sitemap.xml` u Google Search Console (vidi `PRODUCTION.md`, sekcija SEO).

- **Email (SMTP).** Render free blokira odlazni SMTP na portovima 25, 465 i 587. Bez mejla:
  - nova registracija ne može da potvrdi nalog, a prijava se odbija dok nalog nije potvrđen;
  - gost-narudžba se nikad ne napravi, jer se kreira tek klikom na link iz mejla.

  Rješenja bez izmjene koda:
  - koristi demo naloge, ili ručno potvrdi nalog u Neon SQL editoru:
    `UPDATE "Users" SET "EmailConfirmed" = true WHERE "Email" = '…';`
  - ili uzmi SMTP provajdera koji nudi **port 2525** (Brevo, SendGrid, Mailgun…) i postavi
    `Email__SmtpHost`, `Email__SmtpPort=2525`, `Email__UseSsl`, `Email__Username`,
    `Email__Password` i `Email__From` (adresa koju je provajder verifikovao).
- **Pozadinski poslovi** (pretplatne narudžbe, zaključavanje sedmičnog sadržaja, čišćenje
  isteklih gost-zahtjeva) rade samo dok je servis budan, a dok spava ništa se ne izvršava.
  Ako testirate pretplate ili rokove, otvorite sajt prije roka.
- **Rute vozača**: OSRM ne postoji na Renderu, pa se ture slažu po zonama (postojeći fallback).
  **Geokodiranje** ide preko javnog Nominatima (1 zahtjev/s).
- **Slike** su URL-ovi ili statični fajlovi iz `frontend/public`, pa nema uploada na disk API-ja.
  Disk na Renderu se briše pri svakom deployu/restartu i to ovdje ništa ne gubi.
- **Prijava preko Google-a** radi tek kad se Vercel domen doda u *Authorized JavaScript origins*
  u Google Cloud konzoli. Do tada koristi email i lozinku.
- **Limiti**: Render free daje 750 sati mjesečno. Neon Free ima ograničen compute i 0,5 GB
  prostora, što je dovoljno za test.

## Lokalna provjera Docker image-a

```bash
docker build -t epiljara-api backend
docker run --rm -p 10000:10000 -e PORT=10000 -e ASPNETCORE_ENVIRONMENT=Production \
  -e "ConnectionStrings__DefaultConnection=<neki Postgres>" \
  -e Jwt__SigningKey=<min-32-znaka> epiljara-api
curl http://localhost:10000/health   # {"status":"ok"}
```
