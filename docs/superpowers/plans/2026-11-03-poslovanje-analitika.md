# Poslovanje — analitika po mjesecima: plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin stranica „Poslovanje" sa mjesečnom analitikom (prihod i zarada, prodaja, kupci, pretplate, dostava, gazdinstva) i godišnjim pregledom, uz snimak nabavne cijene na svakoj novoj stavci narudžbe.

**Architecture:** Nabavna cijena se snima centralno u `AppDbContext.SaveChangesAsync` za svaku novu `OrderItem` i `StavkaKorpiceNarudzbe`. Analitika je čist C# račun (`PoslovanjeRacun`) nad narudžbama učitanim za sedmice čija isporuka pada u traženi mjesec (`PoslovanjeKalendar`). Dva MediatR upita i dva admin endpointa ga izlažu. Frontend ima dvije stranice (mjesec, godina), SVG grafikon bez nove biblioteke, i PDF preko pdfmake.

**Tech Stack:** .NET 9, EF Core 9 (Postgres, SQLite u testovima), MediatR, xUnit; Next.js 15, React 19, TanStack Query 5, Tailwind 4, pdfmake 0.3, `node --test`.

**Spec:** `docs/superpowers/specs/2026-11-03-poslovanje-analitika-design.md`

## Global Constraints

- Radi se u worktree-u `C:\Users\soviljn\Desktop\epiljara-backend-wt`, grana `poslovanje` (iz `nabavka-potvrdjene`). Ne mergati u `main` i ne pushovati bez izričitog „spoji i pushaj".
- Mjesec narudžbe = mjesec datuma isporuke njene sedmice (`ISOWeek.ToDateTime(Godina, Sedmica, danIsporuke)`), nikad `CreatedAt`.
- Prihod = samo `OrderStatus.Delivered`. Otkazane (`Cancelled`) i neuspjele (`FailedDelivery`) samo u odjeljku Dostava.
- Neto = `Subtotal + DeliveryFee − Popust − PoeniPopust`. Marža = (promet robe − nabavno) / promet robe; dostava i popusti ne ulaze u maržu.
- Nabavna jedinične cijena = snimak na stavci; bez snimka današnja `Product.NabavnaCijena` i mjesec je `Procjena`; bez obje stavka je „bez nabavne cijene" i ne ulazi u trošak.
- Kupac = `CustomerId`; gost = email (trim, mala slova), pa telefon (samo cifre), pa id narudžbe.
- Nabavna cijena nikad u javnim (kupčevim) DTO-ima.
- Ništa zakucano od poslovnih podataka: kategorije, zone, proizvođači dolaze iz baze.
- Tekst u UI-ju: srpski, latinica, ijekavica.
- Bez nove npm ili NuGet zavisnosti.
- Frontend mora proći `npm run build` (tsc i lint ne hvataju sve).
- API i web lokalno rade u prozorima „EPiljara API" i „EPiljara web"; prije `dotnet ef` i prije `npm run build` ugasi odgovarajući prozor, poslije ga ponovo pokreni (`Start-Process pwsh -WorkingDirectory <dir> -ArgumentList '-NoExit','-Command','$host.UI.RawUI.WindowTitle="EPiljara API"; dotnet run --project src/EPiljara.API'`).

## Review Focus

1. **Mjesec bez ijedne narudžbe** (npr. avgust 2026, prije početka rada): stranica prikazuje nule i „—", nikad dijeljenje nulom ni 500. → test u Task 3.
2. **Sedmica na prelazu mjeseca** (sedmica 40/2026: ponedjeljak 28.9, isporuka u četvrtak 1.10): narudžba ide u oktobar. → test u Task 2 i Task 3.
3. **Januar**: prethodni mjesec je decembar prethodne godine, a sedmica 53/1 se ne gubi. → test u Task 2.
4. **Artikal obrisan iz kataloga** (soft delete) poslije narudžbe: red se i dalje broji u prodaji i kod svog gazdinstva (katalog se čita sa `IgnoreQueryFilters`), bez izuzetka. → test u Task 5.
5. **Gost bez emaila** i gost sa emailom u drugačijim velikim slovima: isti gost se ne broji dvaput, različiti gosti bez kontakta se ne spajaju. → test u Task 4.

---

## Struktura fajlova

| Fajl | Odgovornost |
|---|---|
| `backend/src/EPiljara.Domain/Entities/OrderItem.cs` | + `NabavnaCijena` |
| `backend/src/EPiljara.Domain/Entities/StavkaKorpiceNarudzbe.cs` | + `NabavnaCijena` |
| `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs` | preciznost kolona, `SaveChangesAsync` snima nabavnu |
| `backend/src/EPiljara.Infrastructure/Persistence/Migrations/*_NabavnaCijenaNaNarudzbi.cs` | migracija |
| `backend/src/EPiljara.Application/Features/Korpice/MojaKorpica.cs`, `AdminKorpicaNarudzbe.cs` | zadržan red nosi stari snimak |
| `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeKalendar.cs` | sedmica ↔ mjesec |
| `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeRacun.cs` | sav račun (čist, bez baze) |
| `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjePodaci.cs` | učitavanje iz baze |
| `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeUpiti.cs` | dva MediatR upita |
| `backend/src/EPiljara.Application/DTOs/PoslovanjeDtos.cs` | DTO-i |
| `backend/src/EPiljara.API/Controllers/AdminController.cs` | dva endpointa |
| `backend/tests/EPiljara.Application.Tests/NabavnaSnimakTests.cs` | Task 1 |
| `backend/tests/EPiljara.Application.Tests/PoslovanjeTests.cs` | Task 2–6 (+ helper `PoslovanjePodaciTest`) |
| `frontend/src/lib/api/contracts.ts`, `endpoints.ts` | tipovi i pozivi |
| `frontend/src/lib/admin/poslovanje.ts` (+ `.test.ts`) | promjena %, mjeseci, skala grafikona |
| `frontend/src/components/admin/poslovanje/*.tsx` | kartice, odjeljci, grafikon |
| `frontend/src/app/admin/poslovanje/page.tsx`, `godina/page.tsx` | stranice |
| `frontend/src/lib/admin/poslovanje-pdf.ts` | PDF |
| `frontend/src/components/admin/admin-nav.tsx` | stavka menija |

---

### Task 1: Snimak nabavne cijene na stavkama narudžbe

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/OrderItem.cs`
- Modify: `backend/src/EPiljara.Domain/Entities/StavkaKorpiceNarudzbe.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs` (konfiguracija ~174 i ~476, nova metoda)
- Modify: `backend/src/EPiljara.Application/Features/Korpice/MojaKorpica.cs:402-414`
- Modify: `backend/src/EPiljara.Application/Features/Korpice/AdminKorpicaNarudzbe.cs:152-161`
- Create: migracija `NabavnaCijenaNaNarudzbi`
- Test: `backend/tests/EPiljara.Application.Tests/NabavnaSnimakTests.cs`

**Interfaces:**
- Produces: `OrderItem.NabavnaCijena : decimal?`, `StavkaKorpiceNarudzbe.NabavnaCijena : decimal?`; svaki novi red dobija `Product.NabavnaCijena` u trenutku upisa ako mu je vrijednost `null`.

- [ ] **Step 1: Write the failing test**

```csharp
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

/// <summary>
/// Narudžba pamti nabavnu cijenu u trenutku nastanka, da zarada za prošle mjesece ne zavisi od
/// današnjih cijena. Snima se centralno (AppDbContext.SaveChangesAsync), pa je ne zaobilazi
/// nijedna putanja: kupac, gost, pretplata, admin, izmjena korpice.
/// </summary>
public class NabavnaSnimakTests
{
    private static async Task PostaviNabavnuAsync(Scenario t, Guid id, decimal? cijena)
    {
        var p = await t.Db.Products.FirstAsync(x => x.Id == id);
        p.NabavnaCijena = cijena;
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Nova_narudzba_snima_nabavnu_i_za_spajz_i_za_sadrzaj_korpice()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await PostaviNabavnuAsync(t, t.KrompirId, 1.20m);
        await t.PosaljiKorpicuAsync(kolicina: 1m);
        await t.PosaljiObicanArtikalAsync();
        t.Db.ChangeTracker.Clear();

        var spajz = await t.Db.OrderItems.SingleAsync(i => i.ProductId == t.KrompirId);
        Assert.Equal(1.20m, spajz.NabavnaCijena);
        var uKorpici = await t.Db.StavkeKorpiceNarudzbe.SingleAsync(s => s.ProductId == t.KrompirId);
        Assert.Equal(1.20m, uKorpici.NabavnaCijena);
        var korpica = await t.Db.OrderItems.SingleAsync(i => i.ProductId == t.KorpicaId);
        Assert.Null(korpica.NabavnaCijena); // korpica sama nema nabavnu — nabavlja se sadržaj
    }

    [Fact]
    public async Task Kasnija_promjena_nabavne_ne_mijenja_snimak()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await PostaviNabavnuAsync(t, t.KrompirId, 1.20m);
        await t.PosaljiObicanArtikalAsync();
        await PostaviNabavnuAsync(t, t.KrompirId, 9m);

        var spajz = await t.Db.OrderItems.SingleAsync(i => i.ProductId == t.KrompirId);
        Assert.Equal(1.20m, spajz.NabavnaCijena);
    }

    [Fact]
    public async Task Vec_postavljen_snimak_se_ne_prepisuje()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await PostaviNabavnuAsync(t, t.KrompirId, 1.20m);
        var o = await t.PosaljiObicanArtikalAsync();
        t.Db.ChangeTracker.Clear();

        t.Db.OrderItems.Add(new OrderItem
        {
            OrderId = o.Id, ProductId = t.KrompirId, ProductName = "Krompir", Unit = Domain.Enums.Unit.Kg,
            Quantity = 1m, UnitPrice = 2m, NabavnaCijena = 0.80m,
        });
        await t.Db.SaveChangesAsync();
        t.Db.ChangeTracker.Clear();

        Assert.Contains(await t.Db.OrderItems.Where(i => i.ProductId == t.KrompirId).ToListAsync(),
            i => i.NabavnaCijena == 0.80m);
    }

    [Fact]
    public async Task Artikal_bez_nabavne_ostavlja_snimak_prazan()
    {
        using var t = await Scenario.NapraviAsync(objaviSadrzaj: true);
        await PostaviNabavnuAsync(t, t.KrompirId, null);
        await t.PosaljiObicanArtikalAsync();
        t.Db.ChangeTracker.Clear();

        Assert.Null((await t.Db.OrderItems.SingleAsync(i => i.ProductId == t.KrompirId)).NabavnaCijena);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests --filter "FullyQualifiedName~NabavnaSnimakTests"`
Expected: build FAIL — `'OrderItem' does not contain a definition for 'NabavnaCijena'`.

- [ ] **Step 3: Add the fields**

U `OrderItem.cs`, ispod `UnitPrice`:

```csharp
    /// <summary>
    /// Nabavna cijena po jedinici u trenutku narudžbe — snimak, da zarada za prošle mjesece ne
    /// zavisi od današnjih cijena. Null: narudžba starija od snimanja, ili artikal bez nabavne.
    /// Upisuje je AppDbContext pri upisu reda. Nikad ne ide u kupčev DTO.
    /// </summary>
    public decimal? NabavnaCijena { get; set; }
```

U `StavkaKorpiceNarudzbe.cs`, ispod `JedinicnaCijena`, isto polje sa istim komentarom.

- [ ] **Step 4: Configure precision and snapshot in AppDbContext**

U `b.Entity<StavkaKorpiceNarudzbe>` dodaj `e.Property(x => x.NabavnaCijena).HasPrecision(10, 2);`, a u `b.Entity<OrderItem>` dodaj `e.Property(x => x.NabavnaCijena).HasPrecision(10, 2);`.

U klasu `AppDbContext` dodaj:

```csharp
    /// <summary>
    /// Svaki novi red narudžbe (OrderItem, StavkaKorpiceNarudzbe) bez nabavne cijene dobija
    /// trenutnu nabavnu cijenu artikla. Jedno mjesto umjesto pet putanja koje prave redove —
    /// nova putanja ga ne može zaobići. Postavljena vrijednost se ne dira.
    /// </summary>
    // Override bool varijante: SaveChangesAsync(CancellationToken) iz IAppDbContext poziva nju.
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await SnimiNabavneCijeneAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private async Task SnimiNabavneCijeneAsync(CancellationToken ct)
    {
        var redovi = ChangeTracker.Entries<OrderItem>()
            .Where(e => e.State == EntityState.Added && e.Entity.NabavnaCijena is null)
            .Select(e => (Id: e.Entity.ProductId, Postavi: (Action<decimal?>)(c => e.Entity.NabavnaCijena = c)))
            .Concat(ChangeTracker.Entries<StavkaKorpiceNarudzbe>()
                .Where(e => e.State == EntityState.Added && e.Entity.NabavnaCijena is null)
                .Select(e => (Id: e.Entity.ProductId, Postavi: (Action<decimal?>)(c => e.Entity.NabavnaCijena = c))))
            .ToList();
        if (redovi.Count == 0) return;

        var idjevi = redovi.Select(r => r.Id).Distinct().ToList();
        var nabavne = await Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => idjevi.Contains(p.Id) && p.NabavnaCijena != null)
            .ToDictionaryAsync(p => p.Id, p => p.NabavnaCijena, ct);
        foreach (var (id, postavi) in redovi)
            if (nabavne.TryGetValue(id, out var c)) postavi(c);
    }
```

- [ ] **Step 5: Kept rows carry their old snapshot**

U `MojaKorpica.cs` (red ~402, grana `zadrzani.TryGetValue(pid, out var stara)`) u inicijalizator dodaj `NabavnaCijena = stara.NabavnaCijena,` odmah ispod `JedinicnaCijena = stara.JedinicnaCijena,`.

U `AdminKorpicaNarudzbe.cs` (red ~159) ispod `JedinicnaCijena = cijena,` dodaj `NabavnaCijena = stara?.NabavnaCijena,` (za nov artikal je `null`, pa ga SaveChanges popuni trenutnom).

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests --filter "FullyQualifiedName~NabavnaSnimakTests"`
Expected: PASS (4).

- [ ] **Step 7: Migration**

Ugasi prozor „EPiljara API" (zaključava `bin`). Pa:

Run: `cd backend && dotnet tool run dotnet-ef migrations add NabavnaCijenaNaNarudzbi --project src/EPiljara.Infrastructure --startup-project src/EPiljara.API`
Expected: novi fajl u `src/EPiljara.Infrastructure/Persistence/Migrations/` sa dva `AddColumn<decimal>` (`OrderItems.NabavnaCijena`, `StavkeKorpiceNarudzbe.NabavnaCijena`, `precision: 10, scale: 2, nullable: true`) i ničim drugim. Ako ima još izmjena — stani i javi.

- [ ] **Step 8: Full backend suite**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests`
Expected: sve prolazi.

- [ ] **Step 9: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(narudzbe): stavka narudžbe pamti nabavnu cijenu u trenutku nastanka"
```

---

### Task 2: Kalendar — sedmica u mjesec isporuke

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeKalendar.cs`
- Test: `backend/tests/EPiljara.Application.Tests/PoslovanjeTests.cs` (klasa `PoslovanjeKalendarTests`)

**Interfaces:**
- Produces:
  - `PoslovanjeKalendar.MjesecIsporuke(int godina, int sedmica, DayOfWeek dan) : (int Godina, int Mjesec)`
  - `PoslovanjeKalendar.SedmiceMjeseca(int godina, int mjesec, DayOfWeek dan) : List<(int Godina, int Sedmica)>`
  - `PoslovanjeKalendar.Prethodni(int godina, int mjesec) : (int Godina, int Mjesec)`
  - `PoslovanjeKalendar.PocetakUtc(int godina, int mjesec) : DateTime` (lokalna ponoć prvog dana, u UTC)
  - `PoslovanjeKalendar.Indeks(int godina, int mjesec) : int` (= godina*12 + mjesec − 1, za poređenje)

- [ ] **Step 1: Write the failing test**

```csharp
using EPiljara.Application.Features.Admin.Poslovanje;

namespace EPiljara.Application.Tests;

public class PoslovanjeKalendarTests
{
    [Fact]
    public void Sedmica_na_prelazu_ide_u_mjesec_isporuke()
    {
        // Sedmica 40/2026 počinje u ponedjeljak 28.9, a isporuka je u četvrtak 1.10.
        Assert.Equal((2026, 10), PoslovanjeKalendar.MjesecIsporuke(2026, 40, DayOfWeek.Thursday));
        Assert.Equal((2026, 9), PoslovanjeKalendar.MjesecIsporuke(2026, 40, DayOfWeek.Monday));
    }

    [Fact]
    public void Oktobar_2026_ima_pet_cetvrtaka()
    {
        Assert.Equal(
            [(2026, 40), (2026, 41), (2026, 42), (2026, 43), (2026, 44)],
            PoslovanjeKalendar.SedmiceMjeseca(2026, 10, DayOfWeek.Thursday));
    }

    [Fact]
    public void Januar_uzima_sedmicu_prethodne_iso_godine_i_prethodni_je_decembar()
    {
        // 1.1.2027 je petak i pripada ISO sedmici 53/2026.
        Assert.Equal((2026, 53), PoslovanjeKalendar.SedmiceMjeseca(2027, 1, DayOfWeek.Friday)[0]);
        Assert.Equal((2026, 12), PoslovanjeKalendar.Prethodni(2027, 1));
        Assert.Equal((2026, 10), PoslovanjeKalendar.Prethodni(2026, 11));
    }

    [Fact]
    public void Pocetak_mjeseca_je_lokalna_ponoc()
    {
        // 1.10.2026 00:00 u Sarajevu (ljetno vrijeme, UTC+2) = 30.9 22:00 UTC.
        Assert.Equal(new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc), PoslovanjeKalendar.PocetakUtc(2026, 10));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests --filter "FullyQualifiedName~PoslovanjeKalendarTests"`
Expected: build FAIL — namespace `EPiljara.Application.Features.Admin.Poslovanje` ne postoji.

- [ ] **Step 3: Implement**

```csharp
using System.Globalization;
using EPiljara.Application.Features.Korpice;

namespace EPiljara.Application.Features.Admin.Poslovanje;

/// <summary>
/// Narudžba pripada mjesecu u kojem je ISPORUČENA: sedmica se prevodi u datum dana isporuke.
/// Sedmica 40/2026 (pon 28.9) sa isporukom četvrtkom je oktobar, ne septembar.
/// </summary>
public static class PoslovanjeKalendar
{
    public static (int Godina, int Mjesec) MjesecIsporuke(int godina, int sedmica, DayOfWeek dan)
    {
        var d = ISOWeek.ToDateTime(godina, sedmica, dan);
        return (d.Year, d.Month);
    }

    /// <summary>ISO sedmice čiji dan isporuke pada u dati mjesec, redom.</summary>
    public static List<(int Godina, int Sedmica)> SedmiceMjeseca(int godina, int mjesec, DayOfWeek dan)
    {
        var d = new DateTime(godina, mjesec, 1);
        while (d.DayOfWeek != dan) d = d.AddDays(1);
        var sedmice = new List<(int, int)>();
        for (; d.Month == mjesec; d = d.AddDays(7))
            sedmice.Add((ISOWeek.GetYear(d), ISOWeek.GetWeekOfYear(d)));
        return sedmice;
    }

    public static (int Godina, int Mjesec) Prethodni(int godina, int mjesec) =>
        mjesec == 1 ? (godina - 1, 12) : (godina, mjesec - 1);

    public static int Indeks(int godina, int mjesec) => godina * 12 + mjesec - 1;

    /// <summary>Ponoć prvog dana mjeseca po lokalnom (Sarajevo) vremenu, izražena u UTC.</summary>
    public static DateTime PocetakUtc(int godina, int mjesec) =>
        TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(godina, mjesec, 1, 0, 0, 0, DateTimeKind.Unspecified), SedmicaHelper.Zona);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests --filter "FullyQualifiedName~PoslovanjeKalendarTests"`
Expected: PASS (4).

- [ ] **Step 5: Commit**

```bash
git add backend/src/EPiljara.Application/Features/Admin/Poslovanje backend/tests/EPiljara.Application.Tests/PoslovanjeTests.cs
git commit -m "feat(poslovanje): kalendar — sedmica ide u mjesec isporuke"
```

---

### Task 3: Prihod i zarada + mjesečni upit i endpoint

**Files:**
- Create: `backend/src/EPiljara.Application/DTOs/PoslovanjeDtos.cs`
- Create: `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeRacun.cs`
- Create: `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjePodaci.cs`
- Create: `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeUpiti.cs`
- Modify: `backend/src/EPiljara.API/Controllers/AdminController.cs` (poslije `Dashboard`)
- Test: `backend/tests/EPiljara.Application.Tests/PoslovanjeTests.cs` (klase `PoslovanjePodaciTest`, `PoslovanjePrihodTests`)

**Interfaces:**
- Consumes: `PoslovanjeKalendar` (Task 2), `OrderItem.NabavnaCijena` / `StavkaKorpiceNarudzbe.NabavnaCijena` (Task 1).
- Produces:
  - DTO-i iz Step 3 (svi, i za Task 4–6, da se fajl piše jednom).
  - `new PoslovanjeRacun(IReadOnlyDictionary<Guid, Product> katalog)`; `PrihodDto Prihod(IReadOnlyCollection<Order> isporucene)`; `static decimal Neto(Order o)`; `static IEnumerable<Order> Isporucene(IEnumerable<Order>)`; `internal IEnumerable<Linija> Linije(Order o)`.
  - `PoslovanjePodaci.NarudzbeAsync(IAppDbContext, IReadOnlyCollection<(int,int)>, CancellationToken) : Task<List<Order>>`, `PoslovanjePodaci.KatalogAsync(IAppDbContext, CancellationToken) : Task<Dictionary<Guid, Product>>`.
  - `GetPoslovanjeQuery(int? Godina, int? Mjesec) : IRequest<PoslovanjeMjesecaDto>`, handler `GetPoslovanjeHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)`.
  - `GET /admin/poslovanje?godina=&mjesec=`.

- [ ] **Step 1: Write the test helper and failing tests**

Dodaj u `PoslovanjeTests.cs` (iznad kalendar testova, usinge spoji na vrh fajla):

```csharp
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Admin.Poslovanje;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Unit = EPiljara.Domain.Enums.Unit;

namespace EPiljara.Application.Tests;

/// <summary>
/// Podaci za testove analitike, upisani direktno (bez checkout-a), sa fiksnim sedmicama 2026:
/// isporuka četvrtkom, oktobar = sedmice 40–44, septembar = 36–39.
/// Krompir: 2 KM/kg, nabavna 1 (Marko). Jaja: 0,50 KM/kom, nabavna 0,30 (Ana, kategorija Jaja).
/// Korpica povrća (mala): 10 KM, sadržaj 2 kg krompira.
/// </summary>
internal sealed class PoslovanjePodaciTest : IDisposable
{
    public TestDb T { get; } = new();
    public DeliveryZone Centar { get; private set; } = null!;
    public DeliveryZone Laus { get; private set; } = null!;
    public DeliverySlot Slot { get; private set; } = null!;
    public Product Krompir { get; private set; } = null!;
    public Product Jaja { get; private set; } = null!;
    public Product Korpica { get; private set; } = null!;
    private int _brojac;

    public static async Task<PoslovanjePodaciTest> NapraviAsync()
    {
        var p = new PoslovanjePodaciTest();
        var db = p.T.Db;
        p.Centar = new DeliveryZone { Name = "Centar", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
        p.Laus = new DeliveryZone { Name = "Lauš", MinOrderAmount = 0m, DeliveryFee = 3m, IsActive = true };
        p.Slot = new DeliverySlot { DayOfWeek = (int)DayOfWeek.Thursday, TimeFrom = "16:00", TimeTo = "20:00", CutoffHours = 48 };
        var povrce = new Category { Name = "Povrće", Slug = "povrce" };
        var jaja = new Category { Name = "Jaja", Slug = "jaja" };
        var marko = new Producer { Name = "Marko", Village = "Manjača" };
        var ana = new Producer { Name = "Ana", Village = "Kola" };
        p.Krompir = new Product
        {
            Name = "Krompir", Slug = "krompir", Price = 2m, NabavnaCijena = 1m, Unit = Unit.Kg, Stock = 100,
            Category = povrce, Producer = marko,
        };
        p.Jaja = new Product
        {
            Name = "Jaja", Slug = "jaja", Price = 0.5m, NabavnaCijena = 0.3m, Unit = Unit.Kom, Stock = 100,
            Category = jaja, Producer = ana,
        };
        p.Korpica = new Product
        {
            Name = "Mala korpica povrća", Slug = "mala-korpica-povrca", Price = 10m, Unit = Unit.Kom,
            Vrsta = VrstaProizvoda.Korpica, TipKorpice = TipKorpice.Povrce, Velicina = VelicinaKorpice.Mala,
            Category = povrce, Producer = marko,
        };
        db.AddRange(p.Centar, p.Laus, p.Slot, povrce, jaja, marko, ana, p.Krompir, p.Jaja, p.Korpica);
        await db.SaveChangesAsync();
        return p;
    }

    /// <summary>
    /// Upisuje narudžbu za sedmicu 2026. <paramref name="bezSnimka"/> briše snimak nabavne
    /// poslije upisa — kao narudžba starija od snimanja.
    /// </summary>
    public async Task<Order> NarudzbaAsync(
        int sedmica, OrderStatus status, decimal krompirKg = 0, int jaja = 0, int korpica = 0,
        Guid? kupacId = null, string kupac = "Marko Marković", string? email = null, string? telefon = null,
        decimal dostava = 0, decimal popust = 0, decimal poeniPopust = 0, DeliveryZone? zona = null,
        Guid? pretplataId = null, bool bezSnimka = false, int godina = 2026)
    {
        var n = ++_brojac;
        var o = new Order
        {
            Code = $"EP-{n:0000}", PublicToken = Guid.NewGuid().ToString("N"),
            CustomerId = kupacId, CustomerName = kupac, CustomerEmail = email, CustomerPhone = telefon,
            Status = status, DeliveryAddress = "Testna 1", DeliverySlotId = Slot.Id,
            DeliveryZoneId = (zona ?? Centar).Id, Godina = godina, Sedmica = sedmica,
            DeliveryFee = dostava, Popust = popust, PoeniPopust = poeniPopust, PretplataId = pretplataId,
        };
        if (krompirKg > 0)
            o.Items.Add(new OrderItem { ProductId = Krompir.Id, ProductName = "Krompir", Unit = Unit.Kg, Quantity = krompirKg, UnitPrice = 2m });
        if (jaja > 0)
            o.Items.Add(new OrderItem { ProductId = Jaja.Id, ProductName = "Jaja", Unit = Unit.Kom, Quantity = jaja, UnitPrice = 0.5m });
        if (korpica > 0)
        {
            var k = new OrderItem { ProductId = Korpica.Id, ProductName = Korpica.Name, Unit = Unit.Kom, Quantity = korpica, UnitPrice = 10m };
            k.KorpicaStavke.Add(new StavkaKorpiceNarudzbe
            {
                ProductId = Krompir.Id, ProductName = "Krompir", Unit = Unit.Kg, Kolicina = 2m, JedinicnaCijena = 2m,
            });
            o.Items.Add(k);
        }
        o.Subtotal = o.Items.Sum(i => i.Quantity * i.UnitPrice);
        o.TotalAmount = o.Subtotal + dostava - popust - poeniPopust;
        T.Db.Orders.Add(o);
        await T.Db.SaveChangesAsync();

        if (bezSnimka)
        {
            foreach (var i in o.Items)
            {
                i.NabavnaCijena = null;
                foreach (var s in i.KorpicaStavke) s.NabavnaCijena = null;
            }
            await T.Db.SaveChangesAsync();
        }
        T.Db.ChangeTracker.Clear();
        return o;
    }

    public async Task<User> KupacAsync(string ime, Guid? preporucio = null)
    {
        var u = new User { Name = ime, Email = $"{ime.ToLowerInvariant()}@test.ba", PasswordHash = "x", PreporucioId = preporucio };
        T.Db.Users.Add(u);
        await T.Db.SaveChangesAsync();
        return u;
    }

    public Task<PoslovanjeMjesecaDto> MjesecAsync(int mjesec, int godina = 2026) =>
        new GetPoslovanjeHandler(T.Db, Options.Create(TestOpcije.Korpice()))
            .Handle(new GetPoslovanjeQuery(godina, mjesec), default);

    public void Dispose() => T.Dispose();
}

public class PoslovanjePrihodTests
{
    /// <summary>
    /// Oktobar: A (sedm. 41, isporučena) = 3 kg krompira + 1 korpica + 3 KM dostave;
    /// B (sedm. 40 — isporuka 1.10) = 10 jaja. Otkazana i neuspjela ne ulaze u prihod.
    /// Promet 16 + 5 = 21; nabavno 3 + 2 (korpica: 2 kg × 1) + 3 = 8; zarada 13; neto 24.
    /// </summary>
    [Fact]
    public async Task Prihod_broji_samo_isporucene_u_mjesecu_isporuke()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 3, korpica: 1, dostava: 3);
        await p.NarudzbaAsync(40, OrderStatus.Delivered, jaja: 10);
        await p.NarudzbaAsync(42, OrderStatus.Cancelled, krompirKg: 5);
        await p.NarudzbaAsync(43, OrderStatus.FailedDelivery, krompirKg: 1);
        await p.NarudzbaAsync(39, OrderStatus.Delivered, krompirKg: 1); // 24.9 — septembar

        var d = await p.MjesecAsync(10);

        Assert.Equal(21m, d.Prihod.PrometRobe);
        Assert.Equal(3m, d.Prihod.Dostava);
        Assert.Equal(24m, d.Prihod.Neto);
        Assert.Equal(8m, d.Prihod.Nabavno);
        Assert.Equal(13m, d.Prihod.Zarada);
        Assert.Equal(0.619m, d.Prihod.Marza);
        Assert.Equal(2, d.Prihod.BrojNarudzbi);
        Assert.Equal(12m, d.Prihod.ProsjecnaNarudzba);
        Assert.Equal(0.4762m, d.Prihod.UdioKorpica); // 10 od 21
        Assert.False(d.Prihod.Procjena);
        Assert.Equal(2m, d.Prethodni.PrometRobe);   // septembar: samo sedmica 39
    }

    [Fact]
    public async Task Popusti_umanjuju_neto_ali_ne_i_marzu()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 10, popust: 2, poeniPopust: 1);

        var d = await p.MjesecAsync(10);

        Assert.Equal(2m, d.Prihod.PromoPopust);
        Assert.Equal(1m, d.Prihod.PoeniPopust);
        Assert.Equal(17m, d.Prihod.Neto);
        Assert.Equal(0.5m, d.Prihod.Marza); // (20 − 10) / 20
    }

    [Fact]
    public async Task Bez_snimka_racuna_po_danasnjoj_nabavnoj_i_oznacava_procjenu()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 4, bezSnimka: true);

        var d = await p.MjesecAsync(10);

        Assert.True(d.Prihod.Procjena);
        Assert.Equal(4m, d.Prihod.Nabavno);
    }

    [Fact]
    public async Task Artikal_bez_ikakve_nabavne_se_broji_posebno()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var k = await p.T.Db.Products.FirstAsync(x => x.Id == p.Krompir.Id);
        k.NabavnaCijena = null;
        await p.T.Db.SaveChangesAsync();
        p.T.Db.ChangeTracker.Clear();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 4, jaja: 10);

        var d = await p.MjesecAsync(10);

        Assert.Equal(1, d.Prihod.BezNabavneCijene);
        Assert.Equal(3m, d.Prihod.Nabavno); // samo jaja
    }

    [Fact]
    public async Task Prazan_mjesec_daje_nule_bez_greske()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();

        var d = await p.MjesecAsync(8);

        Assert.Equal(0m, d.Prihod.Neto);
        Assert.Null(d.Prihod.Marza);
        Assert.Equal(0m, d.Prihod.ProsjecnaNarudzba);
        Assert.Equal(0, d.Kupci.Aktivni);
        Assert.Null(d.Kupci.Zadrzavanje);
        Assert.Null(d.Dostava.Uspjesnost);
        Assert.Empty(d.Gazdinstva);
    }

    [Fact]
    public async Task Neispravan_mjesec_je_greska_domena()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var ex = await Assert.ThrowsAsync<EPiljara.Domain.Common.DomainException>(() => p.MjesecAsync(13));
        Assert.Equal("mjesec_neispravan", ex.Code);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests --filter "FullyQualifiedName~PoslovanjePrihodTests"`
Expected: build FAIL — `GetPoslovanjeHandler` i `PoslovanjeMjesecaDto` ne postoje.

- [ ] **Step 3: DTOs (cijeli fajl, i za Task 4–6)**

`backend/src/EPiljara.Application/DTOs/PoslovanjeDtos.cs`:

```csharp
namespace EPiljara.Application.DTOs;

/// <summary>Analitika jednog mjeseca (po datumu isporuke). Samo admin.</summary>
public record PoslovanjeMjesecaDto
{
    public int Godina { get; init; }
    public int Mjesec { get; init; }
    public PrihodDto Prihod { get; init; } = new();
    /// <summary>Isti brojevi za prethodni mjesec — za ▲/▼ na karticama.</summary>
    public PrihodDto Prethodni { get; init; } = new();
    public ProdajaDto Prodaja { get; init; } = new();
    public KupciDto Kupci { get; init; } = new();
    public PretplateAnalitikaDto Pretplate { get; init; } = new();
    public DostavaAnalitikaDto Dostava { get; init; } = new();
    public IReadOnlyList<GazdinstvoDto> Gazdinstva { get; init; } = [];
}

public record PrihodDto
{
    public decimal PrometRobe { get; init; }
    public decimal Dostava { get; init; }
    public decimal PromoPopust { get; init; }
    public decimal PoeniPopust { get; init; }
    public decimal Neto { get; init; }
    public decimal Nabavno { get; init; }
    public decimal Zarada { get; init; }
    /// <summary>Zarada / promet robe (0–1). Null kad nema prometa ili nijedna stavka nema nabavnu.</summary>
    public decimal? Marza { get; init; }
    public int BrojNarudzbi { get; init; }
    public decimal ProsjecnaNarudzba { get; init; }
    public decimal UdioKorpica { get; init; }
    public decimal UdioSpajza { get; init; }
    /// <summary>Bar jedna stavka nema snimak nabavne pa je računata po današnjoj cijeni.</summary>
    public bool Procjena { get; init; }
    /// <summary>Stavke bez ikakve nabavne cijene — nisu u trošku.</summary>
    public int BezNabavneCijene { get; init; }
}

public record ProdajaDto
{
    public IReadOnlyList<KorpicaProdajeDto> Korpice { get; init; } = [];
    public IReadOnlyList<KategorijaProdajeDto> Spajz { get; init; } = [];
    public IReadOnlyList<ArtikalProdajeDto> TopArtikli { get; init; } = [];
    public IReadOnlyList<ArtikalProdajeDto> NajslabijiArtikli { get; init; } = [];
}

public record KorpicaProdajeDto
{
    public string Naziv { get; init; } = string.Empty;
    public string Tip { get; init; } = string.Empty;
    public string Velicina { get; init; } = string.Empty;
    public decimal Komada { get; init; }
    public decimal Prihod { get; init; }
    public decimal Nabavno { get; init; }
    public decimal? Marza { get; init; }
}

public record KategorijaProdajeDto
{
    public string Kategorija { get; init; } = string.Empty;
    public decimal Prihod { get; init; }
    public decimal Nabavno { get; init; }
    public decimal? Marza { get; init; }
}

/// <summary>Artikal sabran iz Špajza i sadržaja korpica; prihod sadržaja po cijeni iz korpice.</summary>
public record ArtikalProdajeDto
{
    public string Naziv { get; init; } = string.Empty;
    public string Jedinica { get; init; } = string.Empty;
    public decimal Kolicina { get; init; }
    public decimal Prihod { get; init; }
    public decimal Zarada { get; init; }
}

public record KupciDto
{
    public int Aktivni { get; init; }
    public int AktivniPrethodni { get; init; }
    public int Novi { get; init; }
    public int Povratni { get; init; }
    /// <summary>Dio kupaca aktivnih prošlog mjeseca koji su aktivni i ovog (0–1). Null bez prošlog.</summary>
    public decimal? Zadrzavanje { get; init; }
    public decimal PotrosnjaPoKupcu { get; init; }
    public decimal NarudzbiPoKupcu { get; init; }
    public int Gosti { get; init; }
    public int Registrovani { get; init; }
    public int NoviPrekoPreporuke { get; init; }
    public IReadOnlyList<KupacAnalitikeDto> TopKupci { get; init; } = [];
}

public record KupacAnalitikeDto
{
    public string Ime { get; init; } = string.Empty;
    public bool Gost { get; init; }
    public int Narudzbi { get; init; }
    public decimal Neto { get; init; }
}

public record PretplateAnalitikaDto
{
    public int AktivneNaKraju { get; init; }
    public int Nove { get; init; }
    public int Otkazane { get; init; }
    public decimal? StopaOtkazivanja { get; init; }
    /// <summary>Samo za tekući mjesec — istorija pauza se ne čuva.</summary>
    public int? Pauzirane { get; init; }
    public IReadOnlyList<PlanAnalitikeDto> PoPlanu { get; init; } = [];
    public decimal UdioPrihoda { get; init; }
}

public record PlanAnalitikeDto
{
    public string Plan { get; init; } = string.Empty;
    public int Aktivne { get; init; }
}

public record DostavaAnalitikaDto
{
    public IReadOnlyList<ZonaAnalitikeDto> PoZoni { get; init; } = [];
    public int Isporucene { get; init; }
    public int Neuspjele { get; init; }
    public decimal? Uspjesnost { get; init; }
    public int Otkazane { get; init; }
    public decimal OtkazaneVrijednost { get; init; }
    public decimal? OtkazaneUdio { get; init; }
    public decimal? ProsjecnaOcjena { get; init; }
    public int BrojOcjena { get; init; }
}

public record ZonaAnalitikeDto
{
    public string Zona { get; init; } = string.Empty;
    public int Narudzbi { get; init; }
    public decimal Neto { get; init; }
}

public record GazdinstvoDto
{
    public string Naziv { get; init; } = string.Empty;
    public string Selo { get; init; } = string.Empty;
    public decimal Nabavno { get; init; }
    public IReadOnlyList<ArtikalGazdinstvaDto> Artikli { get; init; } = [];
}

public record ArtikalGazdinstvaDto
{
    public string Naziv { get; init; } = string.Empty;
    public string Jedinica { get; init; } = string.Empty;
    public decimal Kolicina { get; init; }
    public decimal Nabavno { get; init; }
}

/// <summary>Godišnji pregled: 12 mjeseci i prihod po kategoriji po mjesecima.</summary>
public record PoslovanjeGodineDto
{
    public int Godina { get; init; }
    public IReadOnlyList<MjesecGodineDto> Mjeseci { get; init; } = [];
    public IReadOnlyList<SezonaKategorijeDto> Sezona { get; init; } = [];
}

public record MjesecGodineDto
{
    public int Mjesec { get; init; }
    public decimal Neto { get; init; }
    public decimal Zarada { get; init; }
    public decimal? Marza { get; init; }
    public int Narudzbi { get; init; }
    public int AktivniKupci { get; init; }
    public int AktivnePretplate { get; init; }
    public bool Procjena { get; init; }
}

public record SezonaKategorijeDto
{
    public string Kategorija { get; init; } = string.Empty;
    /// <summary>Prihod po mjesecima, indeks 0 = januar.</summary>
    public IReadOnlyList<decimal> Mjeseci { get; init; } = [];
}
```

- [ ] **Step 4: Data loading**

`backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjePodaci.cs`:

```csharp
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Admin.Poslovanje;

/// <summary>Učitavanje za analitiku. Sve read-only; račun je u PoslovanjeRacun.</summary>
public static class PoslovanjePodaci
{
    /// <summary>Narudžbe (svih statusa) tačno datih sedmica, sa stavkama, zonom i ocjenom.</summary>
    public static async Task<List<Order>> NarudzbeAsync(
        IAppDbContext db, IReadOnlyCollection<(int Godina, int Sedmica)> sedmice, CancellationToken ct)
    {
        if (sedmice.Count == 0) return [];
        var godine = sedmice.Select(s => s.Godina).Distinct().ToList();
        var brojevi = sedmice.Select(s => s.Sedmica).Distinct().ToList();
        var skup = sedmice.ToHashSet();
        var narudzbe = await db.Orders.AsNoTracking().AsSplitQuery()
            .Include(o => o.Items).ThenInclude(i => i.KorpicaStavke)
            .Include(o => o.DeliveryZone)
            .Include(o => o.Ocjena)
            .Where(o => godine.Contains(o.Godina) && brojevi.Contains(o.Sedmica))
            .ToListAsync(ct);
        return narudzbe.Where(o => skup.Contains((o.Godina, o.Sedmica))).ToList();
    }

    /// <summary>Cijeli katalog, i obrisani artikli — stare narudžbe ih i dalje nose.</summary>
    public static Task<Dictionary<Guid, Product>> KatalogAsync(IAppDbContext db, CancellationToken ct) =>
        db.Products.IgnoreQueryFilters().AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Producer)
            .ToDictionaryAsync(p => p.Id, ct);
}
```

- [ ] **Step 5: The calculator (prihod dio)**

`backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeRacun.cs`:

```csharp
using EPiljara.Application.DTOs;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Application.Features.Admin.Poslovanje;

/// <summary>
/// Sav račun analitike, nad narudžbama u memoriji — bez baze, da se pravila testiraju direktno.
/// Pravila su u specifikaciji (docs/superpowers/specs/2026-11-03-poslovanje-analitika-design.md).
/// </summary>
public sealed class PoslovanjeRacun(IReadOnlyDictionary<Guid, Product> katalog)
{
    /// <summary>Jedan red robe: stavka Špajza ili red sadržaja korpice (pomnožen brojem kutija).</summary>
    internal sealed record Linija(
        Order Narudzba, Guid ProductId, string Naziv, string Jedinica, decimal Kolicina,
        decimal Prihod, decimal? NabavnaJedinicna, bool Procjena, bool IzKorpice)
    {
        public decimal? Nabavno => NabavnaJedinicna is decimal c ? Kolicina * c : null;
    }

    public static decimal Neto(Order o) => o.Subtotal + o.DeliveryFee - o.Popust - o.PoeniPopust;

    public static IEnumerable<Order> Isporucene(IEnumerable<Order> narudzbe) =>
        narudzbe.Where(o => o.Status == OrderStatus.Delivered);

    internal static bool JeKorpica(OrderItem i, IReadOnlyDictionary<Guid, Product> katalog) =>
        i.KorpicaStavke.Count > 0
        || (katalog.TryGetValue(i.ProductId, out var p) && p.Vrsta == VrstaProizvoda.Korpica);

    internal IEnumerable<Linija> Linije(Order o)
    {
        foreach (var i in o.Items)
        {
            if (JeKorpica(i, katalog))
            {
                foreach (var s in i.KorpicaStavke)
                {
                    var (nabavna, procjena) = Nabavna(s.NabavnaCijena, s.ProductId);
                    var kol = s.Kolicina * i.Quantity;
                    yield return new Linija(o, s.ProductId, s.ProductName, s.Unit.ToApi(), kol,
                        kol * s.JedinicnaCijena, nabavna, procjena, IzKorpice: true);
                }
            }
            else
            {
                var (nabavna, procjena) = Nabavna(i.NabavnaCijena, i.ProductId);
                yield return new Linija(o, i.ProductId, i.ProductName, i.Unit.ToApi(), i.Quantity,
                    i.Quantity * i.UnitPrice, nabavna, procjena, IzKorpice: false);
            }
        }
    }

    private (decimal? Cijena, bool Procjena) Nabavna(decimal? snimak, Guid productId)
    {
        if (snimak is decimal s) return (s, false);
        return katalog.TryGetValue(productId, out var p) && p.NabavnaCijena is decimal c ? (c, true) : (null, false);
    }

    internal static decimal? Udio(decimal dio, decimal cijelo, int decimala = 4) =>
        cijelo == 0 ? null : Math.Round(dio / cijelo, decimala);

    public PrihodDto Prihod(IReadOnlyCollection<Order> isporucene)
    {
        var linije = isporucene.SelectMany(Linije).ToList();
        var promet = isporucene.Sum(o => o.Subtotal);
        var nabavno = linije.Sum(l => l.Nabavno ?? 0m);
        var zarada = promet - nabavno;
        var stavke = isporucene.SelectMany(o => o.Items).ToList();
        var ukupnoStavki = stavke.Sum(i => i.Quantity * i.UnitPrice);
        var korpice = stavke.Where(i => JeKorpica(i, katalog)).Sum(i => i.Quantity * i.UnitPrice);
        var neto = isporucene.Sum(Neto);
        var n = isporucene.Count;
        return new PrihodDto
        {
            PrometRobe = promet,
            Dostava = isporucene.Sum(o => o.DeliveryFee),
            PromoPopust = isporucene.Sum(o => o.Popust),
            PoeniPopust = isporucene.Sum(o => o.PoeniPopust),
            Neto = neto,
            Nabavno = Math.Round(nabavno, 2),
            Zarada = Math.Round(zarada, 2),
            Marza = linije.Any(l => l.Nabavno is not null) ? Udio(zarada, promet) : null,
            BrojNarudzbi = n,
            ProsjecnaNarudzba = n == 0 ? 0m : Math.Round(neto / n, 2),
            UdioKorpica = Udio(korpice, ukupnoStavki) ?? 0m,
            UdioSpajza = ukupnoStavki == 0 ? 0m : 1m - (Udio(korpice, ukupnoStavki) ?? 0m),
            Procjena = linije.Any(l => l.Procjena),
            BezNabavneCijene = linije.Count(l => l.NabavnaJedinicna is null),
        };
    }
}
```

`ToApi()` za `Unit` je postojeći extension u `EPiljara.Domain.Enums` (`Enums.cs:85`), pokriven gornjim `using`-om.

- [ ] **Step 6: The query, with stubs for later sections**

`backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeUpiti.cs`:

```csharp
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Features.Korpice;
using EPiljara.Domain.Common;
using MediatR;
using Microsoft.Extensions.Options;

namespace EPiljara.Application.Features.Admin.Poslovanje;

/// <summary>Analitika mjeseca. Bez parametara: tekući mjesec (po Sarajevu).</summary>
public record GetPoslovanjeQuery(int? Godina = null, int? Mjesec = null) : IRequest<PoslovanjeMjesecaDto>;

public class GetPoslovanjeHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetPoslovanjeQuery, PoslovanjeMjesecaDto>
{
    public async Task<PoslovanjeMjesecaDto> Handle(GetPoslovanjeQuery q, CancellationToken ct)
    {
        var sada = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SedmicaHelper.Zona);
        var godina = q.Godina ?? sada.Year;
        var mjesec = q.Mjesec ?? sada.Month;
        if (mjesec is < 1 or > 12)
            throw new DomainException("Mjesec mora biti od 1 do 12.", "mjesec_neispravan");

        var (dan, _, _) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var (pg, pm) = PoslovanjeKalendar.Prethodni(godina, mjesec);
        var ove = PoslovanjeKalendar.SedmiceMjeseca(godina, mjesec, dan);
        var prosle = PoslovanjeKalendar.SedmiceMjeseca(pg, pm, dan);

        var narudzbe = await PoslovanjePodaci.NarudzbeAsync(db, [.. ove, .. prosle], ct);
        var uMjesecu = narudzbe.Where(o => ove.Contains((o.Godina, o.Sedmica))).ToList();
        var uProslom = narudzbe.Where(o => prosle.Contains((o.Godina, o.Sedmica))).ToList();
        var isporucene = PoslovanjeRacun.Isporucene(uMjesecu).ToList();
        var prosleIsporucene = PoslovanjeRacun.Isporucene(uProslom).ToList();

        var racun = new PoslovanjeRacun(await PoslovanjePodaci.KatalogAsync(db, ct));
        return new PoslovanjeMjesecaDto
        {
            Godina = godina,
            Mjesec = mjesec,
            Prihod = racun.Prihod(isporucene),
            Prethodni = racun.Prihod(prosleIsporucene),
        };
    }
}
```

- [ ] **Step 7: Endpoint**

U `AdminController.cs`, poslije metode `Dashboard`:

```csharp
    [HttpGet("admin/poslovanje")]
    public async Task<IActionResult> Poslovanje([FromQuery] int? godina, [FromQuery] int? mjesec) =>
        Envelope(await Mediator.Send(new GetPoslovanjeQuery(godina, mjesec)));
```

i `using EPiljara.Application.Features.Admin.Poslovanje;` na vrhu.

- [ ] **Step 8: Run tests**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests --filter "FullyQualifiedName~Poslovanje"`
Expected: PASS svi testovi `PoslovanjePrihodTests` i `PoslovanjeKalendarTests`. (`Prazan_mjesec...` prolazi jer `Kupci`, `Dostava`, `Gazdinstva` imaju podrazumijevane prazne vrijednosti.)

- [ ] **Step 9: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(poslovanje): prihod i zarada po mjesecu isporuke, endpoint /admin/poslovanje"
```

---

### Task 4: Kupci

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeRacun.cs`
- Modify: `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjePodaci.cs`
- Modify: `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeUpiti.cs`
- Test: `backend/tests/EPiljara.Application.Tests/PoslovanjeTests.cs` (klasa `PoslovanjeKupciTests`)

**Interfaces:**
- Consumes: `PoslovanjePodaciTest` (Task 3), `PoslovanjeKalendar.Indeks`.
- Produces:
  - `static string PoslovanjeRacun.KljucKupca(Guid? customerId, string? email, string? telefon, Guid orderId)`
  - `KupciDto PoslovanjeRacun.Kupci(IReadOnlyCollection<Order> isporucene, IReadOnlyCollection<Order> prosleIsporucene, IReadOnlyDictionary<string, int> prviMjesec, int indeksMjeseca, IReadOnlySet<Guid> preporuceni)`
  - `PoslovanjePodaci.PrviMjeseciAsync(IAppDbContext, DayOfWeek, CancellationToken) : Task<Dictionary<string, int>>` (ključ kupca → `Indeks` mjeseca prve isporučene narudžbe)
  - `PoslovanjePodaci.PreporuceniAsync(IAppDbContext, CancellationToken) : Task<HashSet<Guid>>`

- [ ] **Step 1: Write the failing tests**

```csharp
public class PoslovanjeKupciTests
{
    [Fact]
    public async Task Novi_povratni_i_zadrzavanje()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var marko = await p.KupacAsync("Marko");
        var jovan = await p.KupacAsync("Jovan");
        await p.NarudzbaAsync(38, OrderStatus.Delivered, krompirKg: 1, kupacId: marko.Id);   // septembar
        await p.NarudzbaAsync(39, OrderStatus.Delivered, krompirKg: 1, kupacId: jovan.Id);   // septembar
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 2, kupacId: marko.Id);
        await p.NarudzbaAsync(42, OrderStatus.Delivered, krompirKg: 3, kupacId: marko.Id);
        await p.NarudzbaAsync(41, OrderStatus.Delivered, jaja: 10, kupac: "Ana", email: " Ana@Mail.com ");

        var k = (await p.MjesecAsync(10)).Kupci;

        Assert.Equal(2, k.Aktivni);               // Marko, Ana
        Assert.Equal(2, k.AktivniPrethodni);      // Marko, Jovan
        Assert.Equal(1, k.Novi);                  // Ana
        Assert.Equal(1, k.Povratni);              // Marko
        Assert.Equal(0.5m, k.Zadrzavanje);        // od Marka i Jovana vratio se Marko
        Assert.Equal(1, k.Gosti);
        Assert.Equal(1, k.Registrovani);
        Assert.Equal(1.5m, k.NarudzbiPoKupcu);    // 3 narudžbe / 2 kupca
        Assert.Equal(7.5m, k.PotrosnjaPoKupcu);   // (4 + 6 + 5) / 2
        Assert.Equal("Marko Marković", k.TopKupci[0].Ime);
        Assert.Equal(2, k.TopKupci[0].Narudzbi);
    }

    [Fact]
    public async Task Isti_gost_po_emailu_se_ne_broji_dvaput_a_gosti_bez_kontakta_se_ne_spajaju()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, jaja: 2, kupac: "Ana", email: "ana@mail.com");
        await p.NarudzbaAsync(42, OrderStatus.Delivered, jaja: 2, kupac: "Ana", email: "ANA@mail.com ");
        await p.NarudzbaAsync(41, OrderStatus.Delivered, jaja: 2, kupac: "Gost 1");
        await p.NarudzbaAsync(42, OrderStatus.Delivered, jaja: 2, kupac: "Gost 2");

        var k = (await p.MjesecAsync(10)).Kupci;

        Assert.Equal(3, k.Aktivni);
        Assert.Equal(3, k.Gosti);
    }

    [Fact]
    public async Task Novi_preko_preporuke()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var marko = await p.KupacAsync("Marko");
        var jovan = await p.KupacAsync("Jovan", preporucio: marko.Id);
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 1, kupacId: jovan.Id);

        Assert.Equal(1, (await p.MjesecAsync(10)).Kupci.NoviPrekoPreporuke);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests --filter "FullyQualifiedName~PoslovanjeKupciTests"`
Expected: FAIL — `Assert.Equal() Failure: Expected: 2, Actual: 0` (Kupci je prazan).

- [ ] **Step 3: Implement**

U `PoslovanjeRacun` dodaj:

```csharp
    /// <summary>Registrovan kupac po nalogu; gost po emailu, pa telefonu, pa (bez kontakta) po narudžbi.</summary>
    public static string KljucKupca(Guid? customerId, string? email, string? telefon, Guid orderId)
    {
        if (customerId is Guid id) return id.ToString();
        var e = email?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(e)) return "g:" + e;
        var t = new string((telefon ?? string.Empty).Where(char.IsDigit).ToArray());
        return t.Length > 0 ? "g:" + t : "g:" + orderId;
    }

    private static string Kljuc(Order o) => KljucKupca(o.CustomerId, o.CustomerEmail, o.CustomerPhone, o.Id);

    public KupciDto Kupci(
        IReadOnlyCollection<Order> isporucene, IReadOnlyCollection<Order> prosleIsporucene,
        IReadOnlyDictionary<string, int> prviMjesec, int indeksMjeseca, IReadOnlySet<Guid> preporuceni)
    {
        var poKupcu = isporucene.GroupBy(Kljuc).ToList();
        var aktivni = poKupcu.Select(g => g.Key).ToHashSet();
        var prosli = prosleIsporucene.Select(Kljuc).ToHashSet();
        var novi = aktivni.Where(k => prviMjesec.TryGetValue(k, out var m) && m == indeksMjeseca).ToHashSet();
        var neto = isporucene.Sum(Neto);
        return new KupciDto
        {
            Aktivni = aktivni.Count,
            AktivniPrethodni = prosli.Count,
            Novi = novi.Count,
            Povratni = aktivni.Count - novi.Count,
            Zadrzavanje = Udio(prosli.Count(aktivni.Contains), prosli.Count),
            PotrosnjaPoKupcu = aktivni.Count == 0 ? 0m : Math.Round(neto / aktivni.Count, 2),
            NarudzbiPoKupcu = aktivni.Count == 0 ? 0m : Math.Round((decimal)isporucene.Count / aktivni.Count, 2),
            Gosti = aktivni.Count(k => k.StartsWith("g:")),
            Registrovani = aktivni.Count(k => !k.StartsWith("g:")),
            NoviPrekoPreporuke = novi.Count(k => Guid.TryParse(k, out var id) && preporuceni.Contains(id)),
            TopKupci = poKupcu
                .Select(g => new KupacAnalitikeDto
                {
                    Ime = g.OrderByDescending(o => o.CreatedAt).First().CustomerName,
                    Gost = g.Key.StartsWith("g:"),
                    Narudzbi = g.Count(),
                    Neto = g.Sum(Neto),
                })
                .OrderByDescending(k => k.Neto).ThenBy(k => k.Ime)
                .Take(10)
                .ToList(),
        };
    }
```

U `PoslovanjePodaci` dodaj:

```csharp
    /// <summary>Za svakog kupca: indeks mjeseca (PoslovanjeKalendar.Indeks) njegove prve isporučene narudžbe.</summary>
    public static async Task<Dictionary<string, int>> PrviMjeseciAsync(IAppDbContext db, DayOfWeek dan, CancellationToken ct)
    {
        var redovi = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Delivered)
            .Select(o => new { o.Id, o.CustomerId, o.CustomerEmail, o.CustomerPhone, o.Godina, o.Sedmica })
            .ToListAsync(ct);
        return redovi
            .GroupBy(r => PoslovanjeRacun.KljucKupca(r.CustomerId, r.CustomerEmail, r.CustomerPhone, r.Id))
            .ToDictionary(g => g.Key, g => g.Min(r =>
            {
                var (gd, m) = PoslovanjeKalendar.MjesecIsporuke(r.Godina, r.Sedmica, dan);
                return PoslovanjeKalendar.Indeks(gd, m);
            }));
    }

    public static async Task<HashSet<Guid>> PreporuceniAsync(IAppDbContext db, CancellationToken ct) =>
        (await db.Users.AsNoTracking().Where(u => u.PreporucioId != null).Select(u => u.Id).ToListAsync(ct)).ToHashSet();
```

(`using EPiljara.Domain.Enums;` na vrh `PoslovanjePodaci.cs`.)

U `GetPoslovanjeHandler.Handle`, u inicijalizator rezultata dodaj:

```csharp
            Kupci = racun.Kupci(
                isporucene, prosleIsporucene,
                await PoslovanjePodaci.PrviMjeseciAsync(db, dan, ct),
                PoslovanjeKalendar.Indeks(godina, mjesec),
                await PoslovanjePodaci.PreporuceniAsync(db, ct)),
```

- [ ] **Step 4: Run tests**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests --filter "FullyQualifiedName~Poslovanje"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(poslovanje): kupci — aktivni, novi, povratni, zadržavanje, top kupci"
```

---

### Task 5: Prodaja, gazdinstva i dostava

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeRacun.cs`
- Modify: `backend/src/EPiljara.Application/Features/Admin/Poslovanje/PoslovanjeUpiti.cs`
- Test: `backend/tests/EPiljara.Application.Tests/PoslovanjeTests.cs` (klasa `PoslovanjeProdajaTests`)

**Interfaces:**
- Produces: `ProdajaDto Prodaja(IReadOnlyCollection<Order> isporucene)`, `IReadOnlyList<GazdinstvoDto> Gazdinstva(IReadOnlyCollection<Order> isporucene)`, `DostavaAnalitikaDto Dostava(IReadOnlyCollection<Order> sveUMjesecu)` na `PoslovanjeRacun`.

- [ ] **Step 1: Write the failing tests**

```csharp
public class PoslovanjeProdajaTests
{
    [Fact]
    public async Task Korpice_spajz_po_kategoriji_i_top_artikli()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 3, korpica: 2, jaja: 10);

        var d = (await p.MjesecAsync(10)).Prodaja;

        var k = Assert.Single(d.Korpice);
        Assert.Equal("Mala korpica povrća", k.Naziv);
        Assert.Equal("povrce", k.Tip);
        Assert.Equal("Mala", k.Velicina);
        Assert.Equal(2m, k.Komada);
        Assert.Equal(20m, k.Prihod);
        Assert.Equal(4m, k.Nabavno);              // 2 kutije × 2 kg × 1 KM
        Assert.Equal(0.8m, k.Marza);
        Assert.Equal(["Jaja", "Povrće"], d.Spajz.Select(s => s.Kategorija).OrderBy(x => x).ToArray());
        var krompir = d.TopArtikli.Single(a => a.Naziv == "Krompir");
        Assert.Equal(7m, krompir.Kolicina);       // 3 Špajz + 4 iz korpica
        Assert.Equal(14m, krompir.Prihod);
        Assert.Equal(7m, krompir.Zarada);
    }

    [Fact]
    public async Task Gazdinstva_sabiraju_nabavno_po_proizvodjacu()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 3, korpica: 1, jaja: 10);

        var g = (await p.MjesecAsync(10)).Gazdinstva;

        Assert.Equal(["Marko", "Ana"], g.Select(x => x.Naziv).ToArray()); // po nabavnom, opadajuće
        Assert.Equal(5m, g[0].Nabavno);           // 3 + 2 kg krompira
        Assert.Equal(5m, Assert.Single(g[0].Artikli).Kolicina);
        Assert.Equal(3m, g[1].Nabavno);
    }

    [Fact]
    public async Task Obrisan_artikal_se_i_dalje_broji_kod_svog_gazdinstva()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 2);
        var k = await p.T.Db.Products.IgnoreQueryFilters().FirstAsync(x => x.Id == p.Krompir.Id);
        k.IsDeleted = true;
        await p.T.Db.SaveChangesAsync();
        p.T.Db.ChangeTracker.Clear();

        var d = await p.MjesecAsync(10);

        Assert.Equal("Krompir", Assert.Single(d.Prodaja.TopArtikli).Naziv);
        Assert.Equal("Marko", Assert.Single(d.Gazdinstva).Naziv);
        Assert.Equal(2m, d.Gazdinstva[0].Nabavno);
    }

    [Fact]
    public async Task Dostava_po_zoni_uspjesnost_i_otkazane()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 2);
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 1, zona: p.Laus);
        await p.NarudzbaAsync(42, OrderStatus.Delivered, krompirKg: 1, zona: p.Laus);
        await p.NarudzbaAsync(42, OrderStatus.FailedDelivery, krompirKg: 1);
        await p.NarudzbaAsync(43, OrderStatus.Cancelled, krompirKg: 5);

        var d = (await p.MjesecAsync(10)).Dostava;

        Assert.Equal(3, d.Isporucene);
        Assert.Equal(1, d.Neuspjele);
        Assert.Equal(0.75m, d.Uspjesnost);
        Assert.Equal(1, d.Otkazane);
        Assert.Equal(10m, d.OtkazaneVrijednost);
        Assert.Equal(0.2m, d.OtkazaneUdio);       // 1 od 5 narudžbi
        Assert.Equal("Lauš", d.PoZoni[0].Zona);   // 2 narudžbe, 4 KM
        Assert.Equal(2, d.PoZoni[0].Narudzbi);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests --filter "FullyQualifiedName~PoslovanjeProdajaTests"`
Expected: FAIL — `Assert.Single() Failure: The collection was empty`.

- [ ] **Step 3: Implement**

U `PoslovanjeRacun` dodaj (`using EPiljara.Application.Features.Korpice;` na vrh, za `SedmicaHelper.Slug`):

```csharp
    public ProdajaDto Prodaja(IReadOnlyCollection<Order> isporucene)
    {
        var korpice = isporucene
            .SelectMany(o => o.Items.Where(i => JeKorpica(i, katalog)))
            .GroupBy(i => i.ProductId)
            .Select(g =>
            {
                katalog.TryGetValue(g.Key, out var p);
                var prihod = g.Sum(i => i.Quantity * i.UnitPrice);
                var nabavno = g.SelectMany(i => i.KorpicaStavke.Select(s =>
                    (s.NabavnaCijena ?? Nabavna(null, s.ProductId).Cijena ?? 0m) * s.Kolicina * i.Quantity)).Sum();
                return new KorpicaProdajeDto
                {
                    Naziv = p?.Name ?? g.First().ProductName,
                    Tip = p?.TipKorpice is { } t ? SedmicaHelper.Slug(t) : string.Empty,
                    Velicina = p?.Velicina?.ToString() ?? string.Empty,
                    Komada = g.Sum(i => i.Quantity),
                    Prihod = prihod,
                    Nabavno = Math.Round(nabavno, 2),
                    Marza = Udio(prihod - nabavno, prihod),
                };
            })
            .OrderByDescending(k => k.Prihod)
            .ToList();

        var spajz = isporucene.SelectMany(Linije)
            .Where(l => !l.IzKorpice)
            .GroupBy(l => KategorijaArtikla(l.ProductId))
            .Select(g =>
            {
                var prihod = g.Sum(l => l.Prihod);
                var nabavno = g.Sum(l => l.Nabavno ?? 0m);
                return new KategorijaProdajeDto
                {
                    Kategorija = g.Key, Prihod = prihod, Nabavno = Math.Round(nabavno, 2), Marza = Udio(prihod - nabavno, prihod),
                };
            })
            .OrderByDescending(k => k.Prihod)
            .ToList();

        var artikli = isporucene.SelectMany(Linije)
            .GroupBy(l => l.ProductId)
            .Select(g => new ArtikalProdajeDto
            {
                Naziv = katalog.TryGetValue(g.Key, out var p) ? p.Name : g.First().Naziv,
                Jedinica = g.First().Jedinica,
                Kolicina = g.Sum(l => l.Kolicina),
                Prihod = Math.Round(g.Sum(l => l.Prihod), 2),
                Zarada = Math.Round(g.Sum(l => l.Prihod - (l.Nabavno ?? 0m)), 2),
            })
            .ToList();
        var top = artikli.OrderByDescending(a => a.Prihod).ThenBy(a => a.Naziv).Take(10).ToList();
        var najslabiji = artikli.Except(top).OrderBy(a => a.Prihod).ThenBy(a => a.Naziv).Take(10).ToList();

        return new ProdajaDto { Korpice = korpice, Spajz = spajz, TopArtikli = top, NajslabijiArtikli = najslabiji };
    }

    internal string KategorijaArtikla(Guid productId) =>
        katalog.TryGetValue(productId, out var p) && p.Category is { } c ? c.Name : "Bez kategorije";

    public IReadOnlyList<GazdinstvoDto> Gazdinstva(IReadOnlyCollection<Order> isporucene) =>
        isporucene.SelectMany(Linije)
            .GroupBy(l => katalog.TryGetValue(l.ProductId, out var p) && p.Producer is { } pr
                ? (pr.Name, pr.Village) : ("Nepoznat proizvođač", string.Empty))
            .Select(g => new GazdinstvoDto
            {
                Naziv = g.Key.Item1,
                Selo = g.Key.Item2,
                Nabavno = Math.Round(g.Sum(l => l.Nabavno ?? 0m), 2),
                Artikli = g.GroupBy(l => (l.ProductId, l.Jedinica))
                    .Select(a => new ArtikalGazdinstvaDto
                    {
                        Naziv = katalog.TryGetValue(a.Key.ProductId, out var p) ? p.Name : a.First().Naziv,
                        Jedinica = a.Key.Jedinica,
                        Kolicina = a.Sum(l => l.Kolicina),
                        Nabavno = Math.Round(a.Sum(l => l.Nabavno ?? 0m), 2),
                    })
                    .OrderByDescending(a => a.Nabavno).ThenBy(a => a.Naziv)
                    .ToList(),
            })
            .OrderByDescending(g => g.Nabavno).ThenBy(g => g.Naziv)
            .ToList();

    public DostavaAnalitikaDto Dostava(IReadOnlyCollection<Order> sveUMjesecu)
    {
        var isporucene = Isporucene(sveUMjesecu).ToList();
        var neuspjele = sveUMjesecu.Count(o => o.Status == OrderStatus.FailedDelivery);
        var otkazane = sveUMjesecu.Where(o => o.Status == OrderStatus.Cancelled).ToList();
        var ocjene = isporucene.Where(o => o.Ocjena is not null).Select(o => o.Ocjena!.Ocjena).ToList();
        return new DostavaAnalitikaDto
        {
            PoZoni = isporucene
                .GroupBy(o => o.DeliveryZone?.Name ?? "Bez zone")
                .Select(g => new ZonaAnalitikeDto { Zona = g.Key, Narudzbi = g.Count(), Neto = g.Sum(Neto) })
                .OrderByDescending(z => z.Narudzbi).ThenByDescending(z => z.Neto).ThenBy(z => z.Zona)
                .ToList(),
            Isporucene = isporucene.Count,
            Neuspjele = neuspjele,
            Uspjesnost = Udio(isporucene.Count, isporucene.Count + neuspjele),
            Otkazane = otkazane.Count,
            OtkazaneVrijednost = otkazane.Sum(o => o.TotalAmount),
            OtkazaneUdio = Udio(otkazane.Count, sveUMjesecu.Count),
            ProsjecnaOcjena = ocjene.Count == 0 ? null : Math.Round((decimal)ocjene.Average(), 2),
            BrojOcjena = ocjene.Count,
        };
    }
```

U `GetPoslovanjeHandler` u rezultat dodaj:

```csharp
            Prodaja = racun.Prodaja(isporucene),
            Dostava = racun.Dostava(uMjesecu),
            Gazdinstva = racun.Gazdinstva(isporucene),
```

- [ ] **Step 4: Run tests**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests --filter "FullyQualifiedName~Poslovanje"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(poslovanje): prodaja, gazdinstva i dostava"
```

---

### Task 6: Pretplate i godišnji pregled

**Files:**
- Modify: `PoslovanjeRacun.cs`, `PoslovanjeUpiti.cs`, `AdminController.cs`
- Test: `PoslovanjeTests.cs` (klase `PoslovanjePretplateTests`, `PoslovanjeGodinaTests`)

**Interfaces:**
- Produces:
  - `PretplateAnalitikaDto PoslovanjeRacun.Pretplate(IReadOnlyCollection<Pretplata> sve, int godina, int mjesec, bool tekuci, IReadOnlyCollection<Order> isporucene)`
  - `static int PoslovanjeRacun.AktivnePretplate(IEnumerable<Pretplata> sve, DateTime trenutakUtc)`
  - `GetPoslovanjeGodinaQuery(int? Godina) : IRequest<PoslovanjeGodineDto>`, `GET /admin/poslovanje/godina?godina=`

- [ ] **Step 1: Write the failing tests**

```csharp
public class PoslovanjePretplateTests
{
    private static async Task<Pretplata> PretplataAsync(PoslovanjePodaciTest p, User u, DateTime kreirana,
        DateTime? otkazana = null, PlanPretplate plan = PlanPretplate.Mjesecna)
    {
        var x = new Pretplata
        {
            UserId = u.Id, ProductId = p.Korpica.Id, DeliveryZoneId = p.Centar.Id, DeliverySlotId = p.Slot.Id,
            DeliveryAddress = "Testna 1", Plan = plan, CreatedAt = kreirana, OtkazanaU = otkazana,
            Status = otkazana is null ? StatusPretplate.Aktivna : StatusPretplate.Otkazana,
        };
        p.T.Db.Pretplate.Add(x);
        await p.T.Db.SaveChangesAsync();
        return x;
    }

    /// <summary>
    /// Oktobar 2025 (prošlost, pa nikad „tekući" mjesec — Pauzirane mora biti null).
    /// Prva: od avgusta, aktivna. Druga: od septembra, otkazana 10.10. Treća: nova 5.10.
    /// Sedmica 41/2025 = isporuka u četvrtak 9.10.2025.
    /// </summary>
    [Fact]
    public async Task Aktivne_nove_otkazane_i_stopa()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        var u = await p.KupacAsync("Marko");
        await PretplataAsync(p, u, new DateTime(2025, 8, 20, 10, 0, 0, DateTimeKind.Utc));
        await PretplataAsync(p, u, new DateTime(2025, 9, 15, 10, 0, 0, DateTimeKind.Utc),
            otkazana: new DateTime(2025, 10, 10, 10, 0, 0, DateTimeKind.Utc));
        var treca = await PretplataAsync(p, u, new DateTime(2025, 10, 5, 10, 0, 0, DateTimeKind.Utc), plan: PlanPretplate.Nedjeljna);
        await p.NarudzbaAsync(41, OrderStatus.Delivered, korpica: 1, kupacId: u.Id, pretplataId: treca.Id, godina: 2025);
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 5, kupacId: u.Id, godina: 2025);

        var d = (await p.MjesecAsync(10, godina: 2025)).Pretplate;

        Assert.Equal(2, d.AktivneNaKraju);        // prva i treća
        Assert.Equal(1, d.Nove);
        Assert.Equal(1, d.Otkazane);
        Assert.Equal(0.5m, d.StopaOtkazivanja);   // 1 od 2 aktivne na početku oktobra
        Assert.Null(d.Pauzirane);                 // samo za tekući mjesec
        Assert.Equal(0.5m, d.UdioPrihoda);        // 10 od 20 KM
        Assert.Contains(d.PoPlanu, x => x.Plan == "Nedjeljna" && x.Aktivne == 1);
    }
}
```

```csharp
public class PoslovanjeGodinaTests
{
    [Fact]
    public async Task Dvanaest_mjeseci_i_sezona_po_kategoriji()
    {
        using var p = await PoslovanjePodaciTest.NapraviAsync();
        await p.NarudzbaAsync(41, OrderStatus.Delivered, krompirKg: 3);           // oktobar
        await p.NarudzbaAsync(39, OrderStatus.Delivered, jaja: 10);               // septembar
        await p.NarudzbaAsync(42, OrderStatus.Cancelled, krompirKg: 50);          // ne broji se

        var g = await new GetPoslovanjeGodinaHandler(p.T.Db, Options.Create(TestOpcije.Korpice()))
            .Handle(new GetPoslovanjeGodinaQuery(2026), default);

        Assert.Equal(12, g.Mjeseci.Count);
        Assert.Equal(6m, g.Mjeseci[9].Neto);      // oktobar
        Assert.Equal(5m, g.Mjeseci[8].Neto);      // septembar
        Assert.Equal(0, g.Mjeseci[0].Narudzbi);
        var povrce = g.Sezona.Single(s => s.Kategorija == "Povrće");
        Assert.Equal(6m, povrce.Mjeseci[9]);
        Assert.Equal(0m, povrce.Mjeseci[8]);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests --filter "FullyQualifiedName~PoslovanjePretplateTests|FullyQualifiedName~PoslovanjeGodinaTests"`
Expected: build FAIL — `GetPoslovanjeGodinaHandler` ne postoji.

- [ ] **Step 3: Implement pretplate**

U `PoslovanjeRacun`:

```csharp
    public static int AktivnePretplate(IEnumerable<Pretplata> sve, DateTime trenutakUtc) =>
        sve.Count(p => p.CreatedAt < trenutakUtc && (p.OtkazanaU is null || p.OtkazanaU >= trenutakUtc));

    public static PretplateAnalitikaDto Pretplate(
        IReadOnlyCollection<Pretplata> sve, int godina, int mjesec, bool tekuci, IReadOnlyCollection<Order> isporucene)
    {
        var pocetak = PoslovanjeKalendar.PocetakUtc(godina, mjesec);
        var (sg, sm) = mjesec == 12 ? (godina + 1, 1) : (godina, mjesec + 1);
        var kraj = PoslovanjeKalendar.PocetakUtc(sg, sm);
        var naPocetku = AktivnePretplate(sve, pocetak);
        var otkazane = sve.Count(p => p.OtkazanaU >= pocetak && p.OtkazanaU < kraj);
        var neto = isporucene.Sum(Neto);
        return new PretplateAnalitikaDto
        {
            AktivneNaKraju = AktivnePretplate(sve, kraj),
            Nove = sve.Count(p => p.CreatedAt >= pocetak && p.CreatedAt < kraj),
            Otkazane = otkazane,
            StopaOtkazivanja = Udio(otkazane, naPocetku),
            Pauzirane = tekuci ? sve.Count(p => p.Status == StatusPretplate.Pauzirana) : null,
            PoPlanu = sve.Where(p => p.CreatedAt < kraj && (p.OtkazanaU is null || p.OtkazanaU >= kraj))
                .GroupBy(p => p.Plan)
                .Select(g => new PlanAnalitikeDto { Plan = g.Key.ToString(), Aktivne = g.Count() })
                .OrderBy(x => x.Plan)
                .ToList(),
            UdioPrihoda = Udio(isporucene.Where(o => o.PretplataId is not null).Sum(Neto), neto) ?? 0m,
        };
    }
```

U `GetPoslovanjeHandler`: `var sada` je već izračunat; u rezultat dodaj

```csharp
            Pretplate = PoslovanjeRacun.Pretplate(
                await db.Pretplate.AsNoTracking().ToListAsync(ct), godina, mjesec,
                tekuci: godina == sada.Year && mjesec == sada.Month, isporucene),
```

(`using Microsoft.EntityFrameworkCore;` na vrh `PoslovanjeUpiti.cs`.)

- [ ] **Step 4: Implement year query**

Na kraj `PoslovanjeUpiti.cs`:

```csharp
/// <summary>Godišnji pregled: glavni brojevi po mjesecima + prihod po kategoriji.</summary>
public record GetPoslovanjeGodinaQuery(int? Godina = null) : IRequest<PoslovanjeGodineDto>;

public class GetPoslovanjeGodinaHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije)
    : IRequestHandler<GetPoslovanjeGodinaQuery, PoslovanjeGodineDto>
{
    public async Task<PoslovanjeGodineDto> Handle(GetPoslovanjeGodinaQuery q, CancellationToken ct)
    {
        var godina = q.Godina ?? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SedmicaHelper.Zona).Year;
        var (dan, _, _) = await SedmicaHelper.UcitajRokAsync(db, opcije.Value, ct);
        var sedmicePoMjesecu = Enumerable.Range(1, 12)
            .ToDictionary(m => m, m => PoslovanjeKalendar.SedmiceMjeseca(godina, m, dan));
        var narudzbe = await PoslovanjePodaci.NarudzbeAsync(db, sedmicePoMjesecu.Values.SelectMany(s => s).ToList(), ct);
        var katalog = await PoslovanjePodaci.KatalogAsync(db, ct);
        var racun = new PoslovanjeRacun(katalog);
        var pretplate = await db.Pretplate.AsNoTracking().ToListAsync(ct);

        var mjeseci = new List<MjesecGodineDto>();
        var sezona = new Dictionary<string, decimal[]>();
        foreach (var (m, sedmice) in sedmicePoMjesecu)
        {
            var isporucene = PoslovanjeRacun.Isporucene(narudzbe.Where(o => sedmice.Contains((o.Godina, o.Sedmica)))).ToList();
            var prihod = racun.Prihod(isporucene);
            var (sg, sm) = m == 12 ? (godina + 1, 1) : (godina, m + 1);
            mjeseci.Add(new MjesecGodineDto
            {
                Mjesec = m,
                Neto = prihod.Neto,
                Zarada = prihod.Zarada,
                Marza = prihod.Marza,
                Narudzbi = prihod.BrojNarudzbi,
                AktivniKupci = isporucene
                    .Select(o => PoslovanjeRacun.KljucKupca(o.CustomerId, o.CustomerEmail, o.CustomerPhone, o.Id))
                    .Distinct().Count(),
                AktivnePretplate = PoslovanjeRacun.AktivnePretplate(pretplate, PoslovanjeKalendar.PocetakUtc(sg, sm)),
                Procjena = prihod.Procjena,
            });
            foreach (var l in isporucene.SelectMany(racun.Linije))
            {
                var kat = racun.KategorijaArtikla(l.ProductId);
                if (!sezona.TryGetValue(kat, out var niz)) sezona[kat] = niz = new decimal[12];
                niz[m - 1] += l.Prihod;
            }
        }

        return new PoslovanjeGodineDto
        {
            Godina = godina,
            Mjeseci = mjeseci,
            Sezona = sezona
                .Select(kv => new SezonaKategorijeDto { Kategorija = kv.Key, Mjeseci = kv.Value.Select(x => Math.Round(x, 2)).ToList() })
                .OrderByDescending(s => s.Mjeseci.Sum()).ThenBy(s => s.Kategorija)
                .ToList(),
        };
    }
}
```

U `AdminController.cs`:

```csharp
    [HttpGet("admin/poslovanje/godina")]
    public async Task<IActionResult> PoslovanjeGodina([FromQuery] int? godina) =>
        Envelope(await Mediator.Send(new GetPoslovanjeGodinaQuery(godina)));
```

- [ ] **Step 5: Run full backend suite**

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests`
Expected: sve prolazi.

- [ ] **Step 6: Smoke test against the local API**

Ugasi i ponovo pokreni prozor „EPiljara API" (primijeni migraciju iz Task 1). Pa u pwsh:

```powershell
$r = Invoke-RestMethod http://localhost:5080/auth/login -Method Post -ContentType 'application/json' -Body '{"email":"admin@epiljara.ba","password":"admin123"}'
$h = @{ Authorization = "Bearer $($r.data.accessToken ?? $r.data.token)" }
(Invoke-RestMethod "http://localhost:5080/admin/poslovanje?godina=2026&mjesec=10" -Headers $h).data.prihod
(Invoke-RestMethod "http://localhost:5080/admin/poslovanje/godina?godina=2026" -Headers $h).data.mjeseci | Format-Table
```

Expected: oktobar ima neto > 0, `procjena = True` (stare narudžbe nemaju snimak), godišnja tabela ima 12 redova, septembar–novembar nisu nula.

- [ ] **Step 7: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat(poslovanje): pretplate i godišnji pregled, endpoint /admin/poslovanje/godina"
```

---

### Task 7: Frontend — tipovi, API, pomoćne funkcije, meni

**Files:**
- Modify: `frontend/src/lib/api/contracts.ts` (na kraj)
- Modify: `frontend/src/lib/api/endpoints.ts` (u `adminApi`, pored `dashboard`)
- Create: `frontend/src/lib/admin/poslovanje.ts`
- Test: `frontend/src/lib/admin/poslovanje.test.ts`
- Modify: `frontend/src/components/admin/admin-nav.tsx`

**Interfaces:**
- Produces: TS tipovi `PoslovanjeMjeseca`, `PoslovanjeGodine` (i ugniježđeni) istih imena polja kao DTO-i u camelCase; `adminApi.poslovanje(godina?, mjesec?)`, `adminApi.poslovanjeGodina(godina?)`; iz `lib/admin/poslovanje.ts`: `promjena(sada, prije): number | null`, `procenat(x): string`, `MJESECI: string[]`, `susjedniMjesec(godina, mjesec, pomak): { godina; mjesec }`, `skalaGrafikona(vrijednosti: number[]): { max: number; podjele: number[] }`, `zaokruziGore(x): number`.

- [ ] **Step 1: Write the failing test**

```ts
import { test } from "node:test";
import assert from "node:assert/strict";
import { procenat, promjena, skalaGrafikona, susjedniMjesec, zaokruziGore } from "./poslovanje.ts";

test("promjena u procentima, bez dijeljenja nulom", () => {
  assert.equal(promjena(120, 100), 20);
  assert.equal(promjena(80, 100), -20);
  assert.equal(promjena(5, 0), null);
  assert.equal(promjena(0, 0), null);
  assert.equal(promjena(1, 3), -66.7);
});

test("procenat iz udjela 0–1, prazno je crtica", () => {
  assert.equal(procenat(0.619), "61,9%");
  assert.equal(procenat(null), "—");
  assert.equal(procenat(undefined), "—");
});

test("susjedni mjesec preko granice godine", () => {
  assert.deepEqual(susjedniMjesec(2026, 1, -1), { godina: 2025, mjesec: 12 });
  assert.deepEqual(susjedniMjesec(2026, 12, 1), { godina: 2027, mjesec: 1 });
  assert.deepEqual(susjedniMjesec(2026, 10, 1), { godina: 2026, mjesec: 11 });
});

test("skala grafikona zaokružuje gore na 1/2/5 × 10^n i ima pet podjela", () => {
  assert.equal(zaokruziGore(0), 1);
  assert.equal(zaokruziGore(1234), 2000);
  assert.equal(zaokruziGore(4100), 5000);
  assert.equal(zaokruziGore(5000), 5000);
  assert.deepEqual(skalaGrafikona([0, 3200, 1500]), { max: 5000, podjele: [0, 1250, 2500, 3750, 5000] });
  assert.deepEqual(skalaGrafikona([-50, 0]).max, 1); // gubitak se crta od nule naniže, skala prati pozitivne
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test 2>&1 | grep -E "poslovanje|fail"`
Expected: FAIL — `Cannot find module './poslovanje.ts'`.

- [ ] **Step 3: Implement helpers**

`frontend/src/lib/admin/poslovanje.ts`:

```ts
export const MJESECI = [
  "januar", "februar", "mart", "april", "maj", "jun",
  "jul", "avgust", "septembar", "oktobar", "novembar", "decembar",
];

/** Promjena u % (jedna decimala) u odnosu na prethodnu vrijednost; null kad prethodne nema. */
export function promjena(sada: number, prije: number): number | null {
  if (!prije) return null;
  return Math.round(((sada - prije) / Math.abs(prije)) * 1000) / 10;
}

/** Udio 0–1 kao „61,9%"; prazno je „—". */
export function procenat(x: number | null | undefined): string {
  if (x === null || x === undefined) return "—";
  return `${(x * 100).toLocaleString("sr-Latn", { maximumFractionDigits: 1 })}%`;
}

export function susjedniMjesec(godina: number, mjesec: number, pomak: number): { godina: number; mjesec: number } {
  const i = godina * 12 + (mjesec - 1) + pomak;
  return { godina: Math.floor(i / 12), mjesec: (i % 12) + 1 };
}

/** Najmanji broj oblika 1, 2 ili 5 × 10^n koji je ≥ x (za vrh ose). */
export function zaokruziGore(x: number): number {
  if (x <= 0) return 1;
  const red = 10 ** Math.floor(Math.log10(x));
  for (const k of [1, 2, 5, 10]) if (k * red >= x) return k * red;
  return 10 * red;
}

/** Vrh ose i pet podjela (0, ¼, ½, ¾, vrh) za stubičasti grafikon. */
export function skalaGrafikona(vrijednosti: number[]): { max: number; podjele: number[] } {
  const max = zaokruziGore(Math.max(0, ...vrijednosti));
  return { max, podjele: [0, 1, 2, 3, 4].map((i) => (max * i) / 4) };
}
```

- [ ] **Step 4: Types**

Na kraj `contracts.ts`:

```ts
/* ---------- Poslovanje (admin analitika) ---------- */

export interface PrihodPoslovanja {
  prometRobe: number;
  dostava: number;
  promoPopust: number;
  poeniPopust: number;
  neto: number;
  nabavno: number;
  zarada: number;
  marza: number | null;
  brojNarudzbi: number;
  prosjecnaNarudzba: number;
  udioKorpica: number;
  udioSpajza: number;
  procjena: boolean;
  bezNabavneCijene: number;
}

export interface KorpicaProdaje { naziv: string; tip: string; velicina: string; komada: number; prihod: number; nabavno: number; marza: number | null }
export interface KategorijaProdaje { kategorija: string; prihod: number; nabavno: number; marza: number | null }
export interface ArtikalProdaje { naziv: string; jedinica: string; kolicina: number; prihod: number; zarada: number }
export interface KupacAnalitike { ime: string; gost: boolean; narudzbi: number; neto: number }
export interface PlanAnalitike { plan: string; aktivne: number }
export interface ZonaAnalitike { zona: string; narudzbi: number; neto: number }
export interface ArtikalGazdinstva { naziv: string; jedinica: string; kolicina: number; nabavno: number }
export interface GazdinstvoAnalitike { naziv: string; selo: string; nabavno: number; artikli: ArtikalGazdinstva[] }

export interface PoslovanjeMjeseca {
  godina: number;
  mjesec: number;
  prihod: PrihodPoslovanja;
  prethodni: PrihodPoslovanja;
  prodaja: {
    korpice: KorpicaProdaje[];
    spajz: KategorijaProdaje[];
    topArtikli: ArtikalProdaje[];
    najslabijiArtikli: ArtikalProdaje[];
  };
  kupci: {
    aktivni: number;
    aktivniPrethodni: number;
    novi: number;
    povratni: number;
    zadrzavanje: number | null;
    potrosnjaPoKupcu: number;
    narudzbiPoKupcu: number;
    gosti: number;
    registrovani: number;
    noviPrekoPreporuke: number;
    topKupci: KupacAnalitike[];
  };
  pretplate: {
    aktivneNaKraju: number;
    nove: number;
    otkazane: number;
    stopaOtkazivanja: number | null;
    pauzirane: number | null;
    poPlanu: PlanAnalitike[];
    udioPrihoda: number;
  };
  dostava: {
    poZoni: ZonaAnalitike[];
    isporucene: number;
    neuspjele: number;
    uspjesnost: number | null;
    otkazane: number;
    otkazaneVrijednost: number;
    otkazaneUdio: number | null;
    prosjecnaOcjena: number | null;
    brojOcjena: number;
  };
  gazdinstva: GazdinstvoAnalitike[];
}

export interface MjesecGodine {
  mjesec: number;
  neto: number;
  zarada: number;
  marza: number | null;
  narudzbi: number;
  aktivniKupci: number;
  aktivnePretplate: number;
  procjena: boolean;
}

export interface PoslovanjeGodine {
  godina: number;
  mjeseci: MjesecGodine[];
  sezona: { kategorija: string; mjeseci: number[] }[];
}
```

- [ ] **Step 5: API calls**

U `adminApi` u `endpoints.ts`, odmah poslije `dashboard`:

```ts
  poslovanje: (godina?: number, mjesec?: number) =>
    apiFetch<PoslovanjeMjeseca>("/admin/poslovanje", { query: godina && mjesec ? { godina, mjesec } : {} }).then((r) => r.data),
  poslovanjeGodina: (godina?: number) =>
    apiFetch<PoslovanjeGodine>("/admin/poslovanje/godina", { query: godina ? { godina } : {} }).then((r) => r.data),
```

i dodaj `PoslovanjeMjeseca, PoslovanjeGodine` u postojeći `import type { ... } from "./contracts"` na vrhu.

- [ ] **Step 6: Menu item**

U `admin-nav.tsx`: dodaj `ChartColumn` u import iz `lucide-react`, pa:

```tsx
const POSLOVANJE: NavItem = { href: "/admin/poslovanje", label: "Poslovanje", icon: ChartColumn };
```

ispod `PREGLED`; `ITEMS` postaje `[PREGLED, POSLOVANJE, ...GRUPE.flatMap((g) => g.stavke)]`; ispod reda ~369 (`<StavkaMenija item={PREGLED} ... />`) dodaj isti red za `POSLOVANJE`.

- [ ] **Step 7: Run tests and typecheck**

Run: `cd frontend && npm test 2>&1 | grep -E "ℹ (pass|fail)" && npx tsc --noEmit`
Expected: `fail 0`, tsc bez izlaza.

- [ ] **Step 8: Commit**

```bash
git add frontend/src
git commit -m "feat(poslovanje): frontend tipovi, API, pomoćne funkcije i stavka menija"
```

---

### Task 8: Frontend — mjesečna stranica

**Files:**
- Create: `frontend/src/components/admin/poslovanje/kartica-poslovanja.tsx`
- Create: `frontend/src/components/admin/poslovanje/odjeljak.tsx`
- Create: `frontend/src/components/admin/poslovanje/tabela.tsx`
- Create: `frontend/src/app/admin/poslovanje/page.tsx`

**Interfaces:**
- Consumes: `adminApi.poslovanje`, `PoslovanjeMjeseca`, `promjena`, `procenat`, `MJESECI`, `susjedniMjesec` (Task 7), `formatPrice` (`@/lib/utils`).
- Produces: `KarticaPoslovanja({ label, vrijednost, sada, prije, obrnuto? })`, `Odjeljak({ naslov, opis?, children, otvoren? })`, `Tabela({ kolone, redovi })` — koristi ih i Task 9.

- [ ] **Step 1: Components**

`kartica-poslovanja.tsx`:

```tsx
import { promjena } from "@/lib/admin/poslovanje";
import { cn } from "@/lib/utils";

/** Broj mjeseca sa ▲/▼ % prema prethodnom. `obrnuto`: rast je loš (npr. otkazane). */
export function KarticaPoslovanja({
  label, vrijednost, sada, prije, obrnuto = false, hint,
}: { label: string; vrijednost: string; sada?: number; prije?: number; obrnuto?: boolean; hint?: string }) {
  const p = sada === undefined || prije === undefined ? null : promjena(sada, prije);
  const dobro = p !== null && (obrnuto ? p < 0 : p > 0);
  return (
    <div className="rounded-card bg-white p-5 shadow-card print:shadow-none print:border print:border-line">
      <p className="text-sm font-medium text-muted">{label}</p>
      <p className="mt-2 font-display text-2xl font-bold tabular-nums">{vrijednost}</p>
      {p !== null ? (
        <p className={cn("mt-1 text-xs font-semibold tabular-nums", p === 0 ? "text-muted" : dobro ? "text-brand-700" : "text-red-700")}>
          {p > 0 ? "▲" : p < 0 ? "▼" : "="} {Math.abs(p).toLocaleString("sr-Latn")}% od prošlog mjeseca
        </p>
      ) : (
        hint && <p className="mt-1 text-xs text-muted">{hint}</p>
      )}
    </div>
  );
}
```

`odjeljak.tsx`:

```tsx
/** Sklopivi odjeljak analitike; pri štampi je uvijek otvoren. */
export function Odjeljak({
  naslov, opis, children, otvoren = true,
}: { naslov: string; opis?: string; children: React.ReactNode; otvoren?: boolean }) {
  return (
    <details open={otvoren} className="group rounded-card bg-white shadow-card print:shadow-none [&[open]>summary_span]:rotate-90">
      <summary className="flex cursor-pointer list-none items-baseline justify-between gap-3 px-5 py-4">
        <h2 className="font-display text-lg font-bold text-ink">{naslov}</h2>
        <span className="text-muted transition-transform print:hidden">›</span>
      </summary>
      <div className="space-y-4 border-t border-line px-5 py-4">
        {opis && <p className="text-sm text-muted">{opis}</p>}
        {children}
      </div>
    </details>
  );
}
```

`tabela.tsx`:

```tsx
import { cn } from "@/lib/utils";

export interface Kolona<T> {
  naslov: string;
  vrijednost: (r: T) => React.ReactNode;
  desno?: boolean;
}

/** Jednostavna tabela; prazna lista daje red „Nema podataka za ovaj mjesec." */
export function Tabela<T>({ kolone, redovi, kljuc }: { kolone: Kolona<T>[]; redovi: T[]; kljuc: (r: T, i: number) => string }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[28rem] text-sm">
        <thead className="text-left text-xs text-muted">
          <tr className="border-b border-line">
            {kolone.map((k) => (
              <th key={k.naslov} className={cn("p-2 font-medium", k.desno && "text-right")}>{k.naslov}</th>
            ))}
          </tr>
        </thead>
        <tbody className="tabular-nums">
          {redovi.length === 0 ? (
            <tr><td colSpan={kolone.length} className="p-3 text-center text-muted">Nema podataka za ovaj mjesec.</td></tr>
          ) : (
            redovi.map((r, i) => (
              <tr key={kljuc(r, i)} className="break-inside-avoid border-b border-line last:border-0">
                {kolone.map((k) => (
                  <td key={k.naslov} className={cn("p-2", k.desno && "text-right")}>{k.vrijednost(r)}</td>
                ))}
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}
```

- [ ] **Step 2: Page**

`frontend/src/app/admin/poslovanje/page.tsx`:

```tsx
"use client";

import { Suspense } from "react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { CalendarRange, ChevronLeft, ChevronRight, Printer } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { KarticaPoslovanja } from "@/components/admin/poslovanje/kartica-poslovanja";
import { Odjeljak } from "@/components/admin/poslovanje/odjeljak";
import { Tabela } from "@/components/admin/poslovanje/tabela";
import { PdfPoslovanjaDugme } from "@/components/admin/poslovanje/pdf-dugme";
import { adminApi } from "@/lib/api/endpoints";
import { MJESECI, procenat, susjedniMjesec } from "@/lib/admin/poslovanje";
import { formatPrice } from "@/lib/utils";

const broj = (x: number) => Number(x.toFixed(2)).toLocaleString("sr-Latn");
const PLANOVI: Record<string, string> = { Nedjeljna: "Nedjeljna", Mjesecna: "Mjesečna", TriMjeseca: "3 mjeseca" };

/**
 * Analitika poslovanja za mjesec (po datumu isporuke). Bez ?godina=&mjesec= otvara tekući mjesec
 * — server ga bira po Sarajevu, pa URL dobija oba parametra tek na prvi pomak strelicom.
 */
export default function AdminPoslovanjePage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Poslovanje />
    </Suspense>
  );
}

function Poslovanje() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const g = Number(params.get("godina")) || undefined;
  const m = Number(params.get("mjesec")) || undefined;

  const q = useQuery({
    queryKey: ["admin", "poslovanje", g ?? "tekuci", m ?? "tekuci"],
    queryFn: () => adminApi.poslovanje(g, m),
  });
  const d = q.data;

  function idi(pomak: number) {
    if (!d) return;
    const s = susjedniMjesec(d.godina, d.mjesec, pomak);
    router.replace(`${pathname}?godina=${s.godina}&mjesec=${s.mjesec}`, { scroll: false });
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">
            Poslovanje{d ? ` — ${MJESECI[d.mjesec - 1]} ${d.godina}.` : ""}
          </h1>
          <p className="text-muted">Mjesec po datumu isporuke; prihod samo od isporučenih narudžbi.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2 print:hidden">
          <Button variant="secondary" size="sm" onClick={() => idi(-1)} disabled={!d} aria-label="Prethodni mjesec">
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button variant="secondary" size="sm" onClick={() => idi(1)} disabled={!d} aria-label="Sljedeći mjesec">
            <ChevronRight className="h-4 w-4" />
          </Button>
          <Link href={`/admin/poslovanje/godina${d ? `?godina=${d.godina}` : ""}`}>
            <Button variant="secondary" size="sm"><CalendarRange className="h-4 w-4" /> Godina</Button>
          </Link>
          <Button variant="secondary" size="sm" onClick={() => window.print()} disabled={!d}>
            <Printer className="h-4 w-4" /> Štampaj
          </Button>
          {d && <PdfPoslovanjaDugme d={d} />}
        </div>
      </div>

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState
          title="Ne mogu da učitam analitiku"
          description="Provjeri konekciju pa pokušaj ponovo."
          action={<Button onClick={() => q.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : (
        <>
          {d.prihod.procjena && (
            <p className="rounded-card bg-honey-100 px-4 py-3 text-sm font-medium text-honey-700">
              Procjena: dio narudžbi je stariji od pamćenja nabavne cijene, pa je zarada računata po današnjim nabavnim cijenama.
            </p>
          )}
          {d.prihod.bezNabavneCijene > 0 && (
            <p className="rounded-card bg-red-50 px-4 py-3 text-sm font-medium text-red-700">
              {d.prihod.bezNabavneCijene} stavki nema nabavnu cijenu — nisu u trošku, pa je zarada prikazana veća nego što jeste.
            </p>
          )}

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-5">
            <KarticaPoslovanja label="Neto prihod" vrijednost={formatPrice(d.prihod.neto)} sada={d.prihod.neto} prije={d.prethodni.neto} />
            <KarticaPoslovanja
              label="Zarada"
              vrijednost={`${formatPrice(d.prihod.zarada)} · ${procenat(d.prihod.marza)}`}
              sada={d.prihod.zarada}
              prije={d.prethodni.zarada}
            />
            <KarticaPoslovanja label="Narudžbe" vrijednost={String(d.prihod.brojNarudzbi)} sada={d.prihod.brojNarudzbi} prije={d.prethodni.brojNarudzbi} />
            <KarticaPoslovanja
              label="Prosječna narudžba"
              vrijednost={formatPrice(d.prihod.prosjecnaNarudzba)}
              sada={d.prihod.prosjecnaNarudzba}
              prije={d.prethodni.prosjecnaNarudzba}
            />
            <KarticaPoslovanja label="Aktivni kupci" vrijednost={String(d.kupci.aktivni)} sada={d.kupci.aktivni} prije={d.kupci.aktivniPrethodni} />
          </div>

          <Odjeljak naslov="Prihod i zarada">
            <Tabela
              kljuc={(r) => r[0]}
              kolone={[
                { naslov: "Stavka", vrijednost: (r: [string, string]) => r[0] },
                { naslov: "Iznos", vrijednost: (r: [string, string]) => r[1], desno: true },
              ]}
              redovi={[
                ["Promet robe", formatPrice(d.prihod.prometRobe)],
                ["Dostava naplaćena", formatPrice(d.prihod.dostava)],
                ["Promo popusti", `− ${formatPrice(d.prihod.promoPopust)}`],
                ["Popust od poena", `− ${formatPrice(d.prihod.poeniPopust)}`],
                ["Neto prihod", formatPrice(d.prihod.neto)],
                ["Nabavni trošak robe", formatPrice(d.prihod.nabavno)],
                ["Zarada na robi", `${formatPrice(d.prihod.zarada)} (${procenat(d.prihod.marza)})`],
                ["Udio korpica / Špajza u prometu", `${procenat(d.prihod.udioKorpica)} / ${procenat(d.prihod.udioSpajza)}`],
              ]}
            />
          </Odjeljak>

          <Odjeljak naslov="Šta se prodaje">
            <h3 className="font-semibold">Korpice</h3>
            <Tabela
              kljuc={(r) => r.naziv}
              redovi={d.prodaja.korpice}
              kolone={[
                { naslov: "Korpica", vrijednost: (r) => r.naziv },
                { naslov: "Komada", vrijednost: (r) => broj(r.komada), desno: true },
                { naslov: "Prihod", vrijednost: (r) => formatPrice(r.prihod), desno: true },
                { naslov: "Nabavno", vrijednost: (r) => formatPrice(r.nabavno), desno: true },
                { naslov: "Marža", vrijednost: (r) => procenat(r.marza), desno: true },
              ]}
            />
            <h3 className="font-semibold">Špajz po kategoriji</h3>
            <Tabela
              kljuc={(r) => r.kategorija}
              redovi={d.prodaja.spajz}
              kolone={[
                { naslov: "Kategorija", vrijednost: (r) => r.kategorija },
                { naslov: "Prihod", vrijednost: (r) => formatPrice(r.prihod), desno: true },
                { naslov: "Nabavno", vrijednost: (r) => formatPrice(r.nabavno), desno: true },
                { naslov: "Marža", vrijednost: (r) => procenat(r.marza), desno: true },
              ]}
            />
            <div className="grid gap-6 lg:grid-cols-2">
              {([["Top 10 artikala", d.prodaja.topArtikli], ["Najslabijih 10", d.prodaja.najslabijiArtikli]] as const).map(([naslov, lista]) => (
                <div key={naslov} className="space-y-2">
                  <h3 className="font-semibold">{naslov}</h3>
                  <Tabela
                    kljuc={(r) => `${r.naziv}|${r.jedinica}`}
                    redovi={[...lista]}
                    kolone={[
                      { naslov: "Artikal", vrijednost: (r) => r.naziv },
                      { naslov: "Količina", vrijednost: (r) => `${broj(r.kolicina)} ${r.jedinica}`, desno: true },
                      { naslov: "Prihod", vrijednost: (r) => formatPrice(r.prihod), desno: true },
                      { naslov: "Zarada", vrijednost: (r) => formatPrice(r.zarada), desno: true },
                    ]}
                  />
                </div>
              ))}
            </div>
            <p className="text-xs text-muted">Artikli iz korpica su uračunati po cijeni iz korpice, zajedno sa Špajzom.</p>
          </Odjeljak>

          <Odjeljak naslov="Kupci">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <KarticaPoslovanja label="Novi / povratni" vrijednost={`${d.kupci.novi} / ${d.kupci.povratni}`} />
              <KarticaPoslovanja label="Zadržavanje" vrijednost={procenat(d.kupci.zadrzavanje)} hint="Od kupaca prošlog mjeseca, naručili i ovog" />
              <KarticaPoslovanja label="Potrošnja po kupcu" vrijednost={formatPrice(d.kupci.potrosnjaPoKupcu)} hint={`${broj(d.kupci.narudzbiPoKupcu)} narudžbi po kupcu`} />
              <KarticaPoslovanja label="Registrovani / gosti" vrijednost={`${d.kupci.registrovani} / ${d.kupci.gosti}`} hint={`${d.kupci.noviPrekoPreporuke} novih preko preporuke`} />
            </div>
            <h3 className="font-semibold">Top kupci</h3>
            <Tabela
              kljuc={(r, i) => `${r.ime}|${i}`}
              redovi={d.kupci.topKupci}
              kolone={[
                { naslov: "Kupac", vrijednost: (r) => (r.gost ? `${r.ime} (gost)` : r.ime) },
                { naslov: "Narudžbi", vrijednost: (r) => r.narudzbi, desno: true },
                { naslov: "Potrošio", vrijednost: (r) => formatPrice(r.neto), desno: true },
              ]}
            />
          </Odjeljak>

          <Odjeljak naslov="Pretplate">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <KarticaPoslovanja label="Aktivne na kraju mjeseca" vrijednost={String(d.pretplate.aktivneNaKraju)} />
              <KarticaPoslovanja label="Nove / otkazane" vrijednost={`${d.pretplate.nove} / ${d.pretplate.otkazane}`} />
              <KarticaPoslovanja label="Stopa otkazivanja" vrijednost={procenat(d.pretplate.stopaOtkazivanja)} />
              <KarticaPoslovanja label="Udio prihoda iz pretplata" vrijednost={procenat(d.pretplate.udioPrihoda)} />
            </div>
            {d.pretplate.pauzirane !== null && <p className="text-sm text-muted">Trenutno pauzirano: {d.pretplate.pauzirane}.</p>}
            <Tabela
              kljuc={(r) => r.plan}
              redovi={d.pretplate.poPlanu}
              kolone={[
                { naslov: "Plan", vrijednost: (r) => PLANOVI[r.plan] ?? r.plan },
                { naslov: "Aktivne", vrijednost: (r) => r.aktivne, desno: true },
              ]}
            />
          </Odjeljak>

          <Odjeljak naslov="Dostava i kvalitet">
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <KarticaPoslovanja label="Uspješnost isporuke" vrijednost={procenat(d.dostava.uspjesnost)} hint={`${d.dostava.isporucene} isporučeno, ${d.dostava.neuspjele} neuspjelo`} />
              <KarticaPoslovanja label="Otkazane" vrijednost={`${d.dostava.otkazane} · ${procenat(d.dostava.otkazaneUdio)}`} hint={formatPrice(d.dostava.otkazaneVrijednost)} />
              <KarticaPoslovanja
                label="Ocjena isporuke"
                vrijednost={d.dostava.prosjecnaOcjena === null ? "—" : `${broj(d.dostava.prosjecnaOcjena)} / 5`}
                hint={`${d.dostava.brojOcjena} ocjena`}
              />
            </div>
            <Tabela
              kljuc={(r) => r.zona}
              redovi={d.dostava.poZoni}
              kolone={[
                { naslov: "Zona", vrijednost: (r) => r.zona },
                { naslov: "Narudžbi", vrijednost: (r) => r.narudzbi, desno: true },
                { naslov: "Neto", vrijednost: (r) => formatPrice(r.neto), desno: true },
              ]}
            />
          </Odjeljak>

          <Odjeljak naslov="Gazdinstva" opis="Koliko je robe uzeto od kog gazdinstva, po nabavnoj cijeni.">
            {d.gazdinstva.length === 0 ? (
              <p className="text-sm text-muted">Nema podataka za ovaj mjesec.</p>
            ) : (
              d.gazdinstva.map((g) => (
                <div key={`${g.naziv}|${g.selo}`} className="space-y-2">
                  <div className="flex items-baseline justify-between gap-3">
                    <h3 className="font-semibold">{g.naziv}{g.selo && <span className="font-normal text-muted"> · {g.selo}</span>}</h3>
                    <p className="font-display font-bold tabular-nums">{formatPrice(g.nabavno)}</p>
                  </div>
                  <Tabela
                    kljuc={(r) => `${r.naziv}|${r.jedinica}`}
                    redovi={g.artikli}
                    kolone={[
                      { naslov: "Artikal", vrijednost: (r) => r.naziv },
                      { naslov: "Količina", vrijednost: (r) => `${broj(r.kolicina)} ${r.jedinica}`, desno: true },
                      { naslov: "Nabavno", vrijednost: (r) => formatPrice(r.nabavno), desno: true },
                    ]}
                  />
                </div>
              ))
            )}
          </Odjeljak>
        </>
      )}
    </div>
  );
}
```

`PdfPoslovanjaDugme` pravi Task 10; do tada napravi `frontend/src/components/admin/poslovanje/pdf-dugme.tsx`:

```tsx
import type { PoslovanjeMjeseca } from "@/lib/api/contracts";

/** Privremeno prazno — Task 10 dodaje preuzimanje PDF-a. */
export function PdfPoslovanjaDugme({ d }: { d: PoslovanjeMjeseca }) {
  return d.mjesec ? null : null;
}
```

- [ ] **Step 3: Typecheck and lint**

Run: `cd frontend && npx tsc --noEmit && npx eslint src/app/admin/poslovanje src/components/admin/poslovanje`
Expected: bez grešaka.

- [ ] **Step 4: Look at it**

Pokreni (ili restartuj) „EPiljara web", otvori `http://localhost:3000/admin/poslovanje?godina=2026&mjesec=10` kao admin. Provjeri: kartice imaju brojeve i ▲/▼, traka „Procjena" se vidi, svih šest odjeljaka se otvara i zatvara, strelice mijenjaju mjesec (i preko januara), avgust 2026 prikazuje „Nema podataka" bez greške, konzola bez upozorenja o ključevima.

- [ ] **Step 5: Commit**

```bash
git add frontend/src
git commit -m "feat(poslovanje): mjesečna stranica analitike"
```

---

### Task 9: Frontend — godišnji pregled sa grafikonom

**Files:**
- Create: `frontend/src/components/admin/poslovanje/grafikon-godine.tsx`
- Create: `frontend/src/app/admin/poslovanje/godina/page.tsx`

**Interfaces:**
- Consumes: `adminApi.poslovanjeGodina`, `PoslovanjeGodine`, `skalaGrafikona`, `MJESECI`, `procenat`, `Tabela`, `Odjeljak` (Task 7–8).

- [ ] **Step 1: Chart**

`grafikon-godine.tsx`:

```tsx
import { MJESECI, skalaGrafikona } from "@/lib/admin/poslovanje";
import type { MjesecGodine } from "@/lib/api/contracts";

const V = 220; // visina crteža
const L = 48;  // prostor za oznake ose

/** Neto (svijetlo) i zarada (tamno) po mjesecima, ručno u SVG-u. */
export function GrafikonGodine({ mjeseci }: { mjeseci: MjesecGodine[] }) {
  const { max, podjele } = skalaGrafikona(mjeseci.flatMap((m) => [m.neto, m.zarada]));
  const sirina = 720;
  const korak = (sirina - L) / 12;
  const y = (x: number) => V - (Math.max(0, x) / max) * V;
  return (
    <figure className="space-y-2">
      <svg viewBox={`0 0 ${sirina} ${V + 24}`} className="w-full" role="img" aria-label="Neto prihod i zarada po mjesecima">
        {podjele.map((p) => (
          <g key={p}>
            <line x1={L} x2={sirina} y1={y(p)} y2={y(p)} className="stroke-line" strokeWidth={1} />
            <text x={L - 6} y={y(p) + 4} textAnchor="end" className="fill-muted text-[10px]">
              {p.toLocaleString("sr-Latn")}
            </text>
          </g>
        ))}
        {mjeseci.map((m, i) => {
          const x = L + i * korak + korak * 0.15;
          const s = korak * 0.32;
          return (
            <g key={m.mjesec}>
              <title>{`${MJESECI[i]}: neto ${m.neto.toLocaleString("sr-Latn")} KM, zarada ${m.zarada.toLocaleString("sr-Latn")} KM`}</title>
              <rect x={x} y={y(m.neto)} width={s} height={V - y(m.neto)} rx={2} className="fill-brand-300" />
              <rect x={x + s + 2} y={y(m.zarada)} width={s} height={V - y(m.zarada)} rx={2} className="fill-brand-700" />
              <text x={x + s} y={V + 16} textAnchor="middle" className="fill-muted text-[10px]">
                {MJESECI[i].slice(0, 3)}
              </text>
            </g>
          );
        })}
      </svg>
      <figcaption className="flex gap-4 text-xs text-muted">
        <span><span className="mr-1 inline-block h-2.5 w-2.5 rounded-sm bg-brand-300" />Neto prihod (KM)</span>
        <span><span className="mr-1 inline-block h-2.5 w-2.5 rounded-sm bg-brand-700" />Zarada (KM)</span>
      </figcaption>
    </figure>
  );
}
```

- [ ] **Step 2: Page**

`frontend/src/app/admin/poslovanje/godina/page.tsx`:

```tsx
"use client";

import { Suspense } from "react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ChevronLeft, ChevronRight, Printer } from "lucide-react";
import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { LoadingBlock } from "@/components/ui/spinner";
import { GrafikonGodine } from "@/components/admin/poslovanje/grafikon-godine";
import { Odjeljak } from "@/components/admin/poslovanje/odjeljak";
import { Tabela } from "@/components/admin/poslovanje/tabela";
import { adminApi } from "@/lib/api/endpoints";
import { MJESECI, procenat } from "@/lib/admin/poslovanje";
import { formatPrice } from "@/lib/utils";

export default function AdminPoslovanjeGodinaPage() {
  return (
    <Suspense fallback={<LoadingBlock />}>
      <Godina />
    </Suspense>
  );
}

function Godina() {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();
  const g = Number(params.get("godina")) || undefined;
  const q = useQuery({
    queryKey: ["admin", "poslovanje", "godina", g ?? "tekuca"],
    queryFn: () => adminApi.poslovanjeGodina(g),
  });
  const d = q.data;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-bold">Poslovanje{d ? ` — ${d.godina}.` : ""}</h1>
          <p className="text-muted">Pregled godine po mjesecima isporuke.</p>
        </div>
        <div className="flex flex-wrap items-center gap-2 print:hidden">
          <Button variant="secondary" size="sm" disabled={!d} aria-label="Prethodna godina"
            onClick={() => d && router.replace(`${pathname}?godina=${d.godina - 1}`, { scroll: false })}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button variant="secondary" size="sm" disabled={!d} aria-label="Sljedeća godina"
            onClick={() => d && router.replace(`${pathname}?godina=${d.godina + 1}`, { scroll: false })}>
            <ChevronRight className="h-4 w-4" />
          </Button>
          <Button variant="secondary" size="sm" onClick={() => window.print()} disabled={!d}>
            <Printer className="h-4 w-4" /> Štampaj
          </Button>
        </div>
      </div>

      {q.isLoading ? (
        <LoadingBlock />
      ) : q.isError || !d ? (
        <EmptyState
          title="Ne mogu da učitam godišnji pregled"
          description="Provjeri konekciju pa pokušaj ponovo."
          action={<Button onClick={() => q.refetch()}>Pokušaj ponovo</Button>}
        />
      ) : (
        <>
          <div className="rounded-card bg-white p-5 shadow-card print:shadow-none">
            <GrafikonGodine mjeseci={d.mjeseci} />
          </div>

          <Odjeljak naslov="Po mjesecima">
            <Tabela
              kljuc={(r) => String(r.mjesec)}
              redovi={d.mjeseci}
              kolone={[
                {
                  naslov: "Mjesec",
                  vrijednost: (r) => (
                    <Link className="underline-offset-2 hover:underline" href={`/admin/poslovanje?godina=${d.godina}&mjesec=${r.mjesec}`}>
                      {MJESECI[r.mjesec - 1]}{r.procjena ? " *" : ""}
                    </Link>
                  ),
                },
                { naslov: "Neto", vrijednost: (r) => formatPrice(r.neto), desno: true },
                { naslov: "Zarada", vrijednost: (r) => formatPrice(r.zarada), desno: true },
                { naslov: "Marža", vrijednost: (r) => procenat(r.marza), desno: true },
                { naslov: "Narudžbi", vrijednost: (r) => r.narudzbi, desno: true },
                { naslov: "Kupaca", vrijednost: (r) => r.aktivniKupci, desno: true },
                { naslov: "Pretplata", vrijednost: (r) => r.aktivnePretplate, desno: true },
              ]}
            />
            {d.mjeseci.some((m) => m.procjena) && (
              <p className="text-xs text-muted">* procjena — zarada po današnjim nabavnim cijenama.</p>
            )}
          </Odjeljak>

          <Odjeljak naslov="Sezona po kategoriji" opis="Prihod po kategoriji robe, svaki mjesec (Špajz i sadržaj korpica zajedno).">
            <Tabela
              kljuc={(r) => r.kategorija}
              redovi={d.sezona}
              kolone={[
                { naslov: "Kategorija", vrijednost: (r) => r.kategorija },
                ...MJESECI.map((ime, i) => ({
                  naslov: ime.slice(0, 3),
                  vrijednost: (r: (typeof d.sezona)[number]) =>
                    r.mjeseci[i] ? Math.round(r.mjeseci[i]).toLocaleString("sr-Latn") : "—",
                  desno: true,
                })),
              ]}
            />
          </Odjeljak>
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Typecheck, lint, look**

Run: `cd frontend && npx tsc --noEmit && npx eslint src/app/admin/poslovanje src/components/admin/poslovanje`
Expected: bez grešaka.
Otvori `http://localhost:3000/admin/poslovanje/godina?godina=2026`: grafikon ima stubove za septembar–novembar, klik na mjesec vodi na mjesečnu stranicu, 2025 prikazuje prazan grafikon i „—" bez greške.

- [ ] **Step 4: Commit**

```bash
git add frontend/src
git commit -m "feat(poslovanje): godišnji pregled sa grafikonom i sezonom po kategoriji"
```

---

### Task 10: PDF mjesečnog pregleda i završna provjera

**Files:**
- Create: `frontend/src/lib/admin/poslovanje-pdf.ts`
- Modify: `frontend/src/components/admin/poslovanje/pdf-dugme.tsx`

**Interfaces:**
- Consumes: `PoslovanjeMjeseca`, `MJESECI`, `procenat`.
- Produces: `dokumentPoslovanja(d: PoslovanjeMjeseca): TDocumentDefinitions`, `preuzmiPdfPoslovanja(d): Promise<void>`, `PdfPoslovanjaDugme({ d })`.

- [ ] **Step 1: PDF document**

`frontend/src/lib/admin/poslovanje-pdf.ts`:

```ts
import type { Content, TableCell, TDocumentDefinitions } from "pdfmake/interfaces";
import type { PoslovanjeMjeseca } from "@/lib/api/contracts";
import { MJESECI, procenat } from "./poslovanje";

const km = (x: number) => `${x.toLocaleString("sr-Latn", { minimumFractionDigits: 2, maximumFractionDigits: 2 })} KM`;
const br = (x: number) => Number(x.toFixed(2)).toLocaleString("sr-Latn");
const z = (t: string, desno = false): TableCell => ({ text: t, bold: true, fontSize: 9, color: "#555555", alignment: desno ? "right" : "left" });

function tabela(naslov: string, zaglavlje: TableCell[], redovi: TableCell[][], sirine: (string | number)[]): Content[] {
  return [
    { text: naslov, fontSize: 13, bold: true, color: "#4b7a3a", margin: [0, 12, 0, 4] },
    redovi.length === 0
      ? { text: "Nema podataka za ovaj mjesec.", color: "#888888", fontSize: 10 }
      : {
          table: { headerRows: 1, dontBreakRows: true, widths: sirine, body: [zaglavlje, ...redovi] },
          layout: "lightHorizontalLines",
          fontSize: 10,
        },
  ];
}

/** Mjesečni pregled poslovanja kao PDF — interni dokument (ima nabavne cijene). */
export function dokumentPoslovanja(d: PoslovanjeMjeseca): TDocumentDefinitions {
  const p = d.prihod;
  const d2 = (t: string) => ({ text: t, alignment: "right" as const });
  return {
    pageSize: "A4",
    pageMargins: [40, 48, 40, 48],
    info: { title: `Poslovanje — ${MJESECI[d.mjesec - 1]} ${d.godina}` },
    defaultStyle: { font: "Roboto", fontSize: 11 },
    content: [
      { text: "E piljara", fontSize: 10, color: "#4b7a3a", bold: true },
      { text: `Poslovanje — ${MJESECI[d.mjesec - 1]} ${d.godina}.`, fontSize: 20, bold: true, margin: [0, 2, 0, 2] },
      { text: "Po datumu isporuke; prihod samo od isporučenih narudžbi. Interno — sadrži nabavne cijene.", fontSize: 9, color: "#555555" },
      ...(p.procjena ? [{ text: "Procjena: dio zarade računat po današnjim nabavnim cijenama.", fontSize: 9, color: "#a15c00" } as Content] : []),
      ...tabela("Prihod i zarada", [z("Stavka"), z("Iznos", true)], [
        ["Promet robe", d2(km(p.prometRobe))],
        ["Dostava naplaćena", d2(km(p.dostava))],
        ["Promo popusti", d2(`− ${km(p.promoPopust)}`)],
        ["Popust od poena", d2(`− ${km(p.poeniPopust)}`)],
        [{ text: "Neto prihod", bold: true }, { text: km(p.neto), bold: true, alignment: "right" }],
        ["Nabavni trošak robe", d2(km(p.nabavno))],
        [{ text: "Zarada na robi", bold: true }, { text: `${km(p.zarada)} (${procenat(p.marza)})`, bold: true, alignment: "right" }],
        ["Narudžbi / prosječna", d2(`${p.brojNarudzbi} / ${km(p.prosjecnaNarudzba)}`)],
      ], ["*", 160]),
      ...tabela("Korpice", [z("Korpica"), z("Kom.", true), z("Prihod", true), z("Marža", true)],
        d.prodaja.korpice.map((k) => [k.naziv, d2(br(k.komada)), d2(km(k.prihod)), d2(procenat(k.marza))]), ["*", 40, 80, 50]),
      ...tabela("Špajz po kategoriji", [z("Kategorija"), z("Prihod", true), z("Marža", true)],
        d.prodaja.spajz.map((k) => [k.kategorija, d2(km(k.prihod)), d2(procenat(k.marza))]), ["*", 80, 50]),
      ...tabela("Top 10 artikala", [z("Artikal"), z("Količina", true), z("Prihod", true), z("Zarada", true)],
        d.prodaja.topArtikli.map((a) => [a.naziv, d2(`${br(a.kolicina)} ${a.jedinica}`), d2(km(a.prihod)), d2(km(a.zarada))]), ["*", 70, 70, 70]),
      ...tabela("Kupci", [z("Pokazatelj"), z("Vrijednost", true)], [
        ["Aktivni (novi / povratni)", d2(`${d.kupci.aktivni} (${d.kupci.novi} / ${d.kupci.povratni})`)],
        ["Zadržavanje", d2(procenat(d.kupci.zadrzavanje))],
        ["Potrošnja po kupcu", d2(km(d.kupci.potrosnjaPoKupcu))],
        ["Registrovani / gosti", d2(`${d.kupci.registrovani} / ${d.kupci.gosti}`)],
        ["Novi preko preporuke", d2(String(d.kupci.noviPrekoPreporuke))],
      ], ["*", 120]),
      ...tabela("Pretplate", [z("Pokazatelj"), z("Vrijednost", true)], [
        ["Aktivne na kraju mjeseca", d2(String(d.pretplate.aktivneNaKraju))],
        ["Nove / otkazane", d2(`${d.pretplate.nove} / ${d.pretplate.otkazane}`)],
        ["Stopa otkazivanja", d2(procenat(d.pretplate.stopaOtkazivanja))],
        ["Udio prihoda iz pretplata", d2(procenat(d.pretplate.udioPrihoda))],
      ], ["*", 120]),
      ...tabela("Dostava po zoni", [z("Zona"), z("Narudžbi", true), z("Neto", true)],
        d.dostava.poZoni.map((x) => [x.zona, d2(String(x.narudzbi)), d2(km(x.neto))]), ["*", 60, 80]),
      { text: `Uspješnost isporuke ${procenat(d.dostava.uspjesnost)} · otkazano ${d.dostava.otkazane} (${km(d.dostava.otkazaneVrijednost)}) · ocjena ${d.dostava.prosjecnaOcjena ?? "—"} (${d.dostava.brojOcjena})`, fontSize: 9, color: "#555555", margin: [0, 4, 0, 0] },
      ...tabela("Gazdinstva", [z("Gazdinstvo"), z("Selo"), z("Nabavno", true)],
        d.gazdinstva.map((g) => [g.naziv, g.selo, d2(km(g.nabavno))]), ["*", 100, 80]),
    ],
    footer: (strana: number, ukupno: number) => ({
      text: `${strana} / ${ukupno}`, alignment: "right", margin: [40, 16, 40, 0], fontSize: 8, color: "#888888",
    }),
  };
}

export async function preuzmiPdfPoslovanja(d: PoslovanjeMjeseca): Promise<void> {
  const [pdfMod, vfsMod] = await Promise.all([import("pdfmake/build/pdfmake"), import("pdfmake/build/vfs_fonts")]);
  const pdfMake = ((pdfMod as { default?: unknown }).default ?? pdfMod) as typeof import("pdfmake/build/pdfmake");
  const vfs = ((vfsMod as { default?: unknown }).default ?? vfsMod) as Parameters<typeof pdfMake.addVirtualFileSystem>[0];
  pdfMake.addVirtualFileSystem(vfs);
  await pdfMake.createPdf(dokumentPoslovanja(d)).download(`poslovanje-${d.godina}-${String(d.mjesec).padStart(2, "0")}.pdf`);
}
```

- [ ] **Step 2: Button**

Zamijeni `pdf-dugme.tsx`:

```tsx
"use client";

import { useState } from "react";
import { FileDown } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useToast } from "@/components/ui/toast";
import type { PoslovanjeMjeseca } from "@/lib/api/contracts";

/** PDF mjesečnog pregleda; pdfmake se učitava tek na klik. */
export function PdfPoslovanjaDugme({ d }: { d: PoslovanjeMjeseca }) {
  const [radi, setRadi] = useState(false);
  const { show } = useToast();
  async function preuzmi() {
    setRadi(true);
    try {
      const { preuzmiPdfPoslovanja } = await import("@/lib/admin/poslovanje-pdf");
      await preuzmiPdfPoslovanja(d);
    } catch {
      show("PDF nije napravljen — pokušaj ponovo.", "error");
    } finally {
      setRadi(false);
    }
  }
  return (
    <Button size="sm" onClick={preuzmi} disabled={radi}>
      <FileDown className="h-4 w-4" /> {radi ? "Pravim PDF…" : "Preuzmi PDF"}
    </Button>
  );
}
```

- [ ] **Step 3: Full verification**

Ugasi „EPiljara web" (build prepisuje `.next`). Pa:

Run: `cd frontend && npm test 2>&1 | grep -E "ℹ (pass|fail)" && npx tsc --noEmit && npm run build 2>&1 | tail -5`
Expected: `fail 0`, tsc bez izlaza, build „Compiled successfully" bez grešaka.

Run: `cd backend && dotnet test tests/EPiljara.Application.Tests`
Expected: sve prolazi.

Ponovo pokreni „EPiljara web" i „EPiljara API". Otvori `/admin/poslovanje?godina=2026&mjesec=10`, klikni „Preuzmi PDF" i otvori fajl: naslov sa mjesecom, sve tabele, č/ć/đ ispravni. Klikni „Štampaj": u pregledu štampe nema dugmadi ni menija.

- [ ] **Step 4: Commit**

```bash
git add frontend/src
git commit -m "feat(poslovanje): PDF mjesečnog pregleda"
```

- [ ] **Step 5: STANJE.md**

U `docs/superpowers/STANJE.md` dodaj red za „Poslovanje (analitika 1–6)" u stilu postojećih redova: gotovo, grana `poslovanje`, nije spojeno; odloženo: promocije i poeni, signali iz korpica. Commit: `git commit -am "docs: STANJE — analitika poslovanja"`.
