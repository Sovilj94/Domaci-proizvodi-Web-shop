# P3 — Pretplate: plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kupac se pretplati na korpicu; narudžbe nastaju same, svake sedmice, prije roka. Pauza, promjena paketa i otkaz sa ekrana naloga. Popust od šeste isporuke.

**Architecture:** Pretplata je **nalog za ponavljanje**, ne narudžba. Pozadinski posao jednom sedmično pravi narudžbu kroz **isti** `CreateOrderHandler` koji koristi checkout — nema drugog puta do `Order`-a. Idempotentnost nosi par `(PoslednjaGodina, PoslednjaSedmica)` upisan u istoj transakciji kao narudžba.

**Tech Stack:** ASP.NET Core 9, EF Core 9 + PostgreSQL, MediatR, FluentValidation, xUnit + SQLite in-memory; Next.js 15 App Router, TypeScript, Tailwind v4, TanStack Query.

**Spec:** `docs/superpowers/specs/2026-10-13-p3-pretplate-design.md`

## Global Constraints

Preuzeto iz plana P2b — isti repo, ista pravila, ništa od ovoga nije novo:

- **Omotnica odgovora:** svaki endpoint vraća `{ data, error, meta }` preko `Envelope`/`EnvelopeList` iz `ApiControllerBase`. Nikad `Ok(dto)`.
- **Identifikator greške je DRUGI argument konstruktora.** `new DomainException("Poruka.", "kod_greske")`; middleware serijalizuje `Code`, ne `Message`.
- `DomainException`→400, `NotFoundException`→404, `ForbiddenException`→403, `ConflictException`→409.
- **JSON izostavlja null** (`WhenWritingNull`): nullable backend polje je na žici odsutno. TS koristi `?:`, nikad `| null`.
- **`TestDb` dijeli jedan `DbContext`**; test koji tvrdi nešto o učitanim podacima zove `db.ChangeTracker.Clear()` ili `AsNoTracking()`.
- **`MediatR.Unit` i `Domain.Enums.Unit` se sudaraju** — alias ili puno ime.
- **Nova NOT NULL kolona dobija eksplicitan `defaultValue`** u migraciji.
- **Testovi rade na SQLite** preko `EnsureCreated()` — migracije se u testovima ne izvršavaju.
- `dotnet ef` se pokreće preko `AppDbContextFactory`: oba projekta `src/EPiljara.Infrastructure`, `--configuration Release` (živi API drži Debug DLL-ove zaključane).
- Srpski/ijekavica u porukama; identifikatori grešaka `snake_case`.
- `npm run build` **samo** u zadnjem tasku i **samo** sa ugašenim dev serverom (dijeli `.next` sa njim).

## Pravila specifična za P3

- **Jedan put do `Order`-a.** Prije bilo kakve izmjene oko narudžbe pokreni
  `grep -rn "new Order" backend/src --include=*.cs` i svjesno odluči o svakom pogotku
  (`CreateOrder.cs` — kroz njega ide pretplata; `GuestOrderRequests.cs` — ne dira se;
  `DbSeeder.cs` — dobija primjer pretplate). P2b je ovo naučio na teži način.
- **Jedno mjesto koje broji isporuke.** `OrderStatus.Delivered` se postavlja na **dva**
  mjesta (`AdminOrders.cs:97`, `DriverFeature.cs:111`). Brojač pretplate ne smije biti
  prepisan u oba — ide u pomoćnik, kao `SnapshotKorpice`.
- **Posao mora smjeti da padne.** Svaki korak komande je `continue`, nikad `throw` koji
  zaustavi obradu ostalih pretplata.

---

## Struktura fajlova

**Backend**

| Fajl | Odgovornost |
|---|---|
| `Domain/Enums/Enums.cs` (izmjena) | `StatusPretplate` |
| `Domain/Entities/Pretplata.cs` (nov) | entitet + prelazi statusa |
| `Domain/Entities/Order.cs` (izmjena) | `PretplataId` |
| `Application/Features/Pretplate/PretplateOpcije.cs` (nov) | prag i popust iz konfiguracije |
| `Application/Features/Pretplate/PretplatnaCijena.cs` (nov) | jedino mjesto koje zna cjenovno pravilo |
| `Application/Features/Pretplate/PretplatneNarudzbe.cs` (nov) | komanda koju zove pozadinski posao |
| `Application/Features/Pretplate/BrojacIsporuka.cs` (nov) | `ZabiljeziIsporukuAsync` — jedino mjesto koje diže `BrojIsporuka` |
| `Application/Features/Pretplate/MojePretplate.cs` (nov) | upiti i komande kupca |
| `Application/Features/Pretplate/AdminPretplate.cs` (nov) | spisak za nabavku |
| `Application/Features/Orders/CreateOrder.cs` (izmjena) | `CustomerId`/`PretplataId` kao ulaz |
| `Application/Features/Admin/AdminOrders.cs` (izmjena) | poziv brojača |
| `Application/Features/Driver/DriverFeature.cs` (izmjena) | poziv brojača |
| `Infrastructure/BackgroundJobs/PretplatneNarudzbeService.cs` (nov) | 15-minutni prolaz |
| `Infrastructure/Persistence/AppDbContext.cs` (izmjena) | `DbSet<Pretplata>` + konfiguracija |
| `Infrastructure/Persistence/DbSeeder.cs` (izmjena) | jedna aktivna pretplata za Muju |
| `Api/Controllers/AccountController.cs` (izmjena) | šest endpointa |
| `Api/Controllers/AdminController.cs` (izmjena) | spisak pretplata |

**Frontend**

| Fajl | Odgovornost |
|---|---|
| `types/domain.ts` (izmjena) | `Pretplata`, `StatusPretplate` |
| `lib/api/contracts.ts` (izmjena) | ulazi za kreiranje/izmjenu/pauzu |
| `lib/api/endpoints.ts` (izmjena) | `accountApi.pretplate*` |
| `lib/api/hooks.ts` (izmjena) | `queryKeys.pretplate()` |
| `app/(shop)/moj-nalog/pretplate/page.tsx` (nov) | ekran |
| `components/nalog/pretplata-kartica.tsx` (nov) | kartica jedne pretplate |
| `components/nalog/pretplata-forma.tsx` (nov) | panel za novu/izmjenu |
| `components/nalog/nalog-nav.tsx` (izmjena) | otključava stavku |
| `app/(shop)/moj-nalog/page.tsx` (izmjena) | otključava pločicu |
| `app/(shop)/korpice/[slug]/page.tsx` (izmjena) | dugme „Pretplati se" |

---

### Task 1: Cjenovno pravilo — `StatusPretplate`, `PretplateOpcije`, `PretplatnaCijena`

Čiste funkcije prve, jer su jedino što se može dokazati bez baze.

**Files:**
- Modify: `backend/src/EPiljara.Domain/Enums/Enums.cs`
- Create: `backend/src/EPiljara.Application/Features/Pretplate/PretplateOpcije.cs`
- Create: `backend/src/EPiljara.Application/Features/Pretplate/PretplatnaCijena.cs`
- Create: `backend/tests/EPiljara.Application.Tests/PretplatnaCijenaTests.cs`

**Step 1:** `public enum StatusPretplate { Aktivna, Pauzirana, Otkazana }`.

**Step 2:** `PretplateOpcije` sa `Sekcija = "Pretplate"`, `PragPopusta = 5`,
`Popust = 0.10m`, `SatiUnaprijed = 48`, `MaksPauzaSedmica = 8`. Registruj u `Program.cs`
pored `KorpiceOpcije` (`services.Configure<PretplateOpcije>(cfg.GetSection(...))`).

**Step 3:** `PretplatnaCijena.Izracunaj(decimal punaCijena, int brojIsporuka, PretplateOpcije o)`:
puna dok je `brojIsporuka < o.PragPopusta`, inače `Math.Round(puna * (1 - o.Popust), 2,
MidpointRounding.ToEven)` — isto zaokruživanje kao svuda u backendu.

**Step 4 (testovi):** peta isporuka (`brojIsporuka == 4`) je puna; šesta (`== 5`) je
umanjena; `brojIsporuka == 0` je puna; negativna vrijednost se ponaša kao nula (brani od
pokvarenog reda u bazi, ne od korisnika).

**Verification:** `dotnet test --filter PretplatnaCijena` — zeleno.

---

### Task 2: Entitet `Pretplata`, `Order.PretplataId`, migracija

**Files:**
- Create: `backend/src/EPiljara.Domain/Entities/Pretplata.cs`
- Modify: `backend/src/EPiljara.Domain/Entities/Order.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `backend/src/EPiljara.Application/Common/Interfaces/IAppDbContext.cs`
- Create: migracija `DodajPretplate`
- Create: `backend/tests/EPiljara.Application.Tests/PretplataModelTests.cs`

**Step 1:** Entitet po tabeli iz speca. Prelazi kao statičko polje, po uzoru na
`SedmicniSadrzaj.Prelazi`:
`Aktivna → [Pauzirana, Otkazana]`, `Pauzirana → [Aktivna, Otkazana]`, `Otkazana → []`.
Metoda `Predji(StatusPretplate)` baca `ConflictException(..., "nedozvoljen_prelaz_pretplate")`
i upisuje `OtkazanaU` pri otkazu.

**Step 2:** `Order.PretplataId` (`Guid?`) sa XML komentarom da `null` znači „naručio čovjek".
Veza se **ne** pravi kao navigacija sa `Pretplata.Narudzbe` kolekcijom — brojanje ide upitom,
ne lijenim učitavanjem.

**Step 3:** `AppDbContext`: `DbSet<Pretplata> Pretplate`, konfiguracija u `OnModelCreating` —
`DeliveryAddress` max 400, `Napomena` max 1000, FK `Restrict` na `User`, `Product`,
`DeliveryZone`, `DeliverySlot`; filtrirani jedinstveni indeks
`(UserId, ProductId) WHERE "Status" <> 2`. Isti `DbSet` dodaj u `IAppDbContext`.

**Step 4:** Migracija kroz `AppDbContextFactory` (vidi Global Constraints). `PretplataId` na
`Orders` je nullable — bez `defaultValue` problema. `Status` i `BrojIsporuka` su NOT NULL sa
eksplicitnim `defaultValue: 0`.

**Step 5 (testovi):** dozvoljeni i nedozvoljeni prelazi; otkaz upisuje `OtkazanaU`; otkazana
ne može natrag u aktivnu.

**Verification:** `dotnet test --filter PretplataModel`; migracija se primjenjuje na dev bazu
bez greške (`dotnet ef database update`).

---

### Task 3: `CreateOrderHandler` prima vlasnika i pretplatu

Pretplata mora kroz postojeći put, a posao nema prijavljenog korisnika.

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs`
- Modify: `backend/tests/EPiljara.Application.Tests/CreateOrderKorpicaTests.cs` (dopuna)

**Step 1:** `CreateOrderCommand` dobija `Guid? CustomerId = null` i `Guid? PretplataId = null`
na kraj liste parametara (postojeći pozivaoci se ne diraju — svi koriste imenovane ili
pozicione argumente do `Longitude`).

**Step 2:** U handleru: `var customerId = cmd.CustomerId ?? (current.IsAuthenticated ? current.Id : null);`
Komentar mora reći **zašto** postoji ulaz: pozadinski posao nema `ICurrentUser`, a narudžba bez
vlasnika se ne bi pojavila pod „Moje narudžbe". `PretplataId` se samo prepisuje na `Order`.

**Step 3 (test):** komanda sa `CustomerId` koju šalje neautentifikovan pozivalac pravi narudžbu
vezanu za tog kupca; bez njega ponašanje ostaje kao prije (prijavljeni dobija svoju, gost nema).

**Verification:** `dotnet test --filter CreateOrder` — sve zeleno, ništa od postojećeg ne pada.

---

### Task 4: Komanda koja pravi pretplatne narudžbe

Srce P3. Piše se **testovima prvo** — prvi test je dupli prolaz, ne sretan slučaj.

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Pretplate/PretplatneNarudzbe.cs`
- Create: `backend/tests/EPiljara.Application.Tests/PretplatneNarudzbeTests.cs`

**Step 1:** `NapraviPretplatneNarudzbeCommand() : IRequest<int>` (vraća broj napravljenih).

**Step 2:** Handler, redoslijed provjera tačno kao u specu („Kako narudžba nastaje"):
status → ciljna sedmica (`SedmicaHelper.SedmicaZaNarudzbu`, isti izvor istine) → već
napravljena? → pauza (i gašenje istekle pauze) → prerano? (`SatiUnaprijed`) → sadržaj
objavljen/zaključan? → `CreateOrderCommand` preko `ISender` → upis `Poslednja*`.

**Step 3:** `Order` i upis `Poslednja*` u **istom** `SaveChangesAsync`. Pošto
`CreateOrderHandler` sam zove `SaveChangesAsync`, transakcija se otvara ovdje
(`db.Database.BeginTransactionAsync`) i obuhvata oba upisa — isti obrazac koji
`DeleteAddressHandler` već koristi za dva `SaveChanges`.

**Step 4:** Svaka pretplata se obrađuje u svom `try`; izuzetak se loguje i ide se dalje. Jedan
pokvaren red ne smije zaustaviti ostale.

**Step 5 (testovi):** dva prolaza → jedna narudžba; pauza preskače tačno N sedmica i vrati se
sama; neobjavljen sadržaj → nema narudžbe, pretplata ostaje `Aktivna`; otkazana ne pravi
ništa; prerano → ništa; napravljena narudžba nosi `PretplataId`, tačnu sedmicu i pretplatnu
cijenu kad je zaslužena.

**Verification:** `dotnet test --filter PretplatneNarudzbe`.

---

### Task 5: Brojač isporuka na jednom mjestu

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Pretplate/BrojacIsporuka.cs`
- Modify: `backend/src/EPiljara.Application/Features/Admin/AdminOrders.cs`
- Modify: `backend/src/EPiljara.Application/Features/Driver/DriverFeature.cs`
- Create: `backend/tests/EPiljara.Application.Tests/BrojacIsporukaTests.cs`

**Step 1:** `BrojacIsporuka.ZabiljeziAsync(IAppDbContext db, Order order, OrderStatus noviStatus, CancellationToken ct)`:
diže `BrojIsporuka` samo kad `order.PretplataId is not null`, kad je `noviStatus == Delivered`
i kad narudžba **već nije** bila `Delivered` (dvostruki poziv ne smije brojati dvaput).

**Step 2:** Poziv iz oba mjesta koja postavljaju status, **prije** `SaveChangesAsync`, da sve
ode u jednom upisu. U oba fajla komentar koji upućuje na pomoćnik i objašnjava da je razlog
postojanja pomoćnika to što su mjesta dva.

**Step 3 (testovi):** dizanje kroz admin put; dizanje kroz vozački put; ponovno postavljanje
`Delivered` ne diže drugi put; narudžba bez pretplate ne dira ništa.

**Verification:** `dotnet test --filter BrojacIsporuka` + `grep -rn "OrderStatus.Delivered" backend/src --include=*.cs`
pokazuje da su oba mjesta pokrivena.

---

### Task 6: Endpointi kupca

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Pretplate/MojePretplate.cs`
- Modify: `backend/src/EPiljara.Application/DTOs/AccountDtos.cs`
- Modify: `backend/src/EPiljara.Api/Controllers/AccountController.cs`
- Create: `backend/tests/EPiljara.Application.Tests/MojePretplateTests.cs`

**Step 1:** `PretplataDto`: paket (naziv, slug, tip, veličina), status, adresa, zona, termin,
`BrojIsporuka`, `TrenutnaCijena`, `PunaCijena`, `PopustAktivan`, `DoPopusta` (koliko još
isporuka), `SljedecaIsporuka` (godina, sedmica, datum) i `PauzaDo`.

**Step 2:** Upit `GetMojePretplateQuery` — sortirano: aktivne, pa pauzirane, pa otkazane;
unutar grupe po `CreatedAt` opadajuće.

**Step 3:** Komande `Napravi`, `Izmijeni`, `Pauziraj(sedmica 1..8)`, `Nastavi`, `Otkazi`.
Validacije: korpica mora biti `Vrsta == Korpica` i `IsAvailable`; zona aktivna; slot postoji;
adresa 1–400; tačka po `AddressRules` pravilima (isti `GeoBounds`, iste poruke pod ključem
`deliveryAddress`); pauza 1–8.

**Step 4:** Vlasništvo se provjerava **poslije** dohvata, kao u `UpdateAddressHandler` —
razlika između „ne postoji" i „nije tvoja" ostaje namjerna.

**Step 5 (testovi):** druga pretplata na istu korpicu → `ConflictException`
(`pretplata_vec_postoji`); otkazana pa nova na istu korpicu → prolazi; tuđa pretplata → 403;
pauza izvan 1–8 → 400; izmjena paketa mijenja cijenu na DTO-u.

**Verification:** `dotnet test --filter MojePretplate`.

---

### Task 7: Pozadinski posao i admin spisak

**Files:**
- Create: `backend/src/EPiljara.Infrastructure/BackgroundJobs/PretplatneNarudzbeService.cs`
- Modify: `backend/src/EPiljara.Api/Program.cs` (registracija)
- Create: `backend/src/EPiljara.Application/Features/Pretplate/AdminPretplate.cs`
- Modify: `backend/src/EPiljara.Api/Controllers/AdminController.cs`

**Step 1:** Servis je doslovna kopija obrasca `ZakljucavanjeSadrzajaService` (15 min, guard
oko prolaza, guard oko `Task.Delay`). Loguje samo kad je broj > 0.

**Step 2:** `GET /admin/pretplate?status=` — spisak sa kupcem, paketom, brojem isporuka i
sedmicom posljednje narudžbe. Plus brojač „koliko aktivnih pretplata čeka sadržaj za narednu
sedmicu" (R2).

**Verification:** API se diže, `/swagger` pokazuje nove rute, posao se ne žali u logu.

---

### Task 8: Frontend — tipovi i API sloj

**Files:**
- Modify: `frontend/src/types/domain.ts`
- Modify: `frontend/src/lib/api/contracts.ts`
- Modify: `frontend/src/lib/api/endpoints.ts`
- Modify: `frontend/src/lib/api/hooks.ts`

**Step 1:** `Pretplata` tip sa `?:` za svako nullable backend polje (JSON izostavlja null).

**Step 2:** `accountApi.pretplate()`, `.dodajPretplatu()`, `.izmijeniPretplatu()`,
`.pauzirajPretplatu()`, `.nastaviPretplatu()`, `.otkaziPretplatu()` — isti oblik kao
`accountApi` za adrese, uključujući 204 bez tijela na otkazu.

**Verification:** `npx tsc --noEmit`.

---

### Task 9: Frontend — ekran `/moj-nalog/pretplate`

**Files:**
- Create: `frontend/src/app/(shop)/moj-nalog/pretplate/page.tsx`
- Create: `frontend/src/components/nalog/pretplata-kartica.tsx`
- Create: `frontend/src/components/nalog/pretplata-forma.tsx`
- Modify: `frontend/src/components/nalog/nalog-nav.tsx`
- Modify: `frontend/src/app/(shop)/moj-nalog/page.tsx`

**Step 1:** Ekran prati `adrese/page.tsx`: `isLoading` → `LoadingBlock`, `isError` → poseban
prazan ekran sa „Pokušaj ponovo" (ne tiho prazno stanje), prazno → poziv na `/korpice`.

**Step 2:** Kartica: paket, status, sljedeća isporuka, cijena (puna precrtana kad je popust
aktivan), traka „još N isporuka do niže cijene", dugmad. Potvrda otkaza je **inline**, nikad
`window.confirm` (zamrzava automatizaciju koja provjerava ekran).

**Step 3:** Forma koristi `AddressPicker` i `pogodiZonu` isto kao `adresa-forma.tsx` (P2c), i
montira se sa `key`-em — birač čita `value`/`initialCoordinates` na mountu.

**Step 4:** Navigacija: skini `uskoro` sa stavke i sa pločice na `/moj-nalog`.

**Verification:** `npx tsc --noEmit`, `npm run lint`.

---

### Task 10: Pretplata sa stranice korpice

**Files:**
- Modify: `frontend/src/app/(shop)/korpice/[slug]/page.tsx`

**Step 1:** Dugme „Pretplati se" pored „Dodaj u korpu". Neprijavljenom vodi na
`/login?next=`, prijavljenom otvara formu sa već izabranom korpicom.

**Step 2:** Cijena na stranici dobija red „sa pretplatom od šeste isporuke: X KM" — to je
jedini razlog da se kupac pretplati i mora stajati gdje se odlučuje.

**Verification:** `npx tsc --noEmit`, `npm run lint`.

---

### Task 11: Seed, dokumentacija, završna provjera

**Files:**
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/DbSeeder.cs`
- Modify: `CLAUDE.md`
- Modify: `docs/superpowers/STANJE.md`
- Modify: `PRODUCTION.md` (nova konfiguraciona sekcija „Pretplate")

**Step 1:** Seeder pravi jednu aktivnu pretplatu za `mujo@example.com` na malu domaćinsku
korpicu, sa `BrojIsporuka = 4` — jedan klik od popusta, da se ekran vidi u oba stanja.

**Step 2:** `CLAUDE.md` dobija sekciju „Pretplate (P3)": jedan put do `Order`-a, idempotentnost
kroz `Poslednja*`, brojač na dva mjesta kroz jedan pomoćnik, popust koji se zarađuje unazad.

**Step 3:** Puna provjera: `dotnet test`, `npx tsc --noEmit`, `npm run lint`, pa sa **ugašenim**
dev serverom i obrisanim `.next` — `npm run build`. Zatim ručna provjera kroz ekran:
pretplata, pauza, nastavak, otkaz, i jedan prolaz posla koji stvarno napravi narudžbu.

**Verification:** sve gore zeleno; `git status` čist; grana spremna za merge u `main`.
