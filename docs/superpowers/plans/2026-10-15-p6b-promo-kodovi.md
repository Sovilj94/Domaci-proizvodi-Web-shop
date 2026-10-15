# P6b Promo kodovi — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin pravi promo kodove, kupac (prijavljen ili gost) ih primjenjuje na checkoutu, narudžba pamti kod i popust, a popust se vidi na svakom prikazu iznosa.

**Architecture:** Entitet `PromoKod` + jedna statička klasa pravila `PromoKodPravila` koju zovu provjera, `CreateOrderHandler`, `CreateGuestOrderRequestHandler` i `ConfirmGuestOrderHandler`. `Order`/`GuestOrderRequest` dobijaju snimak `PromoKodId`/`PromoKod`/`Popust`; `TotalAmount = Subtotal − Popust + DeliveryFee`. Frontend pita server za popust (`POST /promo-kodovi/provjera`), ne računa ga.

**Tech Stack:** ASP.NET Core 9, EF Core 9 (Postgres; testovi SQLite in-memory), MediatR, FluentValidation, xUnit; Next.js 15, React Query, Tailwind.

**Spec:** `docs/superpowers/specs/2026-10-15-p6b-promo-kodovi-design.md`

## Global Constraints

- Radi se samo u `C:/Users/soviljn/Desktop/epiljara-backend-wt`, grana `p6b-promo`.
- Sve poruke korisniku na srpskom (ijekavica, latinica). Svako validator pravilo ima `.WithMessage(...)` na srpskom.
- Enumi idu na žicu kao PascalCase stringovi (`ToString()`); ulaz je string provjeren sa `Enum.TryParse` + `Enum.IsDefined`.
- Greške: `DomainException(poruka, "promo_kod_nevazeci")` (400), `ConflictException(..., "promo_kod_postoji" | "promo_kod_koristen")` (409), `NotFoundException` (404).
- Popust samo na robu, nikad > `Subtotal`; minimum zone i koda se porede sa `Subtotal` prije popusta.
- Otkazane (`OrderStatus.Cancelled`) narudžbe ne troše kod.
- Testovi: `cd backend && dotnet test --configuration Release` (Release jer API može da drži Debug DLL-ove). Frontend: `cd frontend && npx tsc --noEmit && npm run lint`.
- `dotnet ef`: `dotnet ef migrations add <Ime> --project src/EPiljara.Infrastructure --startup-project src/EPiljara.Infrastructure --configuration Release` iz `backend/`.
- NE pokretati API ni Next dev server (sistem ih gasi zbog memorije). NE primjenjivati migraciju na dev bazu (to radi kontroler na kraju).
- Commit trailer: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Komentari u kodu na srpskom, objašnjavaju „zašto", u stilu okolnog koda.

## Review Focus

1. Kod unesen malim slovima i sa razmacima („ dobrodosli ") — mora se prepoznati (Task 1 test).
2. Otkazana narudžba ne smije potrošiti „jednom po kupcu" (Task 1 test).
3. Gost pošalje dva zahtjeva sa istim e-mailom i kodom „jednom po kupcu", potvrdi oba — druga potvrda mora pasti sa 409 i obrisati zahtjev (Task 3 test).
4. Prijavljeni kupac pošalje `customerId` drugog korisnika u tijelu `POST /orders` — narudžba mora ostati na njegovom nalogu (Task 3 test kroz `CreateOrderRequest.ToCommand`).
5. Korpa se promijeni poslije primjene koda ispod minimuma koda — checkout mora pokazati grešku, ne stari popust (Task 4: query ključ uključuje stavke).

---

### Task 1: Domen, migracija i pravila

**Files:**
- Create: `backend/src/EPiljara.Domain/Enums/TipPopusta.cs`
- Create: `backend/src/EPiljara.Domain/Entities/PromoKod.cs`
- Modify: `backend/src/EPiljara.Domain/Entities/Order.cs`, `GuestOrderRequest.cs`
- Modify: `backend/src/EPiljara.Application/Common/Interfaces/IAppDbContext.cs`, `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs`
- Create: `backend/src/EPiljara.Application/Common/LokalniDatum.cs`
- Create: `backend/src/EPiljara.Application/Features/PromoKodovi/PromoKodPravila.cs`
- Create: migracija `DodajPromoKodove`
- Test: `backend/tests/EPiljara.Application.Tests/PromoKodPravilaTests.cs`

**Interfaces:**
- Produces: `PromoKod` entitet, `TipPopusta { Procenat, Iznos }`, `IAppDbContext.PromoKodovi`, `Order.PromoKodId/PromoKod/Popust`, `GuestOrderRequest.PromoKodId/PromoKod/Popust`, `PromoKodPravila.Normalizuj/Popust/ProvjeriAsync`, `KupacZaPromo`, `LokalniDatum.Danas()`.

- [ ] **Step 1: Entitet i enum**

```csharp
// Domain/Enums/TipPopusta.cs
namespace EPiljara.Domain.Enums;
public enum TipPopusta { Procenat, Iznos }
```

```csharp
// Domain/Entities/PromoKod.cs
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Kod za popust na robu (nikad na dostavu). Pravila primjene stoje samo u
/// <c>PromoKodPravila</c>; ovdje su samo podaci. Kod se poslije kreiranja ne mijenja —
/// narudžbe ga pamte kao snimak teksta.
/// </summary>
public class PromoKod : Entity
{
    public string Kod { get; set; } = string.Empty;
    public string Opis { get; set; } = string.Empty;
    public TipPopusta Tip { get; set; }
    public decimal Vrijednost { get; set; }
    public decimal? MinIznos { get; set; }
    public DateOnly? VaziOd { get; set; }
    public DateOnly? VaziDo { get; set; }
    public int? MaxKoristenja { get; set; }
    public bool JednomPoKupcu { get; set; }
    public bool SamoNoviKupci { get; set; }
    public bool Aktivan { get; set; } = true;
    public DateTime Kreiran { get; set; } = DateTime.UtcNow;
}
```

`Order` i `GuestOrderRequest` dobijaju (uz XML komentar: snimak — prikaz ne zavisi od tabele kodova; `Popust` je već uračunat u `TotalAmount`):

```csharp
public Guid? PromoKodId { get; set; }
public string? PromoKod { get; set; }
public decimal Popust { get; set; }
```

- [ ] **Step 2: DbContext**

`IAppDbContext`: `DbSet<PromoKod> PromoKodovi { get; }`. `AppDbContext`: `public DbSet<PromoKod> PromoKodovi => Set<PromoKod>();` i konfiguracija:

```csharp
b.Entity<PromoKod>(e =>
{
    e.Property(x => x.Kod).HasMaxLength(30).IsRequired();
    e.HasIndex(x => x.Kod).IsUnique();
    e.Property(x => x.Opis).HasMaxLength(200).IsRequired();
    e.Property(x => x.Tip).HasConversion<string>().HasMaxLength(16);
    e.Property(x => x.Vrijednost).HasPrecision(10, 2);
    e.Property(x => x.MinIznos).HasPrecision(10, 2);
});
```

U konfiguraciji `Order`: `Popust` `HasPrecision(10, 2)`, `PromoKod` `HasMaxLength(30)`, `HasOne<PromoKod>().WithMany().HasForeignKey(o => o.PromoKodId).OnDelete(DeleteBehavior.Restrict)`, `HasIndex(o => o.PromoKodId)`. U konfiguraciji `GuestOrderRequest`: isto ali `OnDelete(DeleteBehavior.SetNull)`.

- [ ] **Step 3: LokalniDatum**

```csharp
namespace EPiljara.Application.Common;

/// <summary>
/// Današnji datum po vremenu u Sarajevu. Rok promo koda („do 31.10.") je datum koji je admin
/// mislio po našem kalendaru, ne po UTC-u servera — u ponoć po UTC-u je kod Sarajeva već 1 ili
/// 2 sata sljedećeg dana.
/// </summary>
public static class LokalniDatum
{
    private static readonly TimeZoneInfo Zona = Nadji();

    private static TimeZoneInfo Nadji()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }

    public static DateOnly Danas() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zona));
}
```

- [ ] **Step 4: Napiši testove pravila** (`PromoKodPravilaTests.cs`)

Pripremi `TestDb` sa zonom i slotom (kao u `OcjeneTests.PripremiAsync`) i pomoćnikom koji dodaje `Order` sa zadatim `CustomerId`, `CustomerEmail`, `Status`, `PromoKodId`. Testovi (svaki `[Fact]`, imena opisna na srpskom):

- `Popust` (zaokruživanje `MidpointRounding.AwayFromZero`): `Procenat 10` na `33.25` → `3.33` (3.325; banker's bi dao 3.32); `Iznos 5` na `20` → `5`; `Iznos 5` na `4` → `4`.
- `Normalizuj(" dobrodosli10 ")` → `"DOBRODOSLI10"`.
- `ProvjeriAsync` vraća kod za `" jesen5 "` kad u bazi postoji `JESEN5`.
- Nepostojeći → poruka „Promo kod ne postoji ili nije aktivan."; `Aktivan = false` → ista poruka; oba `Code == "promo_kod_nevazeci"`.
- `VaziOd = danas + 1` → „Promo kod još ne važi."; `VaziDo = danas − 1` → „Promo kod je istekao."; `VaziDo = danas` → prolazi (uključiv).
- `MaxKoristenja = 1` i jedna neotkazana narudžba sa kodom → „Promo kod je iskorišten."; ista narudžba `Cancelled` → prolazi.
- `JednomPoKupcu`: narudžba istog `CustomerId` sa kodom → „Promo kod ste već iskoristili."; narudžba gosta sa e-mailom `Ana@X.ba` i kupac `KupacZaPromo(null, "ana@x.ba")` → ista poruka; otkazana → prolazi; narudžba drugog kupca → prolazi.
- `SamoNoviKupci`: kupac ima neotkazanu narudžbu (bilo koju, bez koda) → „Promo kod važi samo za prvu narudžbu."; samo otkazana → prolazi; `KupacZaPromo(null, null)` → prolazi (nema po čemu prepoznati).
- `MinIznos = 30`, subtotal `29.99` → „Promo kod važi za narudžbe od 30 KM."; `30` → prolazi.

- [ ] **Step 5: Pokreni testove — FAIL** (`PromoKodPravila` ne postoji)

- [ ] **Step 6: Implementacija pravila**

```csharp
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.PromoKodovi;

/// <summary>Ko primjenjuje kod. Gost bez unesenog e-maila je (null, null).</summary>
public record KupacZaPromo(Guid? CustomerId, string? Email);

/// <summary>
/// Jedino mjesto sa pravilima promo koda. Zovu ga provjera na checkoutu, oba načina
/// naručivanja i potvrda gosta — pravilo ponovljeno na četiri mjesta bi se razišlo.
/// </summary>
public static class PromoKodPravila
{
    public const string Greska = "promo_kod_nevazeci";

    public static string Normalizuj(string kod) => (kod ?? "").Trim().ToUpperInvariant();

    /// <summary>Popust na robu; nikad veći od nje (O1). AwayFromZero jer kupac očekuje školsko zaokruživanje.</summary>
    public static decimal Popust(PromoKod kod, decimal subtotal)
    {
        var p = kod.Tip == TipPopusta.Procenat
            ? Math.Round(subtotal * kod.Vrijednost / 100m, 2, MidpointRounding.AwayFromZero)
            : kod.Vrijednost;
        return Math.Min(p, subtotal);
    }

    public static async Task<PromoKod> ProvjeriAsync(
        IAppDbContext db, string unos, decimal subtotal, KupacZaPromo kupac, DateOnly danas,
        CancellationToken ct)
    {
        var tekst = Normalizuj(unos);
        var kod = await db.PromoKodovi.FirstOrDefaultAsync(k => k.Kod == tekst, ct);
        // Ugašen i nepostojeći kod daju istu poruku — ne otkriva se da je ugašeni postojao (O12).
        if (kod is null || !kod.Aktivan)
            throw new DomainException("Promo kod ne postoji ili nije aktivan.", Greska);
        if (kod.VaziOd is { } od && danas < od)
            throw new DomainException("Promo kod još ne važi.", Greska);
        if (kod.VaziDo is { } doo && danas > doo)
            throw new DomainException("Promo kod je istekao.", Greska);
        if (kod.MinIznos is { } min && subtotal < min)
            throw new DomainException($"Promo kod važi za narudžbe od {min:0.##} KM.", Greska);

        // Otkazane narudžbe ne troše kod (O7).
        var aktivne = db.Orders.Where(o => o.Status != OrderStatus.Cancelled);

        if (kod.MaxKoristenja is { } max &&
            await aktivne.CountAsync(o => o.PromoKodId == kod.Id, ct) >= max)
            throw new DomainException("Promo kod je iskorišten.", Greska);

        var email = string.IsNullOrWhiteSpace(kupac.Email) ? null : kupac.Email.Trim().ToLower();
        if (kupac.CustomerId is null && email is null)
            return kod; // nema po čemu prepoznati kupca; konačna provjera pri slanju ima e-mail

        var odKupca = aktivne.Where(o =>
            (kupac.CustomerId != null && o.CustomerId == kupac.CustomerId) ||
            (email != null && o.CustomerEmail != null && o.CustomerEmail.ToLower() == email));

        if (kod.JednomPoKupcu && await odKupca.AnyAsync(o => o.PromoKodId == kod.Id, ct))
            throw new DomainException("Promo kod ste već iskoristili.", Greska);
        if (kod.SamoNoviKupci && await odKupca.AnyAsync(ct))
            throw new DomainException("Promo kod važi samo za prvu narudžbu.", Greska);

        return kod;
    }
}
```

Napomena: redoslijed provjera je bitan — minimum prije limita da kupac sa premalom korpom dobije poruku koju može ispraviti.

- [ ] **Step 7: Migracija** — `dotnet ef migrations add DodajPromoKodove ...` (Global Constraints). Pregledaj da `Up` pravi tabelu `PromoKodovi`, tri kolone na `Orders` i `GuestOrderRequests` (`Popust` `numeric(10,2)` NOT NULL default 0), FK i indekse. Ne primjenjuj na bazu.

- [ ] **Step 8: Testovi — PASS**, cijeli suite zelen.

- [ ] **Step 9: Commit** `feat(p6b): promo kod — entitet, migracija i pravila`

---

### Task 2: Admin CRUD i provjera koda (backend)

**Files:**
- Create: `backend/src/EPiljara.Application/DTOs/PromoKodDtos.cs`
- Create: `backend/src/EPiljara.Application/Features/PromoKodovi/AdminPromoKodovi.cs`
- Create: `backend/src/EPiljara.Application/Features/PromoKodovi/ProvjeraPromoKoda.cs`
- Create: `backend/src/EPiljara.API/Controllers/PromoKodoviController.cs`
- Modify: `backend/src/EPiljara.API/Controllers/AdminController.cs`
- Test: `backend/tests/EPiljara.Application.Tests/PromoKodoviAdminTests.cs`, `ProvjeraPromoKodaTests.cs`

**Interfaces:**
- Consumes: Task 1 (`PromoKod`, `PromoKodPravila`, `KupacZaPromo`, `LokalniDatum`).
- Produces: `PromoKodDto`, `PromoKodInput`, `ProvjeraPromoKodaDto { Kod, Opis, Popust, Subtotal }`, `ProvjeriPromoKodQuery(string Kod, string? Email, IReadOnlyList<OrderItemInput> Items)`, rute iz speca.

- [ ] **Step 1: DTO-i**

```csharp
public record PromoKodDto
{
    public Guid Id { get; init; }
    public string Kod { get; init; } = "";
    public string Opis { get; init; } = "";
    public string Tip { get; init; } = "Procenat";
    public decimal Vrijednost { get; init; }
    public decimal? MinIznos { get; init; }
    public DateOnly? VaziOd { get; init; }
    public DateOnly? VaziDo { get; init; }
    public int? MaxKoristenja { get; init; }
    public bool JednomPoKupcu { get; init; }
    public bool SamoNoviKupci { get; init; }
    public bool Aktivan { get; init; }
    /// <summary>Neotkazane narudžbe sa ovim kodom — isti broj koji provjerava limit.</summary>
    public int BrojKoristenja { get; init; }
    public DateTime Kreiran { get; init; }
}

public record ProvjeraPromoKodaDto(string Kod, string Opis, decimal Popust, decimal Subtotal);
```

- [ ] **Step 2: Testovi admina** — lista vraća `BrojKoristenja` koji ne broji otkazane; create normalizuje kod („ jesen5 " → „JESEN5"); duplikat (i različit samo po velikim slovima) → `ConflictException` `promo_kod_postoji`; update mijenja vrijednost ali **ne** kod (pošalji drugi kod u inputu, ostaje stari); delete nekorišćenog briše; delete korišćenog (i samo otkazana narudžba) → `ConflictException` `promo_kod_koristen`; delete nepostojećeg → `NotFoundException`; validator: kod „AB" (prekratak), „ŠUMA" (nedozvoljen znak), tip „Pola", procenat 0 i 91, iznos 0 i 1001, `vaziDo < vaziOd`, `maxKoristenja 0`, `minIznos -1` — svaki daje grešku sa srpskom porukom pod ključem `input.<polje>`; null `Input` → greška.

- [ ] **Step 3: FAIL, pa implementacija** `AdminPromoKodovi.cs` po obrascu `Features/Utisci/Utisci.cs` (Query + Handler, `PromoKodInput` record + `PromoKodInputValidator`, `Create/Update/Delete` komande sa validatorima koji počinju `RuleFor(x => x.Input).NotNull().WithMessage("Podaci o kodu su obavezni.")` pa `SetValidator`).

```csharp
public record PromoKodInput(
    string Kod, string? Opis, string Tip, decimal Vrijednost, decimal? MinIznos,
    DateOnly? VaziOd, DateOnly? VaziDo, int? MaxKoristenja,
    bool JednomPoKupcu, bool SamoNoviKupci, bool Aktivan);
```

Pravila validatora (`OverridePropertyName` na camelCase ime polja kad je izraz, npr. normalizovan kod):
- `PromoKodPravila.Normalizuj(x.Kod)` matches `^[A-Z0-9-]{3,30}$` → „Kod mora imati 3 do 30 znakova: slova A–Z, cifre i crtica."
- `Opis` (trim) max 200 → „Opis može imati najviše 200 znakova."
- `Tip` parsira u `TipPopusta` → „Tip popusta mora biti Procenat ili Iznos."
- Procenat: `Vrijednost` 1–90 → „Procenat mora biti između 1 i 90."; Iznos: 0.01–1000 → „Iznos popusta mora biti između 0,01 i 1000 KM."
- `MinIznos >= 0` → „Minimalni iznos ne može biti negativan."
- `VaziDo >= VaziOd` kad su oba → „Kraj važenja ne može biti prije početka."
- `MaxKoristenja >= 1` kad postoji → „Limit korišćenja mora biti bar 1."

Lista: `db.PromoKodovi.OrderByDescending(k => k.Kreiran)` i broj korišćenja jednim grupisanim upitom nad `Orders` (`Status != Cancelled`, `PromoKodId != null`, `GroupBy`). Duplikat: provjera `AnyAsync` prije upisa, plus `catch (DbUpdateException)` → detach, ponovna provjera, `ConflictException` ili `throw` (isti obrazac kao `OcijeniNarudzbuHandler` u P6a). Delete: `db.Orders.AnyAsync(o => o.PromoKodId == id)` (sve, i otkazane — FK je `Restrict`) → 409 „Kod je korišćen na narudžbama — isključi ga umjesto brisanja.".

- [ ] **Step 4: Testovi provjere** (`ProvjeraPromoKodaTests`): subtotal se računa iz `Product.Price × Quantity` (ne iz klijenta); vraća `Popust` po pravilu; nedostupan proizvod → `DomainException` `product_unavailable`; prijavljen kupac (`FakeCurrentUser`) se prepoznaje po `Id` i `Email` (njegova ranija narudžba + `SamoNoviKupci` → odbijen); gost sa `email` istim kao ranija narudžba gosta → odbijen; prazan kod → validator „Unesite promo kod."; prazne stavke → „Korpa je prazna.".

- [ ] **Step 5: Implementacija** `ProvjeraPromoKoda.cs`:

```csharp
public record ProvjeriPromoKodQuery(string Kod, string? Email, IReadOnlyList<OrderItemInput> Items)
    : IRequest<ProvjeraPromoKodaDto>;
```

Handler (`IAppDbContext db, ICurrentUser current`): subtotal kao u `CreateGuestOrderRequestHandler` (isti `product_unavailable`), `Math.Round(..., 2)`; kupac = prijavljen ? `new KupacZaPromo(current.Id, current.Email)` : `new KupacZaPromo(null, query.Email)`; `PromoKodPravila.ProvjeriAsync(..., LokalniDatum.Danas(), ct)`; vrati `new ProvjeraPromoKodaDto(kod.Kod, kod.Opis, PromoKodPravila.Popust(kod, subtotal), subtotal)`. Napomena u komentaru: provjera ne rezerviše kod — konačna je pri slanju.

- [ ] **Step 6: Kontroleri**

`PromoKodoviController : ApiControllerBase`:
```csharp
/// <summary>Checkout pita koliki je popust prije slanja. Javno; limit protiv pogađanja kodova.</summary>
[HttpPost("promo-kodovi/provjera")]
[EnableRateLimiting("auth")]
public async Task<IActionResult> Provjeri([FromBody] ProvjeriPromoKodQuery query) =>
    Envelope(await Mediator.Send(query));
```
`AdminController` (po uzoru na `admin/utisci`): `GET admin/promo-kodovi`, `POST admin/promo-kodovi` (`[FromBody] PromoKodInput input` → `CreatePromoKodCommand(input)`, `EnvelopeCreated`), `PUT admin/promo-kodovi/{id:guid}`, `DELETE admin/promo-kodovi/{id:guid}`.

- [ ] **Step 7: Testovi PASS, commit** `feat(p6b): admin promo kodovi i provjera koda`

---

### Task 3: Narudžbe sa kodom (backend) + bezbjednosna popravka

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs`, `GuestOrderRequests.cs`
- Modify: `backend/src/EPiljara.Application/DTOs/OrderDtos.cs`, `Mapping/MappingExtensions.cs`
- Modify: `backend/src/EPiljara.API/Controllers/OrdersController.cs`, `Requests.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Services/OrderEmailTemplate.cs`, `GuestOrderEmailTemplate.cs`, `ViberMessage.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/DbSeeder.cs`
- Test: `backend/tests/EPiljara.Application.Tests/PromoKodNarudzbeTests.cs` (jedini test projekat; `CreateOrderRequest` živi u Application baš da bi ga ovaj projekat testirao — vidi Step 1)

**Interfaces:**
- Consumes: Task 1 pravila; Task 2 ništa.
- Produces: `CreateOrderCommand.PromoKod` (string?, posljednji opcioni parametar), `CreateGuestOrderRequestCommand.PromoKod`, `OrderDto.Popust` (decimal) i `OrderDto.PromoKod` (string?), `CreateOrderRequest` (žičani oblik) sa `ToCommand()`.

- [ ] **Step 1: Žičani oblik bez `CustomerId`/`PretplataId` (O18)**

U `Features/Orders/CreateOrder.cs` (Application, da bude testabilno):

```csharp
/// <summary>
/// Tijelo <c>POST /orders</c>. Namjerno bez <c>CustomerId</c> i <c>PretplataId</c>: dok je
/// kontroler vezivao <see cref="CreateOrderCommand"/> direktno, prijavljeni kupac je mogao
/// poslati tuđi <c>customerId</c> (narudžba na tuđem nalogu) ili <c>pretplataId</c>
/// (pretplatna cijena bez pretplate).
/// </summary>
public record CreateOrderRequest(
    CustomerInput Customer, Guid DeliveryZoneId, Guid DeliverySlotId, string DeliveryAddress,
    string? Note, IReadOnlyList<OrderItemInput> Items,
    double? Latitude = null, double? Longitude = null, string? PromoKod = null)
{
    public CreateOrderCommand ToCommand() => new(
        Customer, DeliveryZoneId, DeliverySlotId, DeliveryAddress, Note, Items,
        Latitude, Longitude, PromoKod: PromoKod);
}
```

`CreateOrderCommand` dobija `string? PromoKod = null` kao posljednji parametar. Ažuriraj XML komentar komande (više ne tvrdi nešto netačno — sada je stvarno tako). `OrdersController.Create` prima `[FromBody] CreateOrderRequest request` i šalje `request.ToCommand()`.

Testovi: refleksijom `typeof(CreateOrderRequest).GetProperties()` nema `CustomerId` ni `PretplataId`; `System.Text.Json` deserijalizacija `{"customer":{...},"customerId":"<guid>","pretplataId":"<guid>",...}` u `CreateOrderRequest` pa `ToCommand()` → komanda ima `CustomerId == null` i `PretplataId == null` (koristi `JsonSerializerOptions(JsonSerializerDefaults.Web)`).

- [ ] **Step 2: Testovi narudžbi sa kodom** (`PromoKodNarudzbeTests`; uzmi postavku iz `CreateOrderVlasnikTests`/`GuestOrderTests` — `FakeNotifications`, opcije):
  - Prijavljen kupac sa `"jesen5"` (Iznos 5): `Popust 5`, `PromoKod "JESEN5"`, `PromoKodId` upisan, `TotalAmount == Subtotal − 5 + DeliveryFee`; DTO nosi `Popust` i `PromoKod`.
  - Bez koda: `Popust 0`, `PromoKod null`, `TotalAmount` kao prije.
  - Nevažeći kod → `DomainException` `promo_kod_nevazeci`, narudžba nije upisana, zalihe nisu skinute.
  - Minimum zone 30, subtotal 30, 20 % popust → narudžba prolazi (O2).
  - Gost: zahtjev sa kodom snima `Popust`/`PromoKod`/`PromoKodId`, `TotalAmount` sa popustom; potvrda pravi narudžbu sa istim brojevima.
  - Gost, `JednomPoKupcu`: dva zahtjeva istim e-mailom; prva potvrda prolazi, druga → `ConflictException` `promo_kod_nevazeci`, zahtjev obrisan iz baze, narudžba nije nastala.
  - Gost: kod ugašen između zahtjeva i potvrde → 409, zahtjev obrisan.

- [ ] **Step 3: Implementacija**

`CreateOrderHandler`: poslije provjere minimuma zone (minimum ostaje nad `subtotal` — O2), a prije `new Order`:

```csharp
// Kod se provjerava tek kad je subtotal poznat (minimum koda) i poslije minimuma zone (O2).
PromoKod? promo = null;
var popust = 0m;
if (!string.IsNullOrWhiteSpace(cmd.PromoKod))
{
    promo = await PromoKodPravila.ProvjeriAsync(db, cmd.PromoKod, subtotal,
        new KupacZaPromo(customerId, cmd.Customer.Email), LokalniDatum.Danas(), ct);
    popust = PromoKodPravila.Popust(promo, subtotal);
}
```
(`customerId` se računa prije ovoga — pomjeri tu liniju gore.) U `Order`: `PromoKodId = promo?.Id, PromoKod = promo?.Kod, Popust = popust, TotalAmount = Math.Round(subtotal - popust + zone.DeliveryFee, 2)`.

Bitno: zalihe se skidaju u petlji prije provjere koda — ako kod padne, izuzetak izlazi prije `SaveChangesAsync`, pa se ništa ne upisuje. Test iz Step 2 to pokriva.

Validator komande: `RuleFor(x => x.PromoKod).MaximumLength(30).When(x => x.PromoKod is not null).WithMessage("Promo kod je predugačak.")` — isto u `CreateGuestOrderRequestValidator`.

`CreateGuestOrderRequestCommand` dobija `string? PromoKod = null`; handler isto kao gore sa `new KupacZaPromo(null, cmd.Customer.Email)`, upisuje u zahtjev.

`ConfirmGuestOrderHandler`: ako `zahtjev.PromoKod` nije null, prije pravljenja stavki:

```csharp
// Ponovna provjera (O11): bez nje bi gost sa pet zahtjeva istim e-mailom iskoristio
// „jednom po kupcu" pet puta. Popust se ne preračunava — kupac plaća ono što je vidio.
try
{
    await PromoKodPravila.ProvjeriAsync(db, zahtjev.PromoKod, zahtjev.Subtotal,
        new KupacZaPromo(null, zahtjev.CustomerEmail), LokalniDatum.Danas(), ct);
}
catch (DomainException e) when (e.Code == PromoKodPravila.Greska)
{
    db.GuestOrderRequests.Remove(zahtjev);
    await db.SaveChangesAsync(ct);
    throw new ConflictException(
        $"{e.Message} Narudžba nije primljena — vrati se u korpu i pošalji je ponovo.",
        PromoKodPravila.Greska);
}
```
i u `Order` prenese `PromoKodId/PromoKod/Popust` iz zahtjeva. Ako je `zahtjev.PromoKodId` null a `PromoKod` nije (kod obrisan — ne može zbog `Restrict` na narudžbama, ali može ako nikad nije korišćen) → provjera ga ne nađe → 409, što je ispravno.

`OrderDto`: `public decimal Popust { get; init; }` i `public string? PromoKod { get; init; }` (XML: „Već uračunat u TotalAmount."); `ToDto` ih puni.

- [ ] **Step 4: Mejl i Viber** — u `OrderEmailTemplate` između reda dostave i „Ukupno" dodaj red istog stila `Popust ({kod})` / `−{Money(order.Popust)}` samo kad `order.Popust > 0`; isto u `GuestOrderEmailTemplate` iz `request.Popust`/`request.PromoKod` (red iznad „Ukupno"); `ViberMessage`: linija `Popust ({kod}): −{Money(...)}` prije „Ukupno". Ako za šablone postoje testovi (`grep -rln OrderEmailTemplate backend/tests`), dodaj slučaj sa popustom; inače dodaj jedan test koji renderuje šablon sa `Popust = 5`, `PromoKod = "JESEN5"` i provjerava da sadrži „JESEN5" i iznos, i bez popusta da ne sadrži „Popust".

- [ ] **Step 5: Demo seed** — u `DbSeeder` u demo dijelu (gdje su recepti, sa istim „idempotentnim" obrascem `if (!await db.PromoKodovi.AnyAsync(ct))`): `DOBRODOSLI`, opis „Dobrodošlica — 10 % na prvu narudžbu", `Procenat 10`, `SamoNoviKupci = true`, `Aktivan = true`. Samo u demo seedu.

- [ ] **Step 6: Cijeli suite PASS, commit** `feat(p6b): narudžbe sa promo kodom; POST /orders više ne prima customerId/pretplataId`

---

### Task 4: Checkout i prikazi popusta (frontend)

**Files:**
- Modify: `frontend/src/types/domain.ts`, `frontend/src/lib/api/contracts.ts`, `frontend/src/lib/api/endpoints.ts`, `frontend/src/lib/api/hooks.ts` (samo query ključ ako ključevi tamo žive)
- Create: `frontend/src/components/shop/promo-kod-polje.tsx`
- Modify: `frontend/src/app/(shop)/checkout/page.tsx`, `frontend/src/components/shop/order-summary-panel.tsx`, `frontend/src/components/shop/order-receipt.tsx`, `frontend/src/components/admin/order-card.tsx`
- Modify: `frontend/src/server/mock/store.ts` samo ako tsc traži nova polja

**Interfaces:**
- Consumes: `POST /promo-kodovi/provjera` → `{ kod, opis, popust, subtotal }`; `OrderDto.popust`, `OrderDto.promoKod`; `promoKod?` u tijelu `POST /orders` i `/orders/guest-request`; greška `code: "promo_kod_nevazeci"`.
- Produces: `ProvjeraPromoKoda` tip, `promoApi.provjeri(body)`.

- [ ] **Step 1: Tipovi i API** — `Order` dobija `popust: number; promoKod?: string | null`. `CreateOrderRequest` dobija `promoKod?: string`. Novi tip `ProvjeraPromoKoda { kod: string; opis: string; popust: number; subtotal: number }`. `promoApi.provjeri({ kod, email?, items })` → `apiFetch<ProvjeraPromoKoda>("/promo-kodovi/provjera", { method: "POST", body }).then(r => r.data)`.

- [ ] **Step 2: `PromoKodPolje`** — kontrolisana komponenta:
  - props: `primijenjen: string | null`, `provjera: { data?: ProvjeraPromoKoda; error: unknown; isFetching: boolean }`, `onPrimijeni(kod: string)`, `onUkloni()`, `greskaSlanja?: string`.
  - Bez primijenjenog koda: `Field` „Promo kod" sa `Input` (`autoCapitalize="characters"`, `maxLength={30}`) i dugme „Primijeni" (disabled dok je prazno); Enter u polju primjenjuje **bez** slanja forme (`e.preventDefault()`).
  - Sa primijenjenim: red sa kodom, „−{formatPrice(popust)}" kad `data` postoji, „Provjeravam…" dok `isFetching`, i dugme „Ukloni".
  - Greška (`error instanceof ApiRequestError ? error.message : "Provjera koda nije uspjela."`, ili `greskaSlanja`) ispod polja u `text-xs text-red-700`, `role="alert"`.

- [ ] **Step 3: Checkout**
  - stanje `const [promoKod, setPromoKod] = useState<string | null>(null)`.
  - `const promo = useQuery({ queryKey: ["promo", promoKod, form.email, items.map(i => [i.productId, i.quantity])], queryFn: () => promoApi.provjeri({ kod: promoKod!, email: form.email || undefined, items: ... }), enabled: !!promoKod && items.length > 0, retry: false })`.
  - `const popust = promo.data?.popust ?? 0; const total = Math.round((subtotal - popust + deliveryFee) * 100) / 100;` (popust je sa servera — klijent ga ne računa).
  - `promoKod` ide u body samo kad `promo.data` postoji (kod koji je server odbio se ne šalje i ne blokira narudžbu); kad je `promo.isError`, dugme „Naruči" ostaje omogućeno, a polje pokazuje grešku — kupac može ukloniti kod.
  - `PromoKodPolje` u koraku 3, iznad greške minimuma.
  - Greška slanja sa `code === "promo_kod_nevazeci"` (provjeri kako `ApiRequestError` izlaže kod — `err.code`) ide u `greskaSlanja` polja, a ne u opšti crveni blok (uslov opšteg bloka dobija `&& activeMutation.error.code !== "promo_kod_nevazeci"`).
  - `OrderSummaryPanel` (oba mjesta) dobija `popust={popust}` i `promoKod={promo.data?.kod}`.

- [ ] **Step 4: Prikazi** — `OrderSummaryPanel`: opcioni props `popust?: number; promoKod?: string`; red `Popust ({promoKod})` sa `−{formatPrice(popust)}` u `text-brand-700` između dostave i ukupno, samo kad `popust > 0`. Isto u `OrderReceipt` (između „Trošak dostave" i „Ukupno") i u `order-card.tsx` (u bloku razrade iznosa).

- [ ] **Step 5: Provjera** — `npx tsc --noEmit` i `npm run lint` čisti. Commit `feat(p6b): promo kod na checkoutu i popust na prikazima narudžbe`

---

### Task 5: Admin ekran promo kodova (frontend)

**Files:**
- Create: `frontend/src/app/admin/promo-kodovi/page.tsx`
- Create: `frontend/src/components/admin/promo-kod-form.tsx`
- Modify: `frontend/src/components/admin/admin-nav.tsx`, `frontend/src/lib/api/endpoints.ts` (`adminApi`), `frontend/src/lib/api/hooks.ts` (`queryKeys.adminPromoKodovi`), `frontend/src/types/domain.ts`

**Interfaces:**
- Consumes: `GET/POST /admin/promo-kodovi`, `PUT/DELETE /admin/promo-kodovi/{id}`; `PromoKodDto` (Task 2); greške `promo_kod_postoji`, `promo_kod_koristen`; validacioni ključevi `input.kod`, `input.vrijednost`, …
- Produces: ništa za kasnije taskove.

- [ ] **Step 1: Tipovi i API** — `PromoKod` tip (polja iz `PromoKodDto`, `tip: "Procenat" | "Iznos"`, datumi kao `string | null` „yyyy-MM-dd"), `PromoKodInput`; `adminApi.promoKodovi()`, `createPromoKod(input)`, `updatePromoKod(id, input)`, `deletePromoKod(id)` po uzoru na utiske.

- [ ] **Step 2: Forma** (`promo-kod-form.tsx`, po uzoru na `utisak-form.tsx`): kod (onemogućen pri izmjeni, sa napomenom „Kod se ne mijenja poslije kreiranja"), opis, tip (radio „Procenat"/„Iznos u KM"), vrijednost, minimalan iznos robe, važi od / do (`type="date"`, prazno = bez granice), limit korišćenja (prazno = bez limita), checkboxi „Jednom po kupcu", „Samo za prvu narudžbu", „Aktivan" (podrazumijevano uključen). Greške polja iz `err.fieldErrors["input.kod"]` itd. Prazni brojevi i datumi idu kao `null`.

- [ ] **Step 3: Stranica** — `h1` „Promo kodovi", dugme „Novi kod", lista (tabela na širokom, kartice na uskom ekranu): kod (mono), opis, popust („10 %" ili „5,00 KM"), važenje („do 31.10.2026." / „bez roka"), uslovi (oznake „jednom po kupcu", „prva narudžba", „min. X KM"), korišćeno „N / max" ili „N", status (Aktivan/Isključen), akcije: izmijeni, uključi/isključi (PUT sa obrnutim `aktivan`), obriši samo kad `brojKoristenja === 0` (inače dugme sakriveno, a 409 od servera i dalje ide u toast). Brisanje sa potvrdom u liniji kao na `/admin/utisci` (bez `confirm()` dijaloga). Stanja učitavanja, greške („Promo kodovi se nisu učitali.") i prazne liste („Još nema promo kodova.").

- [ ] **Step 4: Navigacija** — `{ href: "/admin/promo-kodovi", label: "Promo kodovi", icon: TicketPercent }` odmah iza „Ocjene".

- [ ] **Step 5: Provjera** — `tsc`, `lint`; zatim (bez dev servera) `rm -rf .next && npm run build && rm -rf .next` — build mora proći. Commit `feat(p6b): admin ekran promo kodova`
