# P2b — Moja korpica: plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kupac mijenja sadržaj naručene korpice do roka, u okviru 95–100% budžeta; narudžba dobija ISO sedmicu isporuke; pakerska lista počinje da broji narudžbe umjesto šablona.

**Architecture:** Sadržaj korpice se pri naručivanju **snima** iz sedmičnog šablona u redove vezane za `OrderItem` — isti obrazac kojim `OrderItem` već snima naziv i cijenu artikla. Kupac mijenja svoje redove; admin ne može tiho promijeniti ono što je kupac kupio. Pakerska lista prestaje da sabira šablon i sabira te redove preko svih narudžbi te sedmice.

**Tech Stack:** ASP.NET Core 9, EF Core 9 + PostgreSQL, MediatR, FluentValidation, xUnit + SQLite in-memory; Next.js 15 App Router, TypeScript, Tailwind v4, TanStack Query.

**Spec:** `docs/superpowers/specs/2026-10-09-p2b-moja-korpica-design.md`

## Global Constraints

- **Omotnica odgovora:** svaki endpoint vraća `{ data, error, meta }` preko `Envelope`/`EnvelopeList` iz `ApiControllerBase`. Nikad `Ok(dto)`.
- **Identifikator greške je DRUGI argument konstruktora.** `ExceptionHandlingMiddleware` serijalizuje `ex.Code`, nikad `ex.Message`. `new DomainException("Poruka.", "kod_greske")`. Ova greška je u P1 napravljena tri puta.
- `DomainException`→400, `NotFoundException`→404 (ima i konstruktor sa jednim argumentom koji hardkoduje `not_found`), `ForbiddenException`→403, `ConflictException`→409.
- **JSON izostavlja null:** `DefaultIgnoreCondition = WhenWritingNull`. Nullable backend polje je na žici **odsutno**, nikad `null`. TS tipovi koriste `?:`, nikad `| null`.
- **`TestDb` dijeli jedan dugovječan `DbContext`** između pripreme i poziva handlera, pa EF fixup popuni navigacije i bez `Include`. Test koji tvrdi nešto o učitanim podacima mora zvati `db.ChangeTracker.Clear()` ili koristiti `AsNoTracking()`, inače je lažno zelen.
- **`MediatR.Unit` i `EPiljara.Domain.Enums.Unit` se sudaraju** (CS0104) u fajlu koji koristi oba imenska prostora. Rješenje: `using Unit = EPiljara.Domain.Enums.Unit;` ili puno ime.
- **Nova NOT NULL kolona mora dobiti eksplicitan `defaultValue`** u migraciji. U P1 je EF dao enum koloni `DEFAULT ''`, što nije validan član i rušilo bi čitanje postojećih redova.
- **Testovi rade na SQLite** preko `EnsureCreated()`, pa se migracije u testovima **ne izvršavaju**. SQL u migraciji se provjerava na Postgresu, ne u test suite-u.
- **`Product.Mjeseci`** je `int[]` u C# a CSV u bazi; sezona se filtrira **u memoriji**, poslije `ToListAsync()`.
- Srpski/ijekavica u svim porukama i identifikatorima grešaka (`snake_case`).
- `npm run build` se pokreće **samo** u zadnjem tasku i **samo** sa ugašenim dev serverom.

---

## Struktura fajlova

**Backend**

| Fajl | Odgovornost |
|---|---|
| `Domain/Entities/StavkaKorpiceNarudzbe.cs` (nov) | jedan artikal u korpici jedne narudžbe |
| `Domain/Entities/Order.cs` (izmjena) | `Godina`, `Sedmica` |
| `Domain/Entities/OrderItem.cs` (izmjena) | kolekcija `KorpicaStavke` |
| `Application/Features/Korpice/KorpicaPodobnost.cs` (nov) | jedan predikat „smije u korpicu", dijele ga generator i editor |
| `Application/Features/Korpice/MojaKorpica.cs` (nov) | upiti i komanda editora |
| `Application/Features/Orders/CreateOrder.cs` (izmjena) | sedmica + snimanje sadržaja |
| `Application/Features/Korpice/AdminKorpice.cs` (izmjena) | pakerska lista po narudžbama |
| `Application/Features/Korpice/KorpiceQueries.cs` (izmjena) | `SedmicaZaNarudzbu` |
| `Api/Controllers/AccountController.cs` (izmjena) | tri endpointa |

**Frontend**

| Fajl | Odgovornost |
|---|---|
| `components/nalog/korpica-editor.tsx` (nov) | kartica jedne korpice: spisak, budžet, birač |
| `app/(shop)/moj-nalog/korpica/page.tsx` (nov) | ekran, lista korpica koje se mogu mijenjati |
| `components/nalog/nalog-nav.tsx` (izmjena) | otključava stavku „Moja korpica" |
| `app/(shop)/moj-nalog/page.tsx` (izmjena) | otključava pločicu |
| `app/admin/korpice/page.tsx` (izmjena) | broj narudžbi uz pakersku listu |

---

### Task 1: `SedmicaZaNarudzbu` — koja sedmica se naručuje

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Korpice/KorpiceQueries.cs` (u `SedmicaHelper`, poslije `UcitajRokAsync`)
- Test: `backend/tests/EPiljara.Application.Tests/SedmicaZaNarudzbuTests.cs` (nov)

**Interfaces:**
- Consumes: `SedmicaHelper.Tekuca`, `SedmicaHelper.DatumIsporuke` (postoje)
- Produces: `SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek dan, string vrijemeOd, int cutoffSati, DateTime? sada = null) → (int Godina, int Sedmica)`

- [ ] **Step 1: Napiši testove koji padaju**

Novi fajl `backend/tests/EPiljara.Application.Tests/SedmicaZaNarudzbuTests.cs`:

```csharp
using System.Globalization;
using EPiljara.Application.Features.Korpice;

namespace EPiljara.Application.Tests;

/// <summary>
/// Datumi su birani i provjereni ručno: 2026-01-01 je četvrtak, pa ISO 2026 ima 53 sedmice.
/// 2026-10-05 je ponedjeljak, 2026-12-31 je četvrtak.
/// </summary>
public class SedmicaZaNarudzbuTests
{
    [Fact]
    public void Prije_roka_vraca_tekucu_sedmicu()
    {
        // Isporuka četvrtkom u 16:00, rok 48h → rok je utorak u 16:00.
        // Ponedjeljak u 10:00 je prije njega.
        var sada = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);

        var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "16:00", 48, sada);

        Assert.Equal(ISOWeek.GetYear(sada), g);
        Assert.Equal(ISOWeek.GetWeekOfYear(sada), s);
    }

    [Fact]
    public void Tacno_na_roku_jos_uvijek_vraca_tekucu_sedmicu()
    {
        // Granica je uključiva: ko stigne u sekundi roka, naručuje za ovu sedmicu.
        var sada = new DateTime(2026, 10, 6, 16, 0, 0, DateTimeKind.Utc);

        var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "16:00", 48, sada);

        Assert.Equal(ISOWeek.GetWeekOfYear(sada), s);
        Assert.Equal(ISOWeek.GetYear(sada), g);
    }

    [Fact]
    public void Poslije_roka_vraca_sljedecu_sedmicu()
    {
        // Srijeda u 10:00 — rok (utorak 16:00) je prošao.
        var sada = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);

        var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "16:00", 48, sada);

        Assert.Equal(ISOWeek.GetYear(sada), g);
        Assert.Equal(ISOWeek.GetWeekOfYear(sada) + 1, s);
    }

    [Fact]
    public void Prelaz_godine_daje_prvu_sedmicu_naredne_ISO_godine()
    {
        // 2026-12-31 je četvrtak u ISO sedmici 53; rok je prošao 29.12. u 16:00.
        var sada = new DateTime(2026, 12, 31, 20, 0, 0, DateTimeKind.Utc);
        Assert.Equal(53, ISOWeek.GetWeekOfYear(sada));   // pretpostavka testa, ne tvrdnja o kodu

        var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "16:00", 48, sada);

        Assert.Equal(2027, g);
        Assert.Equal(1, s);
    }

    [Fact]
    public void Neparsabilno_vrijeme_ne_rusi_racun()
    {
        // ParsirajVrijeme pada na ponoć; funkcija i dalje mora vratiti validnu sedmicu.
        var sada = new DateTime(2026, 10, 5, 10, 0, 0, DateTimeKind.Utc);

        var (g, s) = SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "ne-vrijeme", 48, sada);

        Assert.InRange(s, 1, 53);
        Assert.Equal(2026, g);
    }
}
```

- [ ] **Step 2: Pokreni testove, potvrdi da padaju**

Run: `cd backend && dotnet test --filter SedmicaZaNarudzbuTests`
Expected: FAIL — `SedmicaHelper` ne sadrži definiciju za `SedmicaZaNarudzbu` (greška kompilacije CS0117).

- [ ] **Step 3: Dodaj metodu**

U `backend/src/EPiljara.Application/Features/Korpice/KorpiceQueries.cs`, unutar `public static class SedmicaHelper`, odmah poslije `UcitajRokAsync`:

```csharp
    /// <summary>
    /// Sedmica koju narudžba isporučuje: prva sedmica čiji rok naručivanja još nije prošao.
    ///
    /// NE koristi <see cref="Tekuca"/> direktno. Korpica je Product, a checkout ne provjerava
    /// rok — bez ovog pravila bi se poslije roka naručivala korpica za sedmicu čiji je sadržaj
    /// već zaključan i čija je pakerska lista već odštampana.
    ///
    /// Granica godine se ne računa ručno: uzima se ponedjeljak tekuće ISO sedmice, doda sedam
    /// dana, i iz tog datuma ponovo izvedu ISO godina i sedmica — pa prelaz 52/53 → 1 izlazi
    /// tačan sam od sebe.
    /// </summary>
    public static (int Godina, int Sedmica) SedmicaZaNarudzbu(
        DayOfWeek dan, string vrijemeOd, int cutoffSati, DateTime? sada = null)
    {
        var now = sada ?? DateTime.UtcNow;
        var (godina, sedmica) = Tekuca(now);

        var rok = DatumIsporuke(godina, sedmica, dan, vrijemeOd).AddHours(-cutoffSati);
        if (now <= rok) return (godina, sedmica);

        var sljedeca = ISOWeek.ToDateTime(godina, sedmica, DayOfWeek.Monday).AddDays(7);
        return (ISOWeek.GetYear(sljedeca), ISOWeek.GetWeekOfYear(sljedeca));
    }
```

- [ ] **Step 4: Pokreni testove, potvrdi da prolaze**

Run: `cd backend && dotnet test --filter SedmicaZaNarudzbuTests`
Expected: PASS — 5 testova.

Zatim cijeli paket: `cd backend && dotnet test`
Expected: PASS — 107 postojećih + 5 novih = 112.

- [ ] **Step 5: Commit**

```bash
git add backend/src/EPiljara.Application/Features/Korpice/KorpiceQueries.cs backend/tests/EPiljara.Application.Tests/SedmicaZaNarudzbuTests.cs
git commit -m "feat(korpice): SedmicaZaNarudzbu — prva sedmica ciji rok nije prosao"
```

---

### Task 2: `Order.Godina` i `Order.Sedmica`

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/Order.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs` (u `b.Entity<Order>`)
- Modify: `backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs`
- Create: migracija `SedmicaNaNarudzbi`
- Test: `backend/tests/EPiljara.Application.Tests/CreateOrderSedmicaTests.cs` (nov)

**Interfaces:**
- Consumes: `SedmicaHelper.SedmicaZaNarudzbu`, `SedmicaHelper.UcitajRokAsync`, `KorpiceOpcije`
- Produces: `Order.Godina`, `Order.Sedmica` (oba `int`, NOT NULL)

- [ ] **Step 1: Napiši test koji pada**

Novi fajl `backend/tests/EPiljara.Application.Tests/CreateOrderSedmicaTests.cs`:

```csharp
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EPiljara.Application.Features.Korpice;

namespace EPiljara.Application.Tests;

public class CreateOrderSedmicaTests
{
    private static async Task<(TestDb, Guid zonaId, Guid slotId, Guid productId)> PripremiAsync()
    {
        var t = new TestDb();

        var zona = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var proizvodjac = new Producer { Name = "Marko", Village = "Manjača" };
        var p = new Product
        {
            Name = "Krompir", Slug = "krompir", Price = 2m, Unit = Unit.Kg,
            Stock = 100, IsAvailable = true, Category = kat, Producer = proizvodjac,
        };

        t.Db.DeliveryZones.Add(zona);
        t.Db.DeliverySlots.Add(slot);
        t.Db.Products.Add(p);
        await t.Db.SaveChangesAsync();

        return (t, zona.Id, slot.Id, p.Id);
    }

    private static CreateOrderHandler Handler(TestDb t) =>
        new(t.Db, new FakeCurrentUser(null), new NoopEmail(), new NoopViber());

    [Fact]
    public async Task Narudzba_dobija_ISO_sedmicu_isporuke()
    {
        var (t, zonaId, slotId, productId) = await PripremiAsync();
        using var _ = t;

        var cmd = new CreateOrderCommand(
            new CustomerInput("Mujo", "mujo@example.com", "065"),
            zonaId, slotId, "Testna 1", null,
            [new OrderItemInput(productId, 1m)]);

        var dto = await Handler(t).Handle(cmd, default);

        t.Db.ChangeTracker.Clear();
        var order = await t.Db.Orders.AsNoTracking().FirstAsync(o => o.Id == dto.Id);

        var (dan, vrijeme, cutoff) = await SedmicaHelper.UcitajRokAsync(
            t.Db, new KorpiceOpcije(), default);
        var (ocekivanaGodina, ocekivanaSedmica) =
            SedmicaHelper.SedmicaZaNarudzbu(dan, vrijeme, cutoff);

        Assert.Equal(ocekivanaGodina, order.Godina);
        Assert.Equal(ocekivanaSedmica, order.Sedmica);
        // Ne smije ostati na podrazumijevanoj nuli — to je cio smisao ovog testa.
        Assert.NotEqual(0, order.Sedmica);
    }
}
```

Uz to, u istom fajlu, na dnu — dvije prazne implementacije servisa koje handler traži:

```csharp
file sealed class NoopEmail : EPiljara.Application.Common.Interfaces.IEmailService
{
    public Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default) => Task.CompletedTask;
}

file sealed class NoopViber : EPiljara.Application.Common.Interfaces.IViberService
{
    public Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default) => Task.CompletedTask;
}
```

**Napomena za implementera:** `IEmailService` i `IViberService` imaju i druge članove osim `SendOrderConfirmationAsync`. Otvori `backend/src/EPiljara.Application/Common/Interfaces/Services.cs`, prepiši **sve** njihove članove u ove dvije klase kao `Task.CompletedTask` / `default`. Ako u `tests/` već postoji lažna implementacija tih servisa (provjeri: `grep -rn "IEmailService" backend/tests/`), **koristi postojeću** umjesto da praviš drugu.

- [ ] **Step 2: Pokreni test, potvrdi da pada**

Run: `cd backend && dotnet test --filter CreateOrderSedmicaTests`
Expected: FAIL — `Order` ne sadrži definiciju za `Godina` (CS1061).

- [ ] **Step 3: Dodaj polja na entitet**

U `backend/src/EPiljara.Domain/Entities/Order.cs`, odmah poslije `public DateTime CreatedAt`:

```csharp
    /// <summary>
    /// ISO 8601 godina i sedmica DATUMA ISPORUKE — ne datuma narudžbe. Korpica kupljena u
    /// ovoj narudžbi nosi sadržaj te sedmice, a pakerska lista po njoj grupiše.
    ///
    /// Narudžbama otprije P2b migracija je upisala sedmicu izvedenu iz CreatedAt. Za njih je
    /// vrijednost PRIBLIŽNA — ko je naručio u nedjelju za četvrtak naredne sedmice ima upisanu
    /// pogrešnu. To se prihvata jer nijedna od njih ne nosi sadržaj korpice koji bi o tu
    /// sedmicu visio; polje im je samo metapodatak.
    /// </summary>
    public int Godina { get; set; }
    public int Sedmica { get; set; }
```

- [ ] **Step 4: Dodaj indeks u EF konfiguraciju**

U `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs`, u bloku `b.Entity<Order>(e => { … })`, odmah poslije `e.HasIndex(x => x.CustomerId);`:

```csharp
            // Pakerska lista i editor oboje traže narudžbe jedne sedmice.
            e.HasIndex(x => new { x.Godina, x.Sedmica });
```

- [ ] **Step 5: Upiši sedmicu pri kreiranju narudžbe**

U `backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs`:

Promijeni potpis handlera da prima opcije korpica:

```csharp
public class CreateOrderHandler(
    IAppDbContext db,
    ICurrentUser current,
    IEmailService email,
    IViberService viber,
    IOptions<KorpiceOpcije> korpiceOpcije)
    : IRequestHandler<CreateOrderCommand, OrderDto>
```

Dodaj uvoze na vrh fajla:

```csharp
using EPiljara.Application.Features.Korpice;
using Microsoft.Extensions.Options;
```

U `Handle`, odmah poslije učitavanja `slot`-a:

```csharp
        // Sedmica isporuke se računa iz ciklusa korpica, ne iz DateTime.Now — vidi
        // SedmicaHelper.SedmicaZaNarudzbu za razlog zašto poslije roka ide naredna sedmica.
        var (danIsporuke, vrijemeIsporuke, cutoffSati) =
            await SedmicaHelper.UcitajRokAsync(db, korpiceOpcije.Value, ct);
        var (godina, sedmica) =
            SedmicaHelper.SedmicaZaNarudzbu(danIsporuke, vrijemeIsporuke, cutoffSati);
```

I u inicijalizator `new Order { … }`, poslije `Note = cmd.Note,`:

```csharp
            Godina = godina,
            Sedmica = sedmica,
```

- [ ] **Step 6: Pokreni test, potvrdi da prolazi**

Run: `cd backend && dotnet test --filter CreateOrderSedmicaTests`
Expected: PASS

- [ ] **Step 7: Napravi migraciju**

```bash
cd backend
dotnet ef migrations add SedmicaNaNarudzbi \
  --project src/EPiljara.Infrastructure \
  --startup-project src/EPiljara.API
```

Otvori generisani fajl i provjeri da obje `AddColumn<int>` imaju `defaultValue: 0`. Ako nemaju, dodaj ih — NOT NULL kolona bez defaulta obara migraciju na tabeli koja već ima redove.

Zatim, **na kraj `Up(...)`**, dodaj backfill:

```csharp
            // Narudžbama otprije P2b upisujemo ISO sedmicu njihovog CreatedAt. To je
            // PRIBLIŽNO — narudžba napravljena u nedjelju za četvrtak naredne sedmice dobija
            // pogrešnu sedmicu. Prihvata se jer nijedna od njih ne nosi sadržaj korpice; vidi
            // komentar na Order.Godina.
            //
            // U Postgresu je EXTRACT(WEEK ...) ISO sedmica, a ISOYEAR odgovarajuća ISO godina
            // — par koji se mora uzimati zajedno, jer se 1. januar često vodi pod sedmicu 52
            // prethodne ISO godine.
            //
            // Testovi rade na SQLite preko EnsureCreated() i migracije uopšte ne izvršavaju,
            // pa ovaj SQL ne pokriva nijedan test. Provjerava se na Postgresu.
            migrationBuilder.Sql("""
                UPDATE "Orders"
                SET "Godina"  = EXTRACT(ISOYEAR FROM "CreatedAt")::int,
                    "Sedmica" = EXTRACT(WEEK    FROM "CreatedAt")::int
                WHERE "Sedmica" = 0;
                """);
```

- [ ] **Step 8: Primijeni migraciju i provjeri na živoj bazi**

```bash
cd backend
dotnet ef database update --project src/EPiljara.Infrastructure --startup-project src/EPiljara.API
```

Expected: prolazi bez greške. Nijedna postojeća narudžba ne smije ostati sa `Sedmica = 0` — prijavi u izvještaju koliko ih je ažurirano i da li ijedna ima nulu.

- [ ] **Step 9: Pokreni cio paket**

Run: `cd backend && dotnet test`
Expected: PASS — 113 testova.

- [ ] **Step 10: Commit**

```bash
git add backend/
git commit -m "feat(narudzbe): Order dobija ISO sedmicu isporuke

Migracija postojecim redovima upisuje sedmicu iz CreatedAt, sto je priblizno
i tako je i dokumentovano — nijedna stara narudzba ne nosi sadrzaj korpice."
```

---

### Task 3: Jedan predikat podobnosti za korpicu

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Korpice/KorpicaPodobnost.cs`
- Modify: `backend/src/EPiljara.Application/Features/Korpice/GenerisiSedmicniSadrzaj.cs:61-71`
- Test: `backend/tests/EPiljara.Application.Tests/KorpicaPodobnostTests.cs` (nov)

**Interfaces:**
- Produces:
  - `KorpicaPodobnost.UpitPodobnih(IQueryable<Product> izvor) → IQueryable<Product>` — dio koji ide u SQL
  - `KorpicaPodobnost.USezoni(IEnumerable<Product> ucitani, int mjesec) → IEnumerable<Product>` — dio koji mora u memoriju

**Zašto ovaj task postoji:** generator i editor moraju imati **isti** pojam „smije u korpicu". Dvije kopije istog uslova se raziđu prvog dana kad se jedan promijeni, a razilaženje se vidi tek kad kupac doda artikal koji generator nikad ne bi izabrao.

- [ ] **Step 1: Napiši testove koji padaju**

Novi fajl `backend/tests/EPiljara.Application.Tests/KorpicaPodobnostTests.cs`:

```csharp
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class KorpicaPodobnostTests
{
    private static Product Napravi(
        string naziv,
        bool mozeUKorpicu = true,
        bool dostupan = true,
        decimal stock = 10m,
        VrstaProizvoda vrsta = VrstaProizvoda.Obican,
        int[]? mjeseci = null) => new()
    {
        Name = naziv,
        Slug = naziv.ToLowerInvariant(),
        Price = 2m,
        Unit = Unit.Kg,
        Stock = stock,
        IsAvailable = dostupan,
        MozeUKorpicu = mozeUKorpicu,
        Vrsta = vrsta,
        Mjeseci = mjeseci ?? [],
    };

    [Fact]
    public async Task UpitPodobnih_propusta_samo_obicne_dostupne_sa_zalihom()
    {
        using var t = new TestDb();
        var kat = new Category { Name = "Povrće", Slug = "povrce" };
        var pr = new Producer { Name = "Marko", Village = "Manjača" };

        var dobar      = Napravi("Krompir");
        var neMoze     = Napravi("Krompir2", mozeUKorpicu: false);
        var nedostupan = Napravi("Krompir3", dostupan: false);
        var bezZalihe  = Napravi("Krompir4", stock: 0m);
        var korpica    = Napravi("Korpica", vrsta: VrstaProizvoda.Korpica);

        foreach (var p in new[] { dobar, neMoze, nedostupan, bezZalihe, korpica })
        {
            p.Category = kat;
            p.Producer = pr;
            t.Db.Products.Add(p);
        }
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var imena = await KorpicaPodobnost.UpitPodobnih(t.Db.Products.AsNoTracking())
            .Select(p => p.Name)
            .ToListAsync();

        Assert.Equal(["Krompir"], imena);
    }

    [Fact]
    public void USezoni_propusta_prazan_popis_mjeseci_i_podudaran_mjesec()
    {
        var uvijek = Napravi("Jaja");                     // prazan Mjeseci = cijele godine
        var samoJul = Napravi("Paradajz", mjeseci: [7]);
        var samoDec = Napravi("Kupus", mjeseci: [12]);

        var rezultat = KorpicaPodobnost
            .USezoni([uvijek, samoJul, samoDec], 7)
            .Select(p => p.Name)
            .ToList();

        Assert.Equal(["Jaja", "Paradajz"], rezultat);
    }
}
```

- [ ] **Step 2: Pokreni testove, potvrdi da padaju**

Run: `cd backend && dotnet test --filter KorpicaPodobnostTests`
Expected: FAIL — tip `KorpicaPodobnost` ne postoji (CS0103).

- [ ] **Step 3: Napravi klasu**

Novi fajl `backend/src/EPiljara.Application/Features/Korpice/KorpicaPodobnost.cs`:

```csharp
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Jedan pojam „artikal smije u korpicu", koji dijele generator sedmičnog sadržaja i kupčev
/// editor. Dvije kopije ovog uslova se raziđu prvog dana kad se jedna promijeni, a
/// razilaženje se primijeti tek kad kupac doda artikal koji generator nikad ne bi izabrao.
///
/// Podijeljen je na dva dijela jer <see cref="Product.Mjeseci"/> je int[] u C# a CSV u bazi:
/// sezona se NE MOŽE filtrirati u SQL-u i mora u memoriju poslije učitavanja.
/// </summary>
public static class KorpicaPodobnost
{
    /// <summary>Dio uslova koji ide u SQL. Sezona se dodaje odvojeno sa <see cref="USezoni"/>.</summary>
    public static IQueryable<Product> UpitPodobnih(IQueryable<Product> izvor) =>
        izvor.Where(p => p.Vrsta == VrstaProizvoda.Obican
                         && p.MozeUKorpicu
                         && p.IsAvailable
                         && p.Stock > 0);

    /// <summary>
    /// Sezonski filter, u memoriji. Prazan <see cref="Product.Mjeseci"/> znači „cijele godine".
    /// </summary>
    public static IEnumerable<Product> USezoni(IEnumerable<Product> ucitani, int mjesec) =>
        ucitani.Where(p => p.Mjeseci.Length == 0 || p.Mjeseci.Contains(mjesec));
}
```

- [ ] **Step 4: Prevedi generator na isti predikat**

U `backend/src/EPiljara.Application/Features/Korpice/GenerisiSedmicniSadrzaj.cs`, zamijeni blok na linijama 60–71:

```csharp
            // Mjeseci su CSV u bazi, pa se sezona filtrira u memoriji nakon učitavanja.
            var bazen = (await db.Products
                    .Where(p => p.Vrsta == VrstaProizvoda.Obican
                                && p.MozeUKorpicu
                                && p.IsAvailable
                                && p.Stock > 0
                                && kategorije.Contains(p.CategoryId))
                    .ToListAsync(ct))
                .Where(p => p.Mjeseci.Length == 0 || p.Mjeseci.Contains(mjesec))
                .OrderBy(p => p.PrioritetUKorpici)
                .ThenBy(_ => Guid.NewGuid())      // nasumično unutar istog prioriteta
                .ToList();
```

ovim:

```csharp
            // Mjeseci su CSV u bazi, pa se sezona filtrira u memoriji nakon učitavanja.
            // Uslov podobnosti dijeli sa kupčevim editorom — vidi KorpicaPodobnost.
            // Ograničenje na kategorije te korpice ostaje SAMO ovdje: generator puni povrćnu
            // korpicu povrćem, a kupac po odluci iz P2b bira iz cijele ponude.
            var bazen = KorpicaPodobnost.USezoni(
                    await KorpicaPodobnost.UpitPodobnih(db.Products)
                        .Where(p => kategorije.Contains(p.CategoryId))
                        .ToListAsync(ct),
                    mjesec)
                .OrderBy(p => p.PrioritetUKorpici)
                .ThenBy(_ => Guid.NewGuid())      // nasumično unutar istog prioriteta
                .ToList();
```

- [ ] **Step 5: Pokreni cio paket**

Run: `cd backend && dotnet test`
Expected: PASS — 115 testova. **Svi postojeći testovi generatora moraju i dalje prolaziti** — ako ijedan padne, predikat nije ekvivalentan starom uslovu i to je greška ovog taska, ne tih testova.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "refactor(korpice): jedan predikat podobnosti za generator i editor"
```

---

### Task 4: Entitet `StavkaKorpiceNarudzbe`

**Files:**
- Create: `backend/src/EPiljara.Domain/Entities/StavkaKorpiceNarudzbe.cs`
- Modify: `backend/src/EPiljara.Domain/Entities/OrderItem.cs`
- Modify: `backend/src/EPiljara.Application/Common/Interfaces/IAppDbContext.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs`
- Create: migracija `KorpicaNaNarudzbi`

**Interfaces:**
- Produces: `StavkaKorpiceNarudzbe`, `IAppDbContext.StavkeKorpiceNarudzbe`, `OrderItem.KorpicaStavke`

- [ ] **Step 1: Napravi entitet**

Novi fajl `backend/src/EPiljara.Domain/Entities/StavkaKorpiceNarudzbe.cs`:

```csharp
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Jedan artikal u korpici JEDNE narudžbe.
///
/// NE miješati sa <see cref="StavkaSadrzaja"/>: ta pripada sedmičnom šablonu i dijele je svi
/// kupci, a ova pripada jednom kupcu i on je smije mijenjati do roka.
///
/// Veza ide na <see cref="OrderItem"/>, ne na Order: jedna narudžba može nositi dvije
/// različite korpice i svaka traži svoj sadržaj.
///
/// Naziv, jedinica i cijena su snapshot, iz istog razloga kao na OrderItem-u — kasnija izmjena
/// artikla ne smije pomjeriti ono što je kupac kupio.
/// </summary>
public class StavkaKorpiceNarudzbe : Entity
{
    /// <summary>OrderItem čiji Product ima Vrsta == Korpica.</summary>
    public Guid OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Unit Unit { get; set; }
    public decimal Kolicina { get; set; }
    public decimal JedinicnaCijena { get; set; }

    /// <summary>
    /// True kad je kupac sam dodao artikal, false kad je stigao iz šablona. Pakerima je to
    /// razlika između „ovo ide u svaku kutiju" i „ovo je nečiji izbor".
    /// </summary>
    public bool DodaoKupac { get; set; }
}
```

- [ ] **Step 2: Dodaj kolekciju na `OrderItem`**

U `backend/src/EPiljara.Domain/Entities/OrderItem.cs`, poslije `public decimal LineTotal => …;`:

```csharp
    /// <summary>
    /// Sadržaj korpice, kad je ovaj red korpica (Product.Vrsta == Korpica). Za obične
    /// artikle je prazan. Čuva se JEDNOM po redu — dvije kutije iste korpice
    /// (Quantity == 2) dijele isti sadržaj, a pakerska lista ga množi sa Quantity.
    /// </summary>
    public ICollection<StavkaKorpiceNarudzbe> KorpicaStavke { get; set; }
        = new List<StavkaKorpiceNarudzbe>();
```

- [ ] **Step 3: Dodaj `DbSet` u interfejs i kontekst**

U `backend/src/EPiljara.Application/Common/Interfaces/IAppDbContext.cs`, poslije `DbSet<StavkaSadrzaja> StavkeSadrzaja { get; }`:

```csharp
    DbSet<StavkaKorpiceNarudzbe> StavkeKorpiceNarudzbe { get; }
```

U `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs`, uz ostale `DbSet` property-je (oko linije 28):

```csharp
    public DbSet<StavkaKorpiceNarudzbe> StavkeKorpiceNarudzbe => Set<StavkaKorpiceNarudzbe>();
```

- [ ] **Step 4: Dodaj EF konfiguraciju**

U istom fajlu, odmah poslije bloka `b.Entity<StavkaSadrzaja>(e => { … });`:

```csharp
        b.Entity<StavkaKorpiceNarudzbe>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Unit).HasConversion<string>().HasMaxLength(8);
            e.Property(x => x.Kolicina).HasPrecision(10, 3);
            e.Property(x => x.JedinicnaCijena).HasPrecision(12, 2);
            e.HasOne(x => x.OrderItem).WithMany(i => i.KorpicaStavke)
                .HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Cascade);
            // Restrict, ne Cascade: brisanje artikla ne smije obrisati red iz nečije
            // narudžbe. Isto kao na StavkaSadrzaja.
            e.HasOne<Product>().WithMany()
                .HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.OrderItemId);
        });
```

- [ ] **Step 5: Napravi i primijeni migraciju**

```bash
cd backend
dotnet ef migrations add KorpicaNaNarudzbi \
  --project src/EPiljara.Infrastructure --startup-project src/EPiljara.API
dotnet ef database update \
  --project src/EPiljara.Infrastructure --startup-project src/EPiljara.API
```

Otvori generisanu migraciju i potvrdi da pravi **samo** tabelu `StavkeKorpiceNarudzbe` sa svojim indeksima i stranim ključevima. Ako dira bilo koju drugu tabelu, nešto je ostalo nesinhronizovano iz ranijeg taska — prijavi to umjesto da prihvatiš.

- [ ] **Step 6: Pokreni cio paket**

Run: `cd backend && dotnet test`
Expected: PASS — 115 testova, nijedan nov. Šema se mijenja, ponašanje još ne.

- [ ] **Step 7: Commit**

```bash
git add backend/
git commit -m "feat(korpice): entitet StavkaKorpiceNarudzbe — sadrzaj korpice po narudzbi"
```

---

### Task 5: Snimanje sadržaja pri naručivanju

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs`
- Test: `backend/tests/EPiljara.Application.Tests/CreateOrderKorpicaTests.cs` (nov)

**Interfaces:**
- Consumes: `StavkaKorpiceNarudzbe`, `Order.Godina`/`Sedmica`, `SedmicniSadrzaj`
- Produces: greška `korpica_bez_sadrzaja`; popunjen `OrderItem.KorpicaStavke`

- [ ] **Step 1: Napiši testove koji padaju**

Novi fajl `backend/tests/EPiljara.Application.Tests/CreateOrderKorpicaTests.cs`. Priprema mora napraviti korpicu kao `Product` sa `Vrsta == Korpica` i objavljen `SedmicniSadrzaj` za **sedmicu koju `SedmicaZaNarudzbu` vrati**, jer handler traži baš tu:

```csharp
using EPiljara.Application.Features.Korpice;
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class CreateOrderKorpicaTests
{
    [Fact]
    public async Task Korpica_dobija_snapshot_sadrzaja_te_sedmice()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);

        var dto = await t.PosaljiKorpicuAsync(kolicina: 1m);

        t.Db.ChangeTracker.Clear();
        var stavke = await t.Db.StavkeKorpiceNarudzbe
            .AsNoTracking()
            .Where(s => s.OrderItem!.OrderId == dto.Id)
            .ToListAsync();

        Assert.Equal(2, stavke.Count);
        Assert.All(stavke, s => Assert.False(s.DodaoKupac));
        Assert.Contains(stavke, s => s.ProductName == "Krompir" && s.Kolicina == 2m);
        Assert.Contains(stavke, s => s.ProductName == "Paprika" && s.Kolicina == 1m);
    }

    [Fact]
    public async Task Korpica_bez_objavljenog_sadrzaja_se_ne_moze_naruciti()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: false);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.PosaljiKorpicuAsync(kolicina: 1m));

        Assert.Equal("korpica_bez_sadrzaja", ex.Code);
    }

    [Fact]
    public async Task Sadrzaj_se_cuva_jednom_bez_obzira_na_kolicinu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);

        var dto = await t.PosaljiKorpicuAsync(kolicina: 3m);

        t.Db.ChangeTracker.Clear();
        var broj = await t.Db.StavkeKorpiceNarudzbe
            .AsNoTracking()
            .CountAsync(s => s.OrderItem!.OrderId == dto.Id);

        // Tri kutije, ali sadržaj je i dalje dva reda. Množenje je posao pakerske liste.
        Assert.Equal(2, broj);
    }

    [Fact]
    public async Task Obican_artikal_ne_dobija_nijednu_stavku_korpice()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);

        var dto = await t.PosaljiObicanArtikalAsync();

        t.Db.ChangeTracker.Clear();
        var broj = await t.Db.StavkeKorpiceNarudzbe
            .AsNoTracking()
            .CountAsync(s => s.OrderItem!.OrderId == dto.Id);

        Assert.Equal(0, broj);
    }
}
```

**Napomena za implementera:** `Scenario` je pomoćnik koji ovaj plan ne propisuje do posljednjeg reda, jer zavisi od toga šta već postoji u `backend/tests/`. Napravi ga u istom fajlu (ili kao zaseban `KorpicaScenario.cs` ako ga koristiš i u Tasku 6/7 — provjeri prvo da li ti treba tamo) tako da:

- naslijedi/omota `TestDb` i bude `IDisposable`
- napravi zonu (`MinOrderAmount = 0`), slot (`DayOfWeek = 4`, `TimeFrom = "16:00"`, `CutoffHours = 48`), kategoriju, proizvođača
- napravi dva obična artikla: `Krompir` (2 KM/kg) i `Paprika` (3 KM/kg), oba `MozeUKorpicu = true`, `IsAvailable`, `Stock = 100`, `Mjeseci = []`
- napravi korpicu: `Product` sa `Vrsta = VrstaProizvoda.Korpica`, `TipKorpice = Povrce`, `Velicina = Mala`, `Price = 10m`
- kad je `objaviSadrzaj: true`, napravi `SedmicniSadrzaj` sa `Status = Objavljen`, `CiljnaVrijednost = 7m`, za **sedmicu koju vrati `SedmicaHelper.SedmicaZaNarudzbu(DayOfWeek.Thursday, "16:00", 48)`**, sa stavkama Krompir ×2 i Paprika ×1
- `PosaljiKorpicuAsync(decimal kolicina)` i `PosaljiObicanArtikalAsync()` sklope `CreateOrderCommand` i pozovu `CreateOrderHandler`

Ne zakucavaj godinu i sedmicu u scenariju — test koji prolazi u oktobru a pada u decembru je gori od testa koji ne postoji.

- [ ] **Step 2: Pokreni testove, potvrdi da padaju**

Run: `cd backend && dotnet test --filter CreateOrderKorpicaTests`
Expected: FAIL — prva tri testa; `Assert.Equal(2, stavke.Count)` vidi 0, a `korpica_bez_sadrzaja` se nikad ne baca.

- [ ] **Step 3: Snimi sadržaj u handleru**

U `backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs`, unutar `foreach (var line in cmd.Items)`, odmah poslije `items.Add(new OrderItem { … });` — ali tako da imaš referencu na dodati red. Promijeni to u:

```csharp
            var stavka = new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Unit = product.Unit,
                Quantity = line.Quantity,
                UnitPrice = product.Price, // price snapshot
                ImageUrl = product.ImageUrl,
            };

            // Korpica nosi sadržaj sedmice koju narudžba isporučuje. Snima se, ne referencira:
            // admin koji kasnije izmijeni šablon ne smije promijeniti ono što je kupac kupio.
            if (product.Vrsta == VrstaProizvoda.Korpica)
            {
                var sadrzaj = await db.SedmicniSadrzaji
                    .Include(s => s.Stavke)
                    .FirstOrDefaultAsync(
                        s => s.KorpicaId == product.Id
                             && s.Godina == godina
                             && s.Sedmica == sedmica
                             && s.Status == StatusSadrzaja.Objavljen, ct)
                    ?? throw new DomainException(
                        $"Za korpicu „{product.Name}" još nema objavljenog sadržaja za tu sedmicu.",
                        "korpica_bez_sadrzaja");

                foreach (var ss in sadrzaj.Stavke)
                {
                    var artikal = await db.Products.FirstOrDefaultAsync(p => p.Id == ss.ProductId, ct);
                    stavka.KorpicaStavke.Add(new StavkaKorpiceNarudzbe
                    {
                        ProductId = ss.ProductId,
                        ProductName = artikal?.Name ?? string.Empty,
                        Unit = artikal?.Unit ?? Unit.Kom,
                        Kolicina = ss.Kolicina,
                        JedinicnaCijena = ss.JedinicnaCijena,
                        DodaoKupac = false,
                    });
                }
            }

            items.Add(stavka);
```

Na vrh fajla dodaj `using Unit = EPiljara.Domain.Enums.Unit;` **ako** prevodilac prijavi CS0104 — fajl koristi i `MediatR` i `EPiljara.Domain.Enums`, pa se `Unit` može sudariti.

- [ ] **Step 4: Pokreni testove, potvrdi da prolaze**

Run: `cd backend && dotnet test --filter CreateOrderKorpicaTests`
Expected: PASS — 4 testa.

- [ ] **Step 5: Pokreni cio paket**

Run: `cd backend && dotnet test`
Expected: PASS — 119 testova.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "feat(korpice): narudzba snima sadrzaj korpice te sedmice

Korpica bez objavljenog sadrzaja za svoju sedmicu se ne moze naruciti —
alternativa je da kupac plati praznu kutiju."
```

---

### Task 6: Pakerska lista broji narudžbe

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Korpice/AdminKorpice.cs:163-208`
- Modify: `backend/src/EPiljara.Application/DTOs/KorpiceDtos.cs` (DTO `StavkaPakerskeDto`)
- Test: `backend/tests/EPiljara.Application.Tests/PakerskaListaTests.cs` (nov ili dopuna postojećeg — provjeri `ls backend/tests/EPiljara.Application.Tests/ | grep -i paker`)

**Interfaces:**
- Consumes: `StavkaKorpiceNarudzbe`, `Order.Godina`/`Sedmica`
- Produces: `GetPakerskaListaQuery` vraća `PakerskaListaDto { BrojNarudzbi, Stavke }`

- [ ] **Step 1: Napiši testove koji padaju**

Novi fajl `backend/tests/EPiljara.Application.Tests/PakerskaListaTests.cs`:

```csharp
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class PakerskaListaTests
{
    [Fact]
    public async Task Dvije_narudzbe_iste_korpice_daju_dvostruku_kolicinu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        t.Db.ChangeTracker.Clear();

        var lista = await t.PakerskaListaAsync();

        Assert.Equal(2, lista.BrojNarudzbi);
        var krompir = lista.Stavke.Single(s => s.Artikal == "Krompir");
        Assert.Equal(4m, krompir.Kolicina);   // 2 po korpici × 2 narudžbe
    }

    [Fact]
    public async Task Kolicina_reda_mnozi_sadrzaj()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await t.PosaljiKorpicuAsync(kolicina: 3m);
        t.Db.ChangeTracker.Clear();

        var lista = await t.PakerskaListaAsync();

        var krompir = lista.Stavke.Single(s => s.Artikal == "Krompir");
        Assert.Equal(6m, krompir.Kolicina);   // 2 × 3 kutije
    }

    [Fact]
    public async Task Otkazana_narudzba_se_ne_broji()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        var zadrzana = await t.PosaljiKorpicuAsync(kolicina: 1m);
        var otkazana = await t.PosaljiKorpicuAsync(kolicina: 1m);

        var o = await t.Db.Orders.FirstAsync(x => x.Id == otkazana.Id);
        o.Status = OrderStatus.Cancelled;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var lista = await t.PakerskaListaAsync();

        Assert.Equal(1, lista.BrojNarudzbi);
        Assert.Equal(2m, lista.Stavke.Single(s => s.Artikal == "Krompir").Kolicina);
    }

    [Fact]
    public async Task Bez_ijedne_narudzbe_lista_je_prazna_a_ne_sablon()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        t.Db.ChangeTracker.Clear();

        var lista = await t.PakerskaListaAsync();

        Assert.Equal(0, lista.BrojNarudzbi);
        Assert.Empty(lista.Stavke);
    }
}
```

**Napomena za implementera:** `Scenario` je isti pomoćnik iz Taska 5. Ako si ga tamo napravio kao `file`-scoped tip u test fajlu, izdvoj ga sada u `backend/tests/EPiljara.Application.Tests/Scenario.cs` kao `internal` i dodaj mu `PakerskaListaAsync()` koji zove `GetPakerskaListaHandler` za sedmicu scenarija. `PosaljiKorpicuAsync` mora vraćati `OrderDto` da bi ovi testovi mogli naći narudžbu.

- [ ] **Step 2: Pokreni testove, potvrdi da padaju**

Run: `cd backend && dotnet test --filter PakerskaListaTests`
Expected: FAIL — `GetPakerskaListaQuery` vraća `IReadOnlyList<StavkaPakerskeDto>`, nema `BrojNarudzbi` (CS1061).

- [ ] **Step 3: Dodaj DTO omotač**

U `backend/src/EPiljara.Application/DTOs/KorpiceDtos.cs`, odmah ispred `StavkaPakerskeDto`:

```csharp
/// <summary>
/// Pakerska lista za jednu sedmicu. Nosi i broj narudžbi, jer prazna lista i „niko nije
/// naručio" izgledaju isto a znače različito — prije P2b je lista u toj situaciji pokazivala
/// sadržaj šablona, pa operater koji to pamti mora vidjeti nulu, ne prazninu.
/// </summary>
public record PakerskaListaDto
{
    public int BrojNarudzbi { get; init; }
    public IReadOnlyList<StavkaPakerskeDto> Stavke { get; init; } = [];
}
```

- [ ] **Step 4: Prepiši handler**

U `backend/src/EPiljara.Application/Features/Korpice/AdminKorpice.cs`, zamijeni cio blok `/* ---------- pakerska lista ---------- */` (linije 163–208) ovim:

```csharp
/* ---------- pakerska lista ---------- */

public record GetPakerskaListaQuery(int Godina, int Sedmica) : IRequest<PakerskaListaDto>;

public class GetPakerskaListaHandler(IAppDbContext db)
    : IRequestHandler<GetPakerskaListaQuery, PakerskaListaDto>
{
    public async Task<PakerskaListaDto> Handle(GetPakerskaListaQuery q, CancellationToken ct)
    {
        // Od P2b lista sabira SADRŽAJ NARUDŽBI, ne šablon. Prije toga je sabirala
        // StavkeSadrzaja — redove jedne kutije — pa je četrdeset narudžbi i jedna davale isti
        // broj, i nabavka po toj listi je bila za jednu korpicu.
        //
        // IgnoreQueryFilters(): bez toga EF primjenjuje Product-ov globalni !IsDeleted filter
        // i na Include lanac, pa je za soft-obrisan artikal Product null. Takav red se i dalje
        // prikazuje sa stvarnom količinom i obilježava — pakerska lista je procurement istina
        // i ne smije praviti prazne redove.
        var redovi = await db.StavkeKorpiceNarudzbe
            .IgnoreQueryFilters()
            .Include(s => s.OrderItem).ThenInclude(i => i!.Order)
            .Where(s => s.OrderItem!.Order!.Godina == q.Godina
                        && s.OrderItem.Order.Sedmica == q.Sedmica
                        && s.OrderItem.Order.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);

        var brojNarudzbi = redovi
            .Select(s => s.OrderItem!.OrderId)
            .Distinct()
            .Count();

        // Artikal se dohvata posebno, jer StavkaKorpiceNarudzbe namjerno nema navigaciju na
        // Product — snapshot naziva živi na samom redu, a proizvođač i selo se čitaju iz
        // trenutnog kataloga jer su podatak o nabavci, ne o prodaji.
        var idjevi = redovi.Select(s => s.ProductId).Distinct().ToList();
        var artikli = await db.Products
            .IgnoreQueryFilters()
            .Include(p => p.Producer)
            .Where(p => idjevi.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var stavke = redovi
            .GroupBy(s => s.ProductId)
            .Select(g =>
            {
                artikli.TryGetValue(g.Key, out var p);
                var prvi = g.First();
                return new StavkaPakerskeDto
                {
                    Artikal = p?.Name ?? prvi.ProductName,
                    Kolicina = g.Sum(x => x.Kolicina * x.OrderItem!.Quantity),
                    Jedinica = (p?.Unit ?? prvi.Unit).ToApi(),
                    Proizvodjac = p?.Producer?.Name ?? string.Empty,
                    Selo = p?.Producer?.Village ?? string.Empty,
                    ArtikalObrisan = p is null || p.IsDeleted,
                };
            })
            .OrderBy(r => r.Proizvodjac).ThenBy(r => r.Artikal)
            .ToList();

        return new PakerskaListaDto { BrojNarudzbi = brojNarudzbi, Stavke = stavke };
    }
}
```

- [ ] **Step 5: Popravi kontroler**

Endpoint koji vraća pakersku listu sada vraća objekat, ne listu — `EnvelopeList` mu više ne odgovara. U `backend/src/EPiljara.Api/Controllers/AdminController.cs` nađi akciju koja zove `GetPakerskaListaQuery` (`grep -n "PakerskaLista" backend/src/EPiljara.Api/Controllers/AdminController.cs`) i promijeni `EnvelopeList(...)` u `Envelope(...)`.

- [ ] **Step 6: Pokreni testove, potvrdi da prolaze**

Run: `cd backend && dotnet test --filter PakerskaListaTests`
Expected: PASS — 4 testa.

Zatim cio paket: `cd backend && dotnet test`
Expected: PASS. **Postojeći testovi pakerske liste koji tvrde da lista vraća sadržaj šablona sada moraju pasti** — to je namjerna promjena ponašanja, ne regresija. Takav test prepiši da koristi narudžbe, i u commit poruci reci koji su to testovi bili.

- [ ] **Step 7: Commit**

```bash
git add backend/
git commit -m "feat(korpice): pakerska lista sabira narudzbe umjesto sablona

Prije ovoga je cetrdeset narudzbi i jedna davale isti broj, pa je nabavka
po toj listi bila za jednu korpicu. Lista nosi i BrojNarudzbi, da se prazno
stanje cita kao nula narudzbi a ne kao kvar."
```

---

### Task 7: Editor — upiti

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Korpice/MojaKorpica.cs`
- Modify: `backend/src/EPiljara.Application/DTOs/KorpiceDtos.cs`
- Modify: `backend/src/EPiljara.Api/Controllers/AccountController.cs`
- Test: `backend/tests/EPiljara.Application.Tests/MojaKorpicaUpitiTests.cs` (nov)

**Interfaces:**
- Consumes: `KorpicaPodobnost`, `SedmicaHelper.UcitajRokAsync`, `StavkaKorpiceNarudzbe`
- Produces:
  - `GetMojeKorpiceQuery : IRequest<IReadOnlyList<MojaKorpicaDto>>`
  - `GetMojaKorpicaQuery(Guid OrderItemId) : IRequest<MojaKorpicaDetaljiDto>`
  - DTO-i `MojaKorpicaDto`, `MojaKorpicaDetaljiDto`, `StavkaMojeKorpiceDto`, `PonudaArtiklaDto`

- [ ] **Step 1: Dodaj DTO-e**

U `backend/src/EPiljara.Application/DTOs/KorpiceDtos.cs`, na kraj fajla:

```csharp
/// <summary>Jedna korpica koju kupac trenutno može mijenjati.</summary>
public record MojaKorpicaDto
{
    public Guid OrderItemId { get; init; }
    public Guid OrderId { get; init; }
    public string KodNarudzbe { get; init; } = string.Empty;
    public string Naziv { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public int Godina { get; init; }
    public int Sedmica { get; init; }
    public DateTime RokIzmjene { get; init; }
    public decimal CiljnaVrijednost { get; init; }
    public decimal TrenutnaVrijednost { get; init; }
    public int BrojArtikala { get; init; }
    public int MinimalanBrojArtikala { get; init; }
}

public record StavkaMojeKorpiceDto
{
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public decimal Kolicina { get; init; }
    public string Jedinica { get; init; } = "kom";
    public decimal JedinicnaCijena { get; init; }
    public bool DodaoKupac { get; init; }
}

/// <summary>Artikal koji kupac može dodati u korpicu.</summary>
public record PonudaArtiklaDto
{
    public Guid ProductId { get; init; }
    public string Naziv { get; init; } = string.Empty;
    public string Jedinica { get; init; } = "kom";
    public decimal Cijena { get; init; }
    public string? ImageUrl { get; init; }
    /// <summary>Artikal pripada kategorijama ove korpice. Birač takve stavlja prve.</summary>
    public bool IzOveKorpice { get; init; }
}

public record MojaKorpicaDetaljiDto
{
    public MojaKorpicaDto Korpica { get; init; } = new();
    public IReadOnlyList<StavkaMojeKorpiceDto> Stavke { get; init; } = [];
    public IReadOnlyList<PonudaArtiklaDto> Ponuda { get; init; } = [];
}
```

- [ ] **Step 2: Napiši testove koji padaju**

Novi fajl `backend/tests/EPiljara.Application.Tests/MojaKorpicaUpitiTests.cs`:

```csharp
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class MojaKorpicaUpitiTests
{
    [Fact]
    public async Task Lista_vraca_korpicu_koja_se_jos_moze_mijenjati()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        t.Db.ChangeTracker.Clear();

        var moje = await t.MojeKorpiceAsync();

        var jedna = Assert.Single(moje);
        Assert.Equal(7m, jedna.TrenutnaVrijednost);   // 2×2 + 1×3, zbir redova narudžbe
        Assert.Equal(2, jedna.BrojArtikala);
    }

    [Fact]
    public async Task CiljnaVrijednost_dolazi_sa_sadrzaja_a_ne_iz_zbira_redova()
    {
        // Scenario upisuje CiljnaVrijednost = 8.00 iako sadržaj sabira 7.00. To nije realno
        // stanje — pri objavi su jednaki — ali je jedini način da se dokaže ODAKLE vrijednost
        // dolazi. Da handler računa cilj iz redova, oba broja bi bila 7.00 i provjera budžeta
        // iz editora bi uvijek davala 100%.
        using var t = await Scenario.NapraviAsync(
            objaviSadrzaj: true, prijavljen: true, ciljnaVrijednost: 8m);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        t.Db.ChangeTracker.Clear();

        var jedna = Assert.Single(await t.MojeKorpiceAsync());

        Assert.Equal(8m, jedna.CiljnaVrijednost);
        Assert.Equal(7m, jedna.TrenutnaVrijednost);
    }

    [Fact]
    public async Task Lista_ne_vraca_tudju_korpicu()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        t.Db.ChangeTracker.Clear();

        var moje = await t.MojeKorpiceAsync(kao: Guid.NewGuid());

        Assert.Empty(moje);
    }

    [Fact]
    public async Task Lista_ne_vraca_korpicu_narudzbe_koja_se_vec_pakuje()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(kolicina: 1m);

        var o = await t.Db.Orders.FirstAsync(x => x.Id == dto.Id);
        o.Status = OrderStatus.Confirmed;
        o.Status = OrderStatus.Packing;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        Assert.Empty(await t.MojeKorpiceAsync());
    }

    [Fact]
    public async Task Detalji_nose_stavke_i_ponudu_sa_oznakom_kategorije()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(kolicina: 1m);
        t.Db.ChangeTracker.Clear();

        var d = await t.MojaKorpicaAsync(await t.OrderItemIdAsync(dto.Id));

        Assert.Equal(2, d.Stavke.Count);
        Assert.All(d.Stavke, s => Assert.False(s.DodaoKupac));
        // Ponuda je cijela sezonska ponuda (odluka P2b), ne samo kategorije ove korpice.
        Assert.Contains(d.Ponuda, p => p.Naziv == "Sir" && !p.IzOveKorpice);
        Assert.Contains(d.Ponuda, p => p.Naziv == "Krompir" && p.IzOveKorpice);
    }

    [Fact]
    public async Task Detalji_tudje_korpice_daju_403()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(kolicina: 1m);
        var orderItemId = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<EPiljara.Domain.Common.ForbiddenException>(
            () => t.MojaKorpicaAsync(orderItemId, kao: Guid.NewGuid()));
    }
}
```

**Napomena za implementera:** `Scenario` iz Taska 5/6 treba proširiti:

- parametar `prijavljen` — kad je `true`, `FakeCurrentUser` nosi id kupca i narudžba dobija `CustomerId`
- parametar `ciljnaVrijednost` sa podrazumijevanom vrijednošću koja odgovara zbiru sadržaja (7.00), da ga samo jedan test može namjerno razići
- artikal `Sir` (3 KM, 1 kom) u **drugoj** kategoriji, da `IzOveKorpice == false` ima šta da pokaže
- pomoćnici `MojeKorpiceAsync(Guid? kao = null)`, `MojaKorpicaAsync(Guid orderItemId, Guid? kao = null)`, `OrderItemIdAsync(Guid orderId)`

- [ ] **Step 3: Pokreni testove, potvrdi da padaju**

Run: `cd backend && dotnet test --filter MojaKorpicaUpitiTests`
Expected: FAIL — tipovi upita ne postoje.

- [ ] **Step 4: Napiši upite**

Novi fajl `backend/src/EPiljara.Application/Features/Korpice/MojaKorpica.cs`:

```csharp
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Korpice;

/// <summary>
/// Kupčev editor sadržaja korpice. Sve tri tačke (lista, detalji, izmjena) dijele isti pojam
/// „korpica koja se još može mijenjati" — vidi <see cref="MojaKorpicaPravila"/>.
/// </summary>
public static class MojaKorpicaPravila
{
    /// <summary>
    /// Statusi narudžbe u kojima se sadržaj još smije mijenjati. Od Packing nadalje su pakeri
    /// već dobili listu, pa izmjena koja se ne vidi u magacinu nije izmjena nego laž kupcu.
    /// </summary>
    public static readonly OrderStatus[] StatusiKojiDozvoljavaju =
        [OrderStatus.Pending, OrderStatus.Confirmed];
}

public record GetMojeKorpiceQuery : IRequest<IReadOnlyList<MojaKorpicaDto>>;

public class GetMojeKorpiceHandler(
    IAppDbContext db, ICurrentUser current, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetMojeKorpiceQuery, IReadOnlyList<MojaKorpicaDto>>
{
    public async Task<IReadOnlyList<MojaKorpicaDto>> Handle(
        GetMojeKorpiceQuery q, CancellationToken ct)
    {
        if (!current.IsAuthenticated) return [];

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);

        var redovi = await db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.KorpicaStavke)
            .Where(i => i.Order!.CustomerId == current.Id
                        && i.KorpicaStavke.Any()
                        && MojaKorpicaPravila.StatusiKojiDozvoljavaju.Contains(i.Order.Status))
            .ToListAsync(ct);

        var korpiceIdjevi = redovi.Select(r => r.ProductId).Distinct().ToList();

        var korpice = await db.Products
            .Where(p => korpiceIdjevi.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        // CiljnaVrijednost je ZAMRZNUTA vrijednost sa sedmičnog sadržaja, upisana pri objavi.
        // NE smije se računati iz redova narudžbe: oni se mijenjaju kad kupac izbaci artikal,
        // pa bi budžet uvijek ispadao 100% i cijela provjera iz editora bila besmislena.
        var sedmice = redovi
            .Select(r => (r.ProductId, r.Order!.Godina, r.Order.Sedmica))
            .Distinct()
            .ToList();

        var sadrzaji = await db.SedmicniSadrzaji
            .Where(s => korpiceIdjevi.Contains(s.KorpicaId))
            .Select(s => new { s.KorpicaId, s.Godina, s.Sedmica, s.CiljnaVrijednost })
            .ToListAsync(ct);

        var ciljevi = sadrzaji
            .Where(s => sedmice.Contains((s.KorpicaId, s.Godina, s.Sedmica)))
            .ToDictionary(s => (s.KorpicaId, s.Godina, s.Sedmica), s => s.CiljnaVrijednost);

        var sada = DateTime.UtcNow;

        return redovi
            .Select(i => Sastavi(i, korpice, ciljevi, dan, vrijemeOd, cutoff, opcije.Value))
            .Where(d => d.RokIzmjene > sada)
            .OrderBy(d => d.RokIzmjene)
            .ToList();
    }

    internal static MojaKorpicaDto Sastavi(
        OrderItem red,
        IReadOnlyDictionary<Guid, Product> korpice,
        IReadOnlyDictionary<(Guid, int, int), decimal> ciljevi,
        DayOfWeek dan, string vrijemeOd, int cutoff,
        KorpiceOpcije opcije)
    {
        korpice.TryGetValue(red.ProductId, out var korpica);
        var o = red.Order!;
        var rok = SedmicaHelper.DatumIsporuke(o.Godina, o.Sedmica, dan, vrijemeOd)
            .AddHours(-cutoff);

        // Kad sadržaja nema (obrisan poslije narudžbe), cilj pada na zbir redova — tada je
        // odnos 100% i editor nikog ne blokira. Bolje nego dijeliti nulom ili tvrditi budžet
        // koji ne postoji.
        var trenutna = Math.Round(red.KorpicaStavke.Sum(s => s.Kolicina * s.JedinicnaCijena), 2);
        var cilj = ciljevi.TryGetValue((red.ProductId, o.Godina, o.Sedmica), out var c) && c > 0m
            ? c
            : trenutna;

        return new MojaKorpicaDto
        {
            OrderItemId = red.Id,
            OrderId = o.Id,
            KodNarudzbe = o.Code,
            Naziv = red.ProductName,
            ImageUrl = red.ImageUrl,
            Godina = o.Godina,
            Sedmica = o.Sedmica,
            RokIzmjene = rok,
            CiljnaVrijednost = cilj,
            TrenutnaVrijednost = trenutna,
            BrojArtikala = red.KorpicaStavke.Count,
            MinimalanBrojArtikala = opcije.MinimalanBrojArtikala.GetValueOrDefault(
                korpica?.Velicina ?? VelicinaKorpice.Mala, 5),
        };
    }
}
```

Nastavi isti fajl upitom za detalje:

```csharp
public record GetMojaKorpicaQuery(Guid OrderItemId) : IRequest<MojaKorpicaDetaljiDto>;

public class GetMojaKorpicaHandler(
    IAppDbContext db, ICurrentUser current, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetMojaKorpicaQuery, MojaKorpicaDetaljiDto>
{
    public async Task<MojaKorpicaDetaljiDto> Handle(GetMojaKorpicaQuery q, CancellationToken ct)
    {
        var red = await db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.KorpicaStavke)
            .FirstOrDefaultAsync(i => i.Id == q.OrderItemId, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");

        if (!current.IsAuthenticated || red.Order!.CustomerId != current.Id)
            throw new ForbiddenException("Ova korpica nije vaša.", "forbidden");

        var korpica = await db.Products.FirstOrDefaultAsync(p => p.Id == red.ProductId, ct);

        // Ponuda je CIJELA sezonska ponuda — odluka P2b. Artikli iz kategorija ove korpice se
        // obilježavaju da ih birač može staviti prve, ali se ništa ne zabranjuje.
        var kategorijeKorpice = await db.KorpicaKategorije
            .Where(k => k.KorpicaId == red.ProductId)
            .Select(k => k.CategoryId)
            .ToListAsync(ct);

        var mjesec = SedmicaHelper
            .DatumIsporuke(red.Order.Godina, red.Order.Sedmica, DayOfWeek.Monday, "00:00")
            .Month;

        var ponuda = KorpicaPodobnost
            .USezoni(await KorpicaPodobnost.UpitPodobnih(db.Products).ToListAsync(ct), mjesec)
            .Select(p => new PonudaArtiklaDto
            {
                ProductId = p.Id,
                Naziv = p.Name,
                Jedinica = p.Unit.ToApi(),
                Cijena = p.Price,
                ImageUrl = p.ImageUrl,
                IzOveKorpice = kategorijeKorpice.Contains(p.CategoryId),
            })
            .OrderByDescending(p => p.IzOveKorpice)
            .ThenBy(p => p.Naziv)
            .ToList();

        var cilj = await db.SedmicniSadrzaji
            .Where(s => s.KorpicaId == red.ProductId
                        && s.Godina == red.Order.Godina
                        && s.Sedmica == red.Order.Sedmica)
            .Select(s => (decimal?)s.CiljnaVrijednost)
            .FirstOrDefaultAsync(ct);

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var zaglavlje = GetMojeKorpiceHandler.Sastavi(
            red,
            korpica is null ? new Dictionary<Guid, Product>() : new() { [korpica.Id] = korpica },
            cilj is null
                ? new Dictionary<(Guid, int, int), decimal>()
                : new() { [(red.ProductId, red.Order.Godina, red.Order.Sedmica)] = cilj.Value },
            dan, vrijemeOd, cutoff, opcije.Value);

        return new MojaKorpicaDetaljiDto
        {
            Korpica = zaglavlje,
            Stavke = red.KorpicaStavke
                .Select(s => new StavkaMojeKorpiceDto
                {
                    ProductId = s.ProductId,
                    Naziv = s.ProductName,
                    Kolicina = s.Kolicina,
                    Jedinica = s.Unit.ToApi(),
                    JedinicnaCijena = s.JedinicnaCijena,
                    DodaoKupac = s.DodaoKupac,
                })
                .OrderBy(s => s.Naziv)
                .ToList(),
            Ponuda = ponuda,
        };
    }
}
```

- [ ] **Step 5: Dodaj endpointe**

U `backend/src/EPiljara.Api/Controllers/AccountController.cs`, poslije akcije `UpdateProfile`:

```csharp
    [HttpGet("account/korpice")]
    public async Task<IActionResult> MojeKorpice() =>
        EnvelopeList(await Mediator.Send(new GetMojeKorpiceQuery()));

    [HttpGet("account/korpice/{orderItemId:guid}")]
    public async Task<IActionResult> MojaKorpica(Guid orderItemId) =>
        Envelope(await Mediator.Send(new GetMojaKorpicaQuery(orderItemId)));
```

Dodaj `using EPiljara.Application.Features.Korpice;` na vrh fajla.

- [ ] **Step 6: Pokreni testove**

Run: `cd backend && dotnet test --filter MojaKorpicaUpitiTests`
Expected: PASS — 5 testova.

Zatim: `cd backend && dotnet test` — Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add backend/
git commit -m "feat(korpice): upiti kupcevog editora — lista i detalji"
```

---

### Task 8: Editor — izmjena

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Korpice/MojaKorpica.cs`
- Modify: `backend/src/EPiljara.Api/Controllers/AccountController.cs`
- Test: `backend/tests/EPiljara.Application.Tests/MojaKorpicaIzmjenaTests.cs` (nov)

**Interfaces:**
- Produces: `IzmijeniMojuKorpicuCommand(Guid OrderItemId, IReadOnlyList<Guid> ProductIds) : IRequest<MojaKorpicaDetaljiDto>`
- Kodovi grešaka: `budzet_premali`, `budzet_preveliki`, `premalo_artikala`, `rok_istekao`, `korpica_se_pakuje`, `artikal_nije_podoban`, `duplirani_artikal`

- [ ] **Step 1: Napiši testove koji padaju**

Novi fajl `backend/tests/EPiljara.Application.Tests/MojaKorpicaIzmjenaTests.cs`:

```csharp
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Scenario ima korpicu sa CiljnaVrijednost = 7.00 (Krompir 2kg×2 + Paprika 1kg×3) i
/// MinimalanBrojArtikala[Mala] = 2 — vidi Scenario.NapraviAsync.
/// </summary>
public class MojaKorpicaIzmjenaTests
{
    [Fact]
    public async Task Zamjena_u_okviru_budzeta_prolazi()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        // Krompir (2kg = 4.00) + Sir (1 kom = 3.00) → 7.00 = 100%
        var rezultat = await t.IzmijeniAsync(id, [t.KrompirId, t.SirId]);

        Assert.Equal(2, rezultat.Stavke.Count);
        var sir = rezultat.Stavke.Single(s => s.Naziv == "Sir");
        Assert.True(sir.DodaoKupac);
        Assert.Equal(1m, sir.Kolicina);          // dodat artikal dobija 1 u svojoj jedinici
        var krompir = rezultat.Stavke.Single(s => s.Naziv == "Krompir");
        Assert.False(krompir.DodaoKupac);
        Assert.Equal(2m, krompir.Kolicina);      // iz šablona zadržava šablonsku količinu
    }

    [Theory]
    [InlineData(94.9)]
    [InlineData(100.1)]
    public async Task Van_opsega_budzeta_se_odbija(double procenat)
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniNaProcenatAsync(id, procenat));

        Assert.Contains(ex.Code, new[] { "budzet_premali", "budzet_preveliki" });
    }

    [Fact]
    public async Task Ispod_minimalnog_broja_artikala_se_odbija()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniAsync(id, [t.SkupiArtikalId]));   // jedan artikal, tačno 7.00

        Assert.Equal("premalo_artikala", ex.Code);
    }

    [Fact]
    public async Task Duplirani_artikal_se_odbija()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.KrompirId]));

        Assert.Equal("duplirani_artikal", ex.Code);
    }

    [Fact]
    public async Task Nepodoban_artikal_se_odbija()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.VanSezoneId]));

        Assert.Equal("artikal_nije_podoban", ex.Code);
    }

    [Fact]
    public async Task Narudzba_koja_se_pakuje_se_ne_moze_mijenjati()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);

        var o = await t.Db.Orders.FirstAsync(x => x.Id == dto.Id);
        o.Status = OrderStatus.Confirmed;
        o.Status = OrderStatus.Packing;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.SirId]));

        Assert.Equal("korpica_se_pakuje", ex.Code);
    }

    [Fact]
    public async Task Tudja_korpica_daje_403()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<ForbiddenException>(
            () => t.IzmijeniAsync(id, [t.KrompirId, t.SirId], kao: Guid.NewGuid()));
    }

    [Fact]
    public async Task Izmjena_se_vidi_na_pakerskoj_listi()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        await t.IzmijeniAsync(id, [t.KrompirId, t.SirId]);
        t.Db.ChangeTracker.Clear();

        var lista = await t.PakerskaListaAsync();

        Assert.Contains(lista.Stavke, s => s.Artikal == "Sir");
        Assert.DoesNotContain(lista.Stavke, s => s.Artikal == "Paprika");
    }

    [Fact]
    public async Task Stari_redovi_se_ne_gomilaju()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true, prijavljen: true);
        var dto = await t.PosaljiKorpicuAsync(1m);
        var id = await t.OrderItemIdAsync(dto.Id);
        t.Db.ChangeTracker.Clear();

        await t.IzmijeniAsync(id, [t.KrompirId, t.SirId]);
        t.Db.ChangeTracker.Clear();

        var broj = await t.Db.StavkeKorpiceNarudzbe.AsNoTracking()
            .CountAsync(s => s.OrderItemId == id);

        Assert.Equal(2, broj);
    }
}
```

**Napomena za implementera:** `Scenario` treba još: artikle `Sir` (3 KM, druga kategorija), `SkupiArtikal` (7 KM, podoban), `VanSezone` (`Mjeseci = [1]` ako je sedmica scenarija izvan januara — izračunaj iz sedmice, ne zakucaj). Plus `IzmijeniAsync(Guid id, Guid[] productIds, Guid? kao = null)` i `IzmijeniNaProcenatAsync(Guid id, double procenat)`. Ovaj drugi mora sastaviti spisak artikala čija ukupna vrijednost daje traženi procenat od 7.00 KM — najlakše tako što scenariju dodaš artikle sa cijenama koje to omogućavaju (npr. 6.65 KM za 95%, 6.64 za 94.9%, 7.00 za 100%, 7.01 za 100.1%) uz drugi jeftin artikal da minimum broja artikala bude zadovoljen. Dokumentuj u izvještaju koje si cijene izabrao i zašto.

- [ ] **Step 2: Pokreni testove, potvrdi da padaju**

Run: `cd backend && dotnet test --filter MojaKorpicaIzmjenaTests`
Expected: FAIL — komanda ne postoji.

- [ ] **Step 3: Napiši komandu**

Na kraj `backend/src/EPiljara.Application/Features/Korpice/MojaKorpica.cs`:

```csharp
public record IzmijeniMojuKorpicuCommand(Guid OrderItemId, IReadOnlyList<Guid> ProductIds)
    : IRequest<MojaKorpicaDetaljiDto>;

public class IzmijeniMojuKorpicuValidator : AbstractValidator<IzmijeniMojuKorpicuCommand>
{
    public IzmijeniMojuKorpicuValidator()
    {
        RuleFor(x => x.ProductIds).NotEmpty().WithMessage("Korpica ne može ostati prazna.");
    }
}

public class IzmijeniMojuKorpicuHandler(
    IAppDbContext db, ICurrentUser current, IOptions<KorpiceOpcije> opcije, IMediator mediator)
    : IRequestHandler<IzmijeniMojuKorpicuCommand, MojaKorpicaDetaljiDto>
{
    /// <summary>Donja i gornja granica budžeta, kao udio CiljnaVrijednost.</summary>
    public const decimal DonjaGranica = 0.95m;
    public const decimal GornjaGranica = 1.00m;

    public async Task<MojaKorpicaDetaljiDto> Handle(
        IzmijeniMojuKorpicuCommand cmd, CancellationToken ct)
    {
        var red = await db.OrderItems
            .Include(i => i.Order)
            .Include(i => i.KorpicaStavke)
            .FirstOrDefaultAsync(i => i.Id == cmd.OrderItemId, ct)
            ?? throw new NotFoundException("Korpica nije pronađena.");

        if (!current.IsAuthenticated || red.Order!.CustomerId != current.Id)
            throw new ForbiddenException("Ova korpica nije vaša.", "forbidden");

        if (!MojaKorpicaPravila.StatusiKojiDozvoljavaju.Contains(red.Order.Status))
            throw new ConflictException(
                "Narudžba se već pakuje, sadržaj se više ne može mijenjati.", "korpica_se_pakuje");

        // Duplikat bi se tiho udvostručio u količini — odbijamo ga umjesto da ga sažmemo,
        // jer kupac koji je dvaput kliknuo isti artikal nije tražio dvostruku količinu.
        if (cmd.ProductIds.Distinct().Count() != cmd.ProductIds.Count)
            throw new DomainException("Isti artikal je naveden dvaput.", "duplirani_artikal");

        var (dan, vrijemeOd, cutoff) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var rok = SedmicaHelper
            .DatumIsporuke(red.Order.Godina, red.Order.Sedmica, dan, vrijemeOd)
            .AddHours(-cutoff);
        if (DateTime.UtcNow > rok)
            throw new DomainException(
                "Rok za izmjenu ove korpice je prošao.", "rok_istekao");

        var mjesec = SedmicaHelper
            .DatumIsporuke(red.Order.Godina, red.Order.Sedmica, DayOfWeek.Monday, "00:00")
            .Month;

        var podobni = KorpicaPodobnost
            .USezoni(await KorpicaPodobnost.UpitPodobnih(db.Products).ToListAsync(ct), mjesec)
            .ToDictionary(p => p.Id);

        // Količine iz šablona te sedmice: artikal koji je u šablonu zadržava svoju količinu,
        // sve ostalo dobija 1 u svojoj jedinici. Količinu NIKAD ne šalje klijent — vidi spec.
        var sadrzaj = await db.SedmicniSadrzaji
            .Include(s => s.Stavke)
            .FirstOrDefaultAsync(s => s.KorpicaId == red.ProductId
                                      && s.Godina == red.Order.Godina
                                      && s.Sedmica == red.Order.Sedmica, ct);
        var sablonskeKolicine = sadrzaj?.Stavke.ToDictionary(s => s.ProductId, s => s.Kolicina)
                                ?? [];

        var nove = new List<StavkaKorpiceNarudzbe>();
        foreach (var pid in cmd.ProductIds)
        {
            if (!podobni.TryGetValue(pid, out var p))
                throw new DomainException(
                    "Artikal nije dostupan za korpicu ove sedmice.", "artikal_nije_podoban");

            nove.Add(new StavkaKorpiceNarudzbe
            {
                OrderItemId = red.Id,
                ProductId = p.Id,
                ProductName = p.Name,
                Unit = p.Unit,
                Kolicina = sablonskeKolicine.GetValueOrDefault(p.Id, 1m),
                JedinicnaCijena = p.Price,
                DodaoKupac = !sablonskeKolicine.ContainsKey(p.Id),
            });
        }

        var cilj = sadrzaj?.CiljnaVrijednost ?? 0m;
        var vrijednost = Math.Round(nove.Sum(s => s.Kolicina * s.JedinicnaCijena), 2);

        if (cilj > 0m && vrijednost < Math.Round(cilj * DonjaGranica, 2))
            throw new DomainException(
                $"Vrijednost korpice je {vrijednost:0.##} KM, a mora biti bar " +
                $"{cilj * DonjaGranica:0.##} KM.", "budzet_premali");

        if (cilj > 0m && vrijednost > Math.Round(cilj * GornjaGranica, 2))
            throw new DomainException(
                $"Vrijednost korpice je {vrijednost:0.##} KM, a najviše može biti " +
                $"{cilj * GornjaGranica:0.##} KM.", "budzet_preveliki");

        var korpica = await db.Products.FirstOrDefaultAsync(p => p.Id == red.ProductId, ct);
        var minimum = opcije.Value.MinimalanBrojArtikala.GetValueOrDefault(
            korpica?.Velicina ?? VelicinaKorpice.Mala, 5);
        if (nove.Count < minimum)
            throw new DomainException(
                $"Korpica mora imati bar {minimum} artikala.", "premalo_artikala");

        // Stari redovi se brišu eksplicitno kroz DbSet. `Clear()` na kolekciji praćenog
        // roditelja je u P1 dao DbUpdateConcurrencyException — EF je pokušao da ih i obriše i
        // ažurira. Vidi CLAUDE.md.
        db.StavkeKorpiceNarudzbe.RemoveRange(red.KorpicaStavke);
        foreach (var s in nove) db.StavkeKorpiceNarudzbe.Add(s);

        await db.SaveChangesAsync(ct);

        return await mediator.Send(new GetMojaKorpicaQuery(red.Id), ct);
    }
}
```

Dodaj `using FluentValidation;` na vrh fajla.

**Napomena za implementera:** komentar o `Clear()` i `DbUpdateConcurrencyException` opisuje stvarnu zamku iz P1, ali `RemoveRange` nad kolekcijom koju si upravo učitao kroz `Include` može dati **drugi** oblik istog problema. Ako test `Stari_redovi_se_ne_gomilaju` padne sa `DbUpdateConcurrencyException`, pokušaj `db.ChangeTracker.Clear()` prije dodavanja novih, ili učitaj stare redove zasebnim `AsNoTracking()` upitom pa ih obriši po Id-u. Prijavi šta je proradilo.

- [ ] **Step 4: Dodaj endpoint**

U `backend/src/EPiljara.Api/Controllers/AccountController.cs`:

```csharp
    [HttpPut("account/korpice/{orderItemId:guid}")]
    public async Task<IActionResult> IzmijeniKorpicu(
        Guid orderItemId, [FromBody] IzmijeniKorpicuBody body) =>
        Envelope(await Mediator.Send(
            new IzmijeniMojuKorpicuCommand(orderItemId, body.ProductIds)));
```

i na dno fajla, uz `UpdateAddressBody`:

```csharp
/// <summary>Tijelo bez Id-a — Id dolazi iz putanje. Količine i cijene određuje server.</summary>
public record IzmijeniKorpicuBody(IReadOnlyList<Guid> ProductIds);
```

- [ ] **Step 5: Pokreni testove**

Run: `cd backend && dotnet test --filter MojaKorpicaIzmjenaTests`
Expected: PASS — 10 slučajeva (9 metoda, jedan `[Theory]` sa 2 `[InlineData]`).

Zatim: `cd backend && dotnet test` — Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "feat(korpice): kupac mijenja sadrzaj svoje korpice u okviru budzeta

PUT nosi samo identifikatore; kolicinu i cijenu odredjuje server. Artikal iz
sablona zadrzava sablonsku kolicinu, dodat dobija 1 u svojoj jedinici."
```

---

### Task 9: Frontend — tipovi i API sloj

**Files:**
- Modify: `frontend/src/types/domain.ts`
- Modify: `frontend/src/lib/api/contracts.ts`
- Modify: `frontend/src/lib/api/endpoints.ts`
- Modify: `frontend/src/lib/api/hooks.ts`

**Interfaces:**
- Produces:
  - tipovi `MojaKorpica`, `StavkaMojeKorpice`, `PonudaArtikla`, `MojaKorpicaDetalji`
  - `accountApi.mojeKorpice(): Promise<MojaKorpica[]>`
  - `accountApi.mojaKorpica(orderItemId: string): Promise<MojaKorpicaDetalji>`
  - `accountApi.izmijeniKorpicu(orderItemId: string, productIds: string[]): Promise<MojaKorpicaDetalji>`
  - `queryKeys.mojeKorpice()`, `queryKeys.mojaKorpica(id)`

- [ ] **Step 1: Dodaj tipove**

U `frontend/src/types/domain.ts`, uz ostale tipove korpica:

```ts
export interface MojaKorpica {
  orderItemId: string;
  orderId: string;
  kodNarudzbe: string;
  naziv: string;
  imageUrl?: string;
  godina: number;
  sedmica: number;
  /** ISO 8601 sa "Z" — backend ga šalje kao UTC. */
  rokIzmjene: string;
  ciljnaVrijednost: number;
  trenutnaVrijednost: number;
  brojArtikala: number;
  minimalanBrojArtikala: number;
}

export interface StavkaMojeKorpice {
  productId: string;
  naziv: string;
  kolicina: number;
  jedinica: string;
  jedinicnaCijena: number;
  dodaoKupac: boolean;
}

export interface PonudaArtikla {
  productId: string;
  naziv: string;
  jedinica: string;
  cijena: number;
  imageUrl?: string;
  izOveKorpice: boolean;
}

export interface MojaKorpicaDetalji {
  korpica: MojaKorpica;
  stavke: StavkaMojeKorpice[];
  ponuda: PonudaArtikla[];
}
```

**Pazi:** `imageUrl` je `?:` a ne `| null`. Backend izostavlja null polja sa žice (`DefaultIgnoreCondition = WhenWritingNull`), pa `null` nikad ne stiže.

- [ ] **Step 2: Dodaj metode u `accountApi`**

U `frontend/src/lib/api/endpoints.ts`, u objekat `accountApi`, poslije `azurirajProfil`:

```ts
  mojeKorpice: () =>
    apiFetch<MojaKorpica[]>("/account/korpice").then((r) => r.data),

  mojaKorpica: (orderItemId: string) =>
    apiFetch<MojaKorpicaDetalji>(`/account/korpice/${orderItemId}`).then((r) => r.data),

  izmijeniKorpicu: (orderItemId: string, productIds: string[]) =>
    apiFetch<MojaKorpicaDetalji>(`/account/korpice/${orderItemId}`, {
      method: "PUT",
      body: { productIds },
    }).then((r) => r.data),
```

Dodaj `MojaKorpica` i `MojaKorpicaDetalji` u postojeći `import type { … } from "@/types/domain";` na vrhu fajla.

- [ ] **Step 3: Dodaj ključeve upita**

U `frontend/src/lib/api/hooks.ts`, u objekat `queryKeys`:

```ts
  mojeKorpice: () => ["account", "korpice"] as const,
  mojaKorpica: (id: string) => ["account", "korpica", id] as const,
```

- [ ] **Step 4: Provjeri**

```bash
cd frontend
npx tsc --noEmit
npm run lint
```

Oba moraju biti čista. **Ne pokreći `npm run build`** — dev server radi na :3000 i dijeli `.next`.

- [ ] **Step 5: Commit**

```bash
git add frontend/
git commit -m "feat(nalog): tipovi i API sloj za moju korpicu"
```

---

### Task 10: Frontend — ekran editora

**Files:**
- Create: `frontend/src/components/nalog/korpica-editor.tsx`
- Create: `frontend/src/app/(shop)/moj-nalog/korpica/page.tsx`
- Modify: `frontend/src/components/nalog/nalog-nav.tsx`
- Modify: `frontend/src/app/(shop)/moj-nalog/page.tsx`

**Interfaces:**
- Consumes: `accountApi.mojeKorpice`, `accountApi.mojaKorpica`, `accountApi.izmijeniKorpicu`, `queryKeys.mojeKorpice`, `queryKeys.mojaKorpica`
- Produces: ruta `/moj-nalog/korpica`

- [ ] **Step 1: Napravi komponentu editora**

`frontend/src/components/nalog/korpica-editor.tsx` — jedna kartica, jedna korpica. Obavezno:

- spisak stavki sa dugmetom za izbacivanje po stavci; stavka sa `dodaoKupac === true` nosi tihu oznaku „tvoj izbor"
- traka budžeta: `trenutnaVrijednost / ciljnaVrijednost` sa procentom, obojena zeleno u opsegu 95–100% a crveno izvan njega
- brojač `brojArtikala / minimalanBrojArtikala`
- odbrojavanje do `rokIzmjene` — koristi postojeću komponentu `components/korpice/rok-traka.tsx` ako joj potpis odgovara (pročitaj je prvo); ako ne, prikaži datum i vrijeme preko `formatDateTime` iz `@/lib/utils`
- birač za dodavanje: `ponuda` je već sortirana tako da su artikli iz kategorija te korpice prvi; grupiši ih vizuelno sa natpisom „Iz ove korpice" i „Ostalo iz ponude"
- dugme „Sačuvaj" onemogućeno dok lokalni izbor nije u opsegu, sa tekstom ispod koji kaže šta nedostaje: `„još 1.20 KM do 95%"` ili `„fali 1 artikal"`
- dugme „Vrati na početno" koje vraća izbor na ono što je stiglo sa servera

Stanje drži lokalno kao `string[]` (spisak `productId`), inicijalizovan iz `detalji.stavke.map(s => s.productId)`, sa `useEffect` koji ga resinhronizuje kad se `detalji` promijene. Vrijednost računaj iz `ponuda` + `stavke` (cijena artikla × količina koju server javlja; za artikal koji nije u `stavke`, količina je 1 a cijena iz `ponuda`).

**Serverska provjera je mjerodavna.** Klijentska postoji samo da kupac ne šalje zahtjev koji unaprijed pada. Grešku sa servera prikaži po kući: `if (err instanceof ApiRequestError) { setErrors(err.fieldErrors); if (Object.keys(err.fieldErrors).length === 0) setError(err.message); }` — vidi `frontend/src/app/(shop)/register/page.tsx:70-74`. Kodovi `budzet_premali`, `budzet_preveliki`, `premalo_artikala`, `rok_istekao`, `korpica_se_pakuje`, `artikal_nije_podoban` stižu **bez** `fieldErrors`, pa se moraju vidjeti kroz `err.message`.

Primitivi (pročitaj prije upotrebe, potpisi su provjereni):
- `components/ui/field.tsx` — `Field({ label, htmlFor?, required?, error?: string, children })`, `Input`, `Select`
- `components/ui/toast.tsx` — `useToast()` → `{ show(message, variant?) }`
- `components/ui/empty-state.tsx` — `EmptyState({ title, description?, icon?, action? })`, **bez djece**
- `components/ui/spinner.tsx:8` — `LoadingBlock({ label?: string })`
- `components/ui/button.tsx` — `Button`, `buttonVariants`

**Ne koristi `window.confirm`** — nativni dijalog zamrzava automatizaciju koja provjerava ovaj ekran.

- [ ] **Step 2: Napravi stranicu**

`frontend/src/app/(shop)/moj-nalog/korpica/page.tsx`:

- `"use client"`
- **bez sopstvene straže** — `app/(shop)/moj-nalog/layout.tsx` je već drži i ne renderuje djecu bez korisnika
- `useQuery` nad `accountApi.mojeKorpice()` sa `queryKeys.mojeKorpice()`
- `LoadingBlock` dok se učitava; grana na `isError` sa dugmetom „Pokušaj ponovo" (`refetch`) — bez nje pad učitavanja izgleda kao prazno stanje
- `EmptyState` kad nema nijedne: naslov „Nemaš korpicu za izmjenu", opis „Kad naručiš korpicu, ovdje mijenjaš šta će u njoj biti — sve do roka za tu sedmicu.", `action` je dugme koje vodi na `/korpice`
- za svaku korpicu po jedan `<KorpicaEditor orderItemId={…} />`

Tokeni shopa: `rounded-card`, `border-line`, `bg-surface`, `text-ink`, `text-muted`, `bg-sand`, `brand-*`, `font-display` za naslove. Uskladi sa `app/(shop)/moj-nalog/adrese/page.tsx` — spoljni omotač je goli `<div className="space-y-6">`, bez `mx-auto max-w-*`, jer layout već daje okvir.

- [ ] **Step 3: Otključaj navigaciju**

U `frontend/src/components/nalog/nalog-nav.tsx`, u nizu stavki, ukloni `uskoro: true` sa stavke `/moj-nalog/korpica`. Ostavi `/moj-nalog/pretplate` zaključanu — pretplate su P3.

U `frontend/src/app/(shop)/moj-nalog/page.tsx`, premjesti pločicu „Izmjena sadržaja korpice" iz niza zaključanih u niz živih, sa `href: "/moj-nalog/korpica"` i opisom „Podesi šta ide u tvoju korpicu prije dostave."

- [ ] **Step 4: Provjeri**

```bash
cd frontend
npx tsc --noEmit
npm run lint
```

Oba čista. **Ne pokreći `npm run build`.**

- [ ] **Step 5: Commit**

```bash
git add frontend/
git commit -m "feat(nalog): ekran Moja korpica — izmjena sadrzaja u okviru budzeta"
```

---

### Task 11: Admin — broj narudžbi uz pakersku listu

**Files:**
- Modify: `frontend/src/app/admin/korpice/page.tsx`
- Modify: `frontend/src/types/domain.ts`
- Modify: `frontend/src/lib/api/endpoints.ts`

- [ ] **Step 1: Uskladi tip i API sa novim oblikom odgovora**

Endpoint pakerske liste sada vraća objekat, ne niz. U `frontend/src/types/domain.ts`:

```ts
export interface PakerskaLista {
  brojNarudzbi: number;
  stavke: StavkaPakerske[];
}
```

(`StavkaPakerske` već postoji — ako se zove drukčije, zadrži postojeće ime i samo ga referenciraj.)

U `frontend/src/lib/api/endpoints.ts`, metoda koja dohvata pakersku listu mora vraćati `PakerskaLista` umjesto `StavkaPakerske[]`. Nađi je sa `grep -n "paker" frontend/src/lib/api/endpoints.ts`.

- [ ] **Step 2: Prikaži broj narudžbi**

U `frontend/src/app/admin/korpice/page.tsx`, iznad tabele pakerske liste dodaj red:

```tsx
<p className="text-sm text-muted">
  {lista.brojNarudzbi === 0
    ? "Za ovu sedmicu još nema nijedne narudžbe."
    : `${lista.brojNarudzbi} ${narudzbeRijec(lista.brojNarudzbi)} za ovu sedmicu.`}
</p>
```

`narudzbeRijec` već postoji u `frontend/src/lib/orders.ts` (koristi ga `admin-nav.tsx`). Prazno stanje tabele mora reći isto: „Nema narudžbi za ovu sedmicu, pa nema ni šta da se pakuje." — **ne** „nema podataka", jer operater koji pamti staro ponašanje mora razumjeti da je nula narudžbi, a ne kvar.

- [ ] **Step 3: Provjeri**

```bash
cd frontend
npx tsc --noEmit
npm run lint
```

- [ ] **Step 4: Commit**

```bash
git add frontend/
git commit -m "feat(admin): pakerska lista pokazuje broj narudzbi za sedmicu"
```

---

### Task 12: Dokumentacija i završna provjera

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Dopuni `CLAUDE.md`**

U sekciju „Moj nalog (P2a)" preimenuj naslov u „Moj nalog i korpice kupca (P2a, P2b)" i dodaj:

- **`Order.Godina`/`Order.Sedmica` su ISO sedmica DATUMA ISPORUKE,** ne datuma narudžbe, i računaju se sa `SedmicaHelper.SedmicaZaNarudzbu` — prva sedmica čiji rok nije prošao. Narudžbama otprije P2b migracija je upisala sedmicu iz `CreatedAt`, što je približno.
- **`StavkaKorpiceNarudzbe` nije `StavkaSadrzaja`.** Prva pripada jednoj narudžbi i kupac je mijenja; druga je sedmični šablon i dijele je svi. Sadržaj se pri naručivanju **snima**, ne referencira.
- **Pakerska lista od P2b sabira narudžbe, ne šablon.** Prazna lista znači nula narudžbi. Ranije je u toj situaciji pokazivala sadržaj jedne kutije, pa je nabavka po njoj bila za jednu korpicu.
- **Korpica bez objavljenog sadržaja za svoju sedmicu se ne može naručiti** (`korpica_bez_sadrzaja`). To je jedina tačka koja sprečava prodaju koja se ne može isporučiti — `/korpice` izlog i dalje govori o tekućoj sedmici i ne zna za ovo pravilo.
- **`KorpicaPodobnost` je jedini pojam „smije u korpicu".** Generator ga sužava na kategorije te korpice; kupčev editor ne — po odluci iz P2b kupac bira iz cijele sezonske ponude.
- **`PUT /account/korpice/{orderItemId}` prima samo `productIds`.** Količinu i cijenu određuje server: artikal iz šablona te sedmice zadržava šablonsku količinu, sve ostalo dobija 1 u svojoj jedinici.

- [ ] **Step 2: Puna provjera**

```bash
cd backend && dotnet test
```

Expected: PASS, sve zeleno.

Zatim **ugasi dev server na :3000**, pa iz `frontend/`:

```bash
rm -rf .next
npx tsc --noEmit && npm run lint && npm run build
```

Sva tri čista. U tabeli ruta potvrdi da se pojavljuje `/moj-nalog/korpica`.

Vrati dev server poslije builda (`rm -rf .next && npm run dev`) — build i `next dev` dijele `.next` i build ostavlja stanje od kojeg dev server vraća 500 na svaku rutu.

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: P2b u CLAUDE.md — sedmica narudzbe, snapshot korpice, pakerska lista"
```

---

## Šta P2b NE pokriva

Provjereno prema specu — sve ispod je namjerno izvan:

| Izostavljeno | Gdje ide |
|---|---|
| Pretplate, pauziranje, promjena paketa | P3 |
| Izmjena količine postojeće stavke | izričita odluka korisnika protiv |
| Pakerska lista za obične stavke narudžbe | zaseban posao |
| `PorcijaUKorpici` po artiklu | zaseban posao, vidi R4 u specu |
| Birač adrese na mapi | ostatak iz P2a |
| Vremenska zona na `DeliverySlot` | zaseban posao, vidi R5 |
| Usklađivanje `/korpice` izloga sa sedmicom naručivanja | zaseban posao, vidi R6 |
