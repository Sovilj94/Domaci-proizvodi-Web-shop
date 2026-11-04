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
2. Na <https://render.com> otvori **New → Blueprint**, poveži GitHub repo i izaberi granu
   **`main`**. Render pročita `render.yaml` i ponudi servis `epiljara-api`. U `render.yaml` su
   `branch: main` i `autoDeploy: true`, pa svaki push na `main` sam pokreće novi deploy API-ja.
   Push u kom su izmijenjeni samo frontend ili dokumentacija API ne dira (`buildFilter`).
3. Render pita za vrijednosti sa `sync: false`:

   | Varijabla | Vrijednost |
   |---|---|
   | `ConnectionStrings__DefaultConnection` | string iz Neona (korak 1) |
   | `Jwt__SigningKey` | ključ iz koraka 2.1 |
   | `CORS_ORIGINS` | za sada prazno; Vercel URL ide u koraku 4 |
   | `App__BaseUrl` | za sada prazno; Vercel URL ide u koraku 4 |
   | `RUN_MIGRATIONS` | `true` |
   | `ADMIN_EMAIL` | tvoj email za admin panel |
   | `ADMIN_PASSWORD` | tvoja lozinka, min. 8 znakova (nije u repou; promjena + restart = nova lozinka) |
   | `ADMIN_NAME` | opciono, ime koje se prikazuje |
   | `Email__*` | prazno, ili vidi „Email" niže |

4. **Apply**. Prvi build traje nekoliko minuta. Kad se završi, otvori
   `https://epiljara-api.onrender.com/health` — mora vratiti `{"status":"ok"}`.
   Tačan URL piše na vrhu stranice servisa.
5. U logu servisa (**Logs**) ne smije biti `Database initialization failed`. Ako ga ima,
   connection string nije dobar.

`RUN_MIGRATIONS` može ostati `true`, jer je servis jedna instanca, a migracije koje su već
primijenjene se preskaču.

### Podaci

U kodu nema nijednog podatka: API pri startu samo primijeni migracije i napravi admina iz
`ADMIN_EMAIL`. Nova baza je zato **prazna**. Podatke donosi dump postojeće baze (proizvodi, slike,
korpice, sadržaj, nalozi), a sve poslije toga se unosi kroz admin.

Prenos lokalne baze u Neon (prazan Neon projekat, prije prvog starta API-ja):

```powershell
docker exec epiljara-postgres sh -c "pg_dump -U epiljara -d epiljara -Fc --no-owner --no-acl -f /var/tmp/epiljara.dump"
docker cp epiljara-postgres:/var/tmp/epiljara.dump .\epiljara.dump
docker run --rm -v ${PWD}:/d postgres:16 pg_restore --no-owner --no-acl -d "<Neon URI>" /d/epiljara.dump
```

Dump nosi i `__EFMigrationsHistory`, pa API poslije samo dopuni migracije koje su novije od dumpa.

## 3. Vercel — frontend

1. Na <https://vercel.com> otvori **Add New → Project** i importuj isti repo.
2. **Root Directory: `frontend`**. Framework se sam prepozna kao Next.js, a build komande
   ostavi podrazumijevane.
3. **Environment Variables**:

   | Varijabla | Vrijednost |
   |---|---|
   | `NEXT_PUBLIC_API_URL` | `https://epiljara-api.onrender.com` (bez `/` na kraju) |
   | `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | opciono, isti kao `Auth__Google__ClientId` na backendu |

   > Bez `NEXT_PUBLIC_API_URL` frontend gađa `http://localhost:5080` i nijedan podatak se ne
   > učita — ugrađenih podataka nema. Vrijednost se ugrađuje u build, pa poslije svake izmjene
   > treba **Redeploy**.
4. **Deploy**. Zapiši dobijeni domen, npr. `https://epiljara.vercel.app`.
5. Provjeri **Settings → Git → Production Branch** = `main` (podrazumijevano jeste). Tada svaki
   push na `main` sam ide na produkcijski Vercel domen, a ostale grane dobiju samo preview.

> **Kako se deploya od sada:** spoji izmjene u `main` i pushaj, i to je sve. Render i Vercel
> sami povuku `main` i deployaju. Ručni deploy ne treba.

## 4. Nazad na Render — CORS i linkovi

Na Renderu otvori **epiljara-api → Environment** i postavi:

- `CORS_ORIGINS` = `https://epiljara.vercel.app`. Više domena se odvaja zarezom, a kosa crta
  na kraju ne smeta.
- `App__BaseUrl` = `https://epiljara.vercel.app`. Od ovoga se prave linkovi u mejlovima
  (potvrda naloga, potvrda gost-narudžbe, praćenje narudžbe).

**Save Changes** pokreće novi deploy. Poslije toga se sa Vercel sajta prijavi svojim
`ADMIN_EMAIL` / `ADMIN_PASSWORD` i otvori `/admin` da provjeriš cijeli lanac.

Vercel *preview* deploy (svaki PR ili grana) ima drugi domen i CORS ga odbija, osim ako
i njega dodaš u `CORS_ORIGINS`. Za testere dijeli samo produkcijski Vercel domen.

## 5. Napomena za testere

> Server je na besplatnom planu i uspava se nakon ~15 minuta bez posjeta. **Prvi zahtjev nakon
> pauze traje do ~1 minute** — stranica tada izgleda kao da „visi" ili prikaže grešku. Sačekaj
> i osvježi stranicu, pa sve dalje radi normalno. Baza se poslije pauze budi par sekundi.

Demo nalozi postoje samo ako je baza prenesena iz lokalnog dumpa (vidi „Podaci"):
`admin@epiljara.ba` / `admin123`, `marko@example.com` / `marko123`, `vozac@epiljara.ba` / `vozac123`.
Lozinke su javne (zapisane su u dokumentaciji), pa ovo okruženje ne koristi za stvarne podatke. Tvoj
admin iz `ADMIN_EMAIL` je odvojen od njih i ima samo tvoju lozinku.

---

## Poznata ograničenja ovog okruženja

- **Google ne indeksira test sajt, namjerno.** Bez `SEO_INDEX=true` fajl `robots.txt`
  zabranjuje sve, a svaka stranica nosi `noindex`. Na pravom domenu na Vercelu postavi
  `NEXT_PUBLIC_SITE_URL=https://…` i `SEO_INDEX=true`, uradi Redeploy, pa prijavi
  `/sitemap.xml` u Google Search Console (vidi `PRODUCTION.md`, sekcija SEO).

- **Mejlovi.** Svaki mejl prvo ide u red u bazi (`/admin/mejlovi` pokazuje šta je poslato, šta čeka i
  zašto nije prošlo), a šalje ga pozadinski posao. Render free blokira odlazni SMTP (25/465/587), pa u
  produkciji mejl ide preko **Brevo HTTP API-ja** (port 443). Vidi „Mejlovi u produkciji" ispod.
- **Pozadinski poslovi** (pretplatne narudžbe, zaključavanje sedmičnog sadržaja, čišćenje
  isteklih gost-zahtjeva) rade samo dok je servis budan, a dok spava ništa se ne izvršava.
  Ako testirate pretplate ili rokove, otvorite sajt prije roka.
- **Rute vozača**: OSRM ne postoji na Renderu, pa se ture slažu po zonama (postojeći fallback).
  **Geokodiranje** ide preko javnog Nominatima (1 zahtjev/s).
- **Slike** proizvoda, kategorija, korpica i domaćina su **u bazi** (tabela `Slike`). API ih servira
  na `/slike/...`, a Vercel ih prosljeđuje API-ju (rewrite u `next.config.ts`). Admin novu sliku
  učita dugmetom „Učitaj sliku" u formi i ona odmah važi, bez deploya. Slike stižu samo sa dumpom
  baze ili kroz admin — u repou ih nema. `NEXT_PUBLIC_API_URL` na Vercelu mora biti postavljen, jer
  bez njega slika nema. Disk na Renderu se briše pri restartu, ali slike nisu na disku, pa se ništa ne gubi.
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

## Mejlovi u produkciji (Brevo)

> Detaljna lista za čekiranje (domen, DNS, Brevo, Render, provjera, problemi): `docs/MEJLOVI-PRODUKCIJA.md`.

Kod je spreman; treba samo nalog kod dobavljača i DNS zapisi domena. Brevo je izabran jer šalje preko
HTTPS-a (radi na Renderu), besplatno daje **300 mejlova dnevno**, a serveri su u EU.

1. **Nalog:** <https://www.brevo.com> → registracija (besplatni plan).
2. **Domen pošiljaoca** (bez ovoga mejlovi idu u spam ili ih Brevo odbija):
   Brevo → *Senders, Domains & Dedicated IPs* → *Domains* → *Add a domain* (npr. `epiljara.ba`).
   Brevo prikaže zapise koje treba dodati kod registrara domena:
   - **TXT `brevo-code`** — potvrda vlasništva,
   - **DKIM** (TXT ili CNAME, kako Brevo ponudi),
   - **SPF**: ako domen već ima `v=spf1 …`, dopiši `include:spf.brevo.com`; inače `v=spf1 include:spf.brevo.com ~all`,
   - **DMARC** (preporuka): TXT na `_dmarc` sa `v=DMARC1; p=none; rua=mailto:tvoja@adresa`.
   Kad Brevo pokaže sve zelene kvačice, domen je potvrđen.
3. **Pošiljalac:** *Senders* → *Add a sender* → npr. `narudzbe@epiljara.ba`, ime „E piljara".
4. **API ključ:** *SMTP & API* → *API Keys* → *Generate a new API key*. Ključ se vidi samo jednom.
5. **Render → epiljara-api → Environment:**

   | Ključ | Vrijednost |
   | --- | --- |
   | `Email__Transport` | `brevo` |
   | `Email__Brevo__ApiKey` | ključ iz koraka 4 |
   | `Email__From` | adresa iz koraka 3 (mora biti na potvrđenom domenu) |
   | `Email__FromName` | `E piljara` |
   | `Email__ReplyTo` | adresa koju neko stvarno čita (odgovori kupaca stižu tu) |
   | `App__BaseUrl` | javna adresa sajta — od nje su linkovi u mejlovima |

   Sačuvaj → Render sam redeploya. Bez ključa uz `Email__Transport=brevo` API namjerno ne starta.
6. **Provjera:** `/admin/mejlovi` → „Pošalji probni mejl". Status „Poslato" znači da ga je Brevo primio;
   provjeri i da nije u spamu. Greška (npr. `Brevo 401` — pogrešan ključ, ili nepotvrđen pošiljalac)
   piše uz mejl; kad je popraviš, mejlovi iz reda odu sami, a oni sa „Greška" šalješ ponovo jednim klikom.
7. **Kome idu obavještenja radnicima:** `/admin/mejlovi` → Podešavanja → „Primaoci" (prazno = svi admini).

**Render free spava** poslije ~15 min bez posjeta: mejl napravljen u zahtjevu ode odmah (servis je tada
budan), ali plan vozačima u 17 h i ponovni pokušaji idu tek kad se servis probudi. Rješenje bez troška:
besplatan ping (npr. cron-job.org) na `https://epiljara-api.onrender.com/health` svakih 10 minuta — jedan
servis budan cijeli mjesec staje u 750 besplatnih sati.

**Lokalno** ostaje `Email__Transport=smtp` prema Mailpitu (`docker run -p 1025:1025 -p 8025:8025 axllent/mailpit`,
pregled na <http://localhost:8025>).
