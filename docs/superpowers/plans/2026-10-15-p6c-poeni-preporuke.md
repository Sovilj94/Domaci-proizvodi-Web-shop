# P6c Poeni i preporuke — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kupac sa nalogom zarađuje poene za isporučene narudžbe, troši ih kao popust na checkoutu, i dobija poene za preporuku prijatelja.

**Architecture:** Knjiga stavki `PoeniStavka` (stanje = zbir), jedna klasa pravila `PoeniPravila` i jedan pomoćnik za promjenu statusa narudžbe koji zovu admin i vozač (kao `BrojacIsporuka`). Trošenje ide kroz `CreateOrderHandler` poslije promo popusta iz P6b; `Order` dobija `IskoristeniPoeni`/`PoeniPopust`.

**Tech Stack:** ASP.NET Core 9, EF Core 9 (Postgres; testovi SQLite in-memory), MediatR, FluentValidation, xUnit; Next.js 15, React Query, Tailwind.

**Spec:** `docs/superpowers/specs/2026-10-15-p6c-poeni-preporuke-design.md`

## Global Constraints

- Radi se samo u `C:/Users/soviljn/Desktop/epiljara-backend-wt`, grana `p6c-poeni`. Nikad „Kumov Web Shop".
- Srpske poruke i komentari (ijekavica, latinica), komentari objašnjavaju „zašto".
- Enumi na žici kao PascalCase stringovi.
- Konstante: `PoenaPoKM = 1`, `VrijednostPoenaKM = 0.05m`, `NagradaPreporuka = 100`, u `PoeniOpcije` (`Sekcija = "Poeni"`), registrovane u `Program.cs` kao `PretplateOpcije`; `appsettings.json` se ne dira.
- `TotalAmount = Subtotal − Popust − PoeniPopust + DeliveryFee`. Poeni pokrivaju samo robu poslije promo popusta, u cijelim KM.
- Gost nikad ne zarađuje ni ne troši poene.
- Testovi: `cd backend && dotnet test --configuration Release`. Frontend: `cd frontend && npx tsc --noEmit && npm run lint`; build: `rm -rf .next && npm run build && rm -rf .next`.
- `dotnet ef migrations add DodajPoene --project src/EPiljara.Infrastructure --startup-project src/EPiljara.Infrastructure --configuration Release` iz `backend/`. Ne primjenjivati na bazu. Ne pokretati API ni dev server. Python nije instaliran.
- Commit trailer: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. Admin postavi `Delivered` dvaput (isti status ponovo) — poeni se ne smiju upisati dvaput (Task 1 test).
2. Otkaz narudžbe sa potrošenim poenima vraća poene tačno jednom, i kad se otkaže iz `FailedDelivery` (Task 1 test).
3. Kupac sa 30 poena (1,50 KM) — troši 20 poena za 1 KM, ostaje 10 (Task 1 test `ZaTrosenje`).
4. Promo kod pokrije skoro svu robu — poeni ne smiju preći ostatak robe niti dirati dostavu (Task 2 test).
5. Posao pretplata pravi narudžbu sa `CustomerId` — `IskoristiPoene` je false, ništa se ne troši (Task 2 test).

---

### Task 1: Domen, migracija, pravila i pomoćnik statusa

**Files:**
- Create: `backend/src/EPiljara.Domain/Enums/VrstaPoena.cs`, `backend/src/EPiljara.Domain/Entities/PoeniStavka.cs`
- Modify: `backend/src/EPiljara.Domain/Entities/User.cs`, `Order.cs`
- Modify: `IAppDbContext.cs`, `AppDbContext.cs`
- Create: `backend/src/EPiljara.Application/Features/Poeni/PoeniOpcije.cs`, `PoeniPravila.cs`
- Modify: `backend/src/EPiljara.API/Program.cs` (Configure<PoeniOpcije>)
- Modify: `backend/src/EPiljara.Application/Features/Admin/AdminOrders.cs` (`UpdateOrderStatusHandler`), `backend/src/EPiljara.Application/Features/Driver/DriverFeature.cs` (mjesto gdje se zove `BrojacIsporuka.ZabiljeziAsync`)
- Create: migracija `DodajPoene`
- Test: `backend/tests/EPiljara.Application.Tests/PoeniPravilaTests.cs`; popravi postojeće testove koji konstruišu izmijenjene handlere (`Options.Create(new PoeniOpcije())`)

**Interfaces — Produces:**
- `enum VrstaPoena { Isporuka, Iskoristeno, Povrat, PreporukaPreporucio, PreporukaNovi, Korekcija }`
- `PoeniStavka { UserId, Iznos (int), Vrsta, OrderId?, PreporuceniId?, Kreirana }`, `IAppDbContext.PoeniStavke`
- `User.PreporukaKod` (string?, max 8, jedinstven filtrirano), `User.PreporucioId` (Guid?, FK self `SetNull`)
- `Order.IskoristeniPoeni` (int), `Order.PoeniPopust` (decimal 10,2)
- `PoeniOpcije { const Sekcija = "Poeni"; int PoenaPoKM = 1; decimal VrijednostPoenaKM = 0.05m; int NagradaPreporuka = 100 }`
- `static class PoeniPravila`:
  - `Task<int> StanjeAsync(IAppDbContext db, Guid userId, CancellationToken ct)` — `SumAsync(s => s.Iznos)` (0 kad nema stavki)
  - `(int Poena, decimal Popust) ZaTrosenje(int stanje, decimal robaPoslijePromo, PoeniOpcije o)`
  - `int ZaradaZaNarudzbu(Order order, PoeniOpcije o)`
  - `Task ZabiljeziPromjenuStatusaAsync(IAppDbContext db, Order order, OrderStatus novi, PoeniOpcije o, CancellationToken ct)`

- [ ] **Step 1: Entiteti i DbContext.** `PoeniStavka` sa XML komentarom (knjiga: stanje je zbir, nikad se ne mijenja ni briše stavka). Konfiguracija: `Vrsta` `HasConversion<string>().HasMaxLength(24)`; indeksi `UserId`, `OrderId`; FK `UserId → User` `Cascade`, `OrderId → Order` `SetNull`, `PreporuceniId` bez FK (samo podatak; komentar zašto: brisanje preporučenog ne smije brisati tuđu nagradu). `User.PreporukaKod` `HasMaxLength(8)` + `HasIndex(...).IsUnique().HasFilter("\"PreporukaKod\" IS NOT NULL")` — provjeri kako je filtriran indeks napisan za `Utisak.OcjenaId` u istom fajlu i uradi isto (SQLite testovi moraju proći). `User.PreporucioId` → `HasOne<User>().WithMany().HasForeignKey(u => u.PreporucioId).OnDelete(DeleteBehavior.SetNull)`. `Order.PoeniPopust` `HasPrecision(10,2)`.

- [ ] **Step 2: Testovi** (`PoeniPravilaTests`, SQLite `TestDb` kao u `OcjeneTests`):
  - `ZaTrosenje(0, 50, o)` → (0, 0); `(-40, 50)` → (0, 0); `(30, 50)` → (20, 1); `(1000, 50)` → (1000, 50); `(1000, 12.70m)` → (240, 12); `(19, 50)` → (0, 0).
  - `ZaradaZaNarudzbu`: Subtotal 33.90, Popust 5, PoeniPopust 2, DeliveryFee 4 → 26; Subtotal 0.99 → 0.
  - Hook `Delivered` (stari status `OutForDelivery`, `CustomerId` postoji): upisana jedna `Isporuka` sa zaradom i `OrderId`; drugi poziv sa narudžbom već `Delivered` → ništa novo; drugi poziv kad je stari status ponovo `OutForDelivery` (simulacija, npr. ponovni zahtjev) a `Isporuka` za tu narudžbu postoji → ništa novo.
  - Gost (`CustomerId` null) → ništa.
  - Zarada 0 → nema stavke `Isporuka`.
  - Preporuka: kupac sa `PreporucioId = A`: prva isporuka → `PreporukaNovi` +100 kupcu i `PreporukaPreporucio` +100 A sa `PreporuceniId` = kupac; druga isporučena narudžba istog kupca → nema novih stavki preporuke.
  - Preporučilac obrisan (`PreporucioId` pokazuje na nepostojećeg) → kupac i dalje dobije `PreporukaNovi`? **Ne:** nagrada se daje samo kad preporučilac postoji (provjera `Users.AnyAsync`); test da nema nijedne stavke preporuke i da nema izuzetka.
  - `Cancelled` iz `Pending` sa `IskoristeniPoeni = 40` → `Povrat` +40 sa `OrderId`; ponovni poziv → ništa; iz `FailedDelivery` isto radi; `IskoristeniPoeni = 0` → ništa.
  - `StanjeAsync` sabira (+100, −40, +40) → 100; bez stavki → 0.
  - Integracija: `UpdateOrderStatusHandler` za `Delivered` upiše `Isporuka` (jedan test kroz handler).

- [ ] **Step 3: FAIL, implementacija.**

```csharp
public static (int Poena, decimal Popust) ZaTrosenje(int stanje, decimal robaPoslijePromo, PoeniOpcije o)
{
    if (stanje <= 0 || robaPoslijePromo <= 0) return (0, 0m);
    // Cijele KM: kupac vidi „−12 KM", ne „−12,35 KM", a poeni se troše u okruglim blokovima.
    var popust = Math.Floor(Math.Min(stanje * o.VrijednostPoenaKM, robaPoslijePromo));
    var poena = (int)(popust / o.VrijednostPoenaKM);
    return (poena, popust);
}

public static int ZaradaZaNarudzbu(Order order, PoeniOpcije o) =>
    (int)Math.Floor(Math.Max(0, order.Subtotal - order.Popust - order.PoeniPopust)) * o.PoenaPoKM;
```

`ZabiljeziPromjenuStatusaAsync`: `if (order.Status == novi) return;` zatim grane iz speca. Duplikat se provjerava upitom (`PoeniStavke.AnyAsync(s => s.OrderId == order.Id && s.Vrsta == ...)`) jer pozivalac još nije snimio. Stavke se samo `Add`-uju — pozivalac radi `SaveChangesAsync`. Za preporuku: učitaj kupca (`Users.FindAsync`/`FirstOrDefaultAsync`), provjeri `PreporucioId`, da `PreporukaNovi` za kupca ne postoji, i da preporučilac postoji.

Pozivi: u `UpdateOrderStatusHandler` odmah poslije `BrojacIsporuka.ZabiljeziAsync(...)` (prije `TransitionTo`), isto u `DriverFeature`. Handleri dobijaju `IOptions<PoeniOpcije>` u konstruktoru. **Pažnja (DriverFeature):** vozač može provesti narudžbu kroz `Packing`/`OutForDelivery` prije konačnog statusa — pozovi pomoćnik sa konačnim statusom prije tih prelaza, sa `order.Status` kakav je u tom trenutku (kao što radi `BrojacIsporuka`); pročitaj kod oko linije 110 i postupi isto.

- [ ] **Step 4: Migracija** `DodajPoene`; pregledaj `Up` (tabela `PoeniStavke`, kolone na `Users` i `Orders` sa defaultom 0, indeksi, FK). Ne primjenjuj.

- [ ] **Step 5: Cijeli suite PASS; commit** `feat(p6c): poeni — knjiga, pravila i upis pri isporuci i otkazu`

---

### Task 2: Backend — nalog, registracija i trošenje na narudžbi

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Poeni/MojiPoeni.cs`, `backend/src/EPiljara.Application/DTOs/PoeniDtos.cs`
- Modify: `backend/src/EPiljara.API/Controllers/AccountController.cs` (ili gdje su `/account/*` rute — provjeri)
- Modify: `backend/src/EPiljara.Application/Features/Auth/AuthCommands.cs` (`RegisterCommand`, handler, validator)
- Modify: `Features/Orders/CreateOrder.cs` (`CreateOrderRequest`, `CreateOrderCommand`, handler), `DTOs/OrderDtos.cs`, `Mapping/MappingExtensions.cs`
- Modify: `Infrastructure/Services/OrderEmailTemplate.cs`, `ViberMessage.cs`
- Test: `MojiPoeniTests.cs`, `PoeniNarudzbeTests.cs`, dopuna registracije (postojeći auth testovi ako ih ima — `grep -rl RegisterHandler backend/tests`)

**Interfaces:**
- Consumes: Task 1 (`PoeniPravila`, `PoeniOpcije`, `PoeniStavka`, `VrstaPoena`, polja na `User`/`Order`).
- Produces: `GET /account/poeni` → `MojiPoeniDto { Stanje, VrijednostPoenaKM, VrijednostKM, NagradaPreporuka, PreporukaKod, BrojPreporuka, Istorija: PoeniStavkaDto[] }`, `PoeniStavkaDto { Iznos, Vrsta, Opis, OrderCode?, Kreirana }`; `RegisterCommand.PreporukaKod` (string?, posljednji, default null); `CreateOrderRequest.IskoristiPoene` / `CreateOrderCommand.IskoristiPoene` (bool, default false, posljednji); `OrderDto.IskoristeniPoeni` (int), `OrderDto.PoeniPopust` (decimal).

- [ ] **Step 1: Testovi `MojiPoeniTests`:** stanje i `VrijednostKM = stanje × 0.05` (negativno stanje → `VrijednostKM` 0); prvi poziv pravi kod od 8 znakova iz abecede `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`, drugi vraća isti; istorija najnovije prvo, max 50, `Opis` po vrsti tačno kao u specu (sa kodom narudžbe kad postoji `OrderId`; za `PreporukaPreporucio` ime preporučenog u obliku „Ime P." — vidi kako P6a pravi „Ime P." u `Features/Ocjene/Ocjene.cs` i iskoristi isti pomoćnik ako je dostupan, inače ga izvuci u zajedničko mjesto bez mijenjanja ponašanja); `BrojPreporuka` = broj korisnika sa `PreporucioId == ja`.

- [ ] **Step 2: Implementacija `MojiPoeni.cs`** — `GetMojiPoeniQuery : IRequest<MojiPoeniDto>`, handler (`IAppDbContext`, `ICurrentUser`, `IOptions<PoeniOpcije>`); neprijavljen → `UnauthorizedException` ili obrazac koji koriste ostali `/account` upiti (provjeri `MojePretplate.cs`). Generisanje koda: `RandomNumberGenerator.GetInt32` nad abecedom, do 5 pokušaja protiv `Users.AnyAsync(u => u.PreporukaKod == kod)`, pa `SaveChangesAsync`. Ruta `GET account/poeni` `[Authorize]`.

- [ ] **Step 3: Registracija** — `RegisterCommand` dobija `string? PreporukaKod = null`; handler: ako nije prazan, `Trim().ToUpperInvariant()`, nađi korisnika sa tim kodom, upiši `PreporucioId`; nepoznat → tiho ignoriši (komentar O10). Validator: `MaximumLength(8)` kad nije null → „Kod preporuke je predugačak.". Testovi: važeći kod → `PreporucioId`; nepoznat → registracija uspije bez njega; malim slovima → prepoznat.

- [ ] **Step 4: Testovi `PoeniNarudzbeTests`** (postavka kao `PromoKodNarudzbeTests`): kupac sa 300 poena, roba 20, dostava 3, `IskoristiPoene = true` → `IskoristeniPoeni 300`, `PoeniPopust 15`, `TotalAmount 8`, stavka `Iskoristeno −300` sa `OrderId`; sa 1000 poena i robom 20 → 400 poena, 20 KM; sa promo kodom 5 KM i 1000 poena na robi 20 → poeni 15 KM (300 poena), `TotalAmount = 20 − 5 − 15 + 3`; bez checkboxa → ništa; stanje 0 → ništa i bez stavke; komanda sa `CustomerId` (posao pretplata) i `IskoristiPoene` false → ništa; `OrderDto` nosi oba polja; nevažeći promo kod → ništa upisano, ni stavka poena.

- [ ] **Step 5: Implementacija u `CreateOrderHandler`** — poslije promo bloka:

```csharp
// Poeni poslije promo koda (O5) i samo za prijavljenog kupca — gost nema knjigu poena.
var iskoristeniPoeni = 0;
var poeniPopust = 0m;
if (cmd.IskoristiPoene && customerId is { } kupacId)
{
    var stanje = await PoeniPravila.StanjeAsync(db, kupacId, ct);
    (iskoristeniPoeni, poeniPopust) = PoeniPravila.ZaTrosenje(stanje, subtotal - popust, poeniOpcije.Value);
}
```
`Order`: `IskoristeniPoeni`, `PoeniPopust`, `TotalAmount = Math.Round(subtotal - popust - poeniPopust + zone.DeliveryFee, 2)`. Ako `iskoristeniPoeni > 0`: `db.PoeniStavke.Add(new PoeniStavka { UserId = kupacId, Iznos = -iskoristeniPoeni, Vrsta = VrstaPoena.Iskoristeno, Order = order })` — ili postavi `OrderId = order.Id` (Id se generiše u `Entity` konstruktoru? provjeri `Entity`) — u istom `SaveChangesAsync`. `CreateOrderRequest` i `ToCommand` prenose `IskoristiPoene`.

- [ ] **Step 6: DTO, mapiranje, mejl i Viber** — `OrderDto.IskoristeniPoeni/PoeniPopust`; u `OrderEmailTemplate` i `ViberMessage` red „Poeni ({N}): −{iznos}" odmah ispod reda promo popusta, istog stila, samo kad `PoeniPopust > 0`; test po šablonu (sa i bez).

- [ ] **Step 7: Cijeli suite PASS, commit** `feat(p6c): moji poeni, preporuka pri registraciji i trošenje poena na narudžbi`

---

### Task 3: Frontend — stranica Poeni, navigacija, registracija

**Files:**
- Create: `frontend/src/app/(shop)/moj-nalog/poeni/page.tsx`
- Modify: `frontend/src/components/nalog/nalog-nav.tsx`, `frontend/src/app/(shop)/register/page.tsx`, `frontend/src/lib/api/endpoints.ts`, `contracts.ts`, `hooks.ts`, `frontend/src/types/domain.ts`

**Interfaces:**
- Consumes: `GET /account/poeni` (camelCase: `stanje, vrijednostPoenaKM, vrijednostKM, nagradaPreporuka, preporukaKod, brojPreporuka, istorija[{ iznos, vrsta, opis, orderCode, kreirana }]`); `POST /auth/register` + `preporukaKod`.
- Produces: `MojiPoeni` tip, `accountApi.poeni()` (ili ime po postojećem obrascu), hook `useMojiPoeni(enabled)` sa `queryKeys.mojiPoeni()` — Task 4 ga koristi.

- [ ] **Step 1: Tipovi, API, hook** po obrascu postojećih `/account/*` poziva.
- [ ] **Step 2: Stranica `/moj-nalog/poeni`** — `h1` „Poeni"; kartica stanja (veliki broj poena, „= {formatPrice(vrijednostKM)}"); kratko „kako funkcioniše" iz brojeva sa servera: „1 poen za svaku KM robe na isporučenoj narudžbi · 20 poena = 1 KM popusta" (izvedi 20 kao `Math.round(1 / vrijednostPoenaKM)`), „Poene trošiš na checkoutu."; kartica preporuke: link `${window.location.origin}/register?preporuka=${kod}` u read-only polju + dugme „Kopiraj" (`navigator.clipboard.writeText`, toast „Link je kopiran", u try/catch), tekst „Kad prijatelj primi prvu isporuku, oboje dobijate po {nagradaPreporuka} poena." i „Preporučio si: {brojPreporuka}"; istorija (lista: opis, datum `dd.MM.yyyy.`, iznos zeleno `+N` / sivo `−N`), prazno stanje „Još nema poena. Prvi stižu sa prvom isporukom.". Učitavanje i greška kao na `/moj-nalog/pretplate`. Obraćanje „ti" kao ostale stranice naloga.
- [ ] **Step 3: Navigacija** — `{ href: "/moj-nalog/poeni", label: "Poeni", icon: Coins }` iza „Pretplate".
- [ ] **Step 4: Registracija** — `useSearchParams()` za `preporuka` (pazi: stranica mora biti u `Suspense` granici ako već nije — `npm run build` to provjerava); kad postoji, šalje `preporukaKod` i iznad forme prikazuje napomenu „Preporučio te prijatelj — kad primiš prvu isporuku, dobijaš poene na nalogu.".
- [ ] **Step 5: tsc, lint, build; commit** `feat(p6c): stranica poena, preporuka pri registraciji`

---

### Task 4: Frontend — poeni na checkoutu i na prikazima narudžbe

**Files:**
- Modify: `frontend/src/app/(shop)/checkout/page.tsx`, `frontend/src/components/shop/order-summary-panel.tsx`, `order-receipt.tsx`, `frontend/src/components/admin/order-card.tsx`, `frontend/src/types/domain.ts`, `contracts.ts`, `frontend/src/server/mock/store.ts` (ako tsc traži)

**Interfaces:**
- Consumes: Task 3 `useMojiPoeni`; `OrderDto.iskoristeniPoeni/poeniPopust`; `CreateOrderRequest.iskoristiPoene`.

- [ ] **Step 1: Tipovi** — `Order.iskoristeniPoeni: number; poeniPopust: number`; `CreateOrderRequest.iskoristiPoene?: boolean`.
- [ ] **Step 2: Checkout** — samo za prijavljenog: `const poeni = useMojiPoeni(!!user)`; `const [koristiPoene, setKoristiPoene] = useState(false)`. Prikaz popusta (O14, ista formula kao server, komentar koji to kaže i upućuje na `PoeniPravila.ZaTrosenje`): `roba = subtotal - popust`; `popustPoena = stanje > 0 && roba > 0 ? Math.floor(Math.min(stanje * v, roba)) : 0`; `poenaZaTrosenje = Math.round(popustPoena / v)`. U koraku 3, ispod promo polja, kad `stanje > 0`: checkbox „Iskoristi poene ({poenaZaTrosenje} poena = −{formatPrice(popustPoena)})"; ako je `popustPoena === 0` (premalo poena), umjesto checkboxa tekst „Imaš {stanje} poena — treba ti bar {Math.round(1/v)} za popust od 1 KM.". `total` oduzima `koristiPoene ? popustPoena : 0`. Body: `iskoristiPoene: koristiPoene && popustPoena > 0` (samo za `orderApi.create`, ne za gosta). Poslije uspješne narudžbe invalidiraj `queryKeys.mojiPoeni()`.
- [ ] **Step 3: Prikazi** — `OrderSummaryPanel` dobija opcione `poeniPopust?`, `iskoristeniPoeni?` i red „Poeni ({N})" −X ispod reda promo popusta, kad `poeniPopust > 0`; isto u `OrderReceipt` i `order-card.tsx` iz `order.poeniPopust`/`order.iskoristeniPoeni`.
- [ ] **Step 4: tsc, lint, build; commit** `feat(p6c): poeni na checkoutu i na prikazima narudžbe`
