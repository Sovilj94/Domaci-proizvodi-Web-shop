# Naručivanje bez naloga — plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kupac bez naloga naručuje, a narudžba nastaje tek kad klikne link iz mejla u roku od 15 minuta; prijavljenom kupcu se ništa ne traži i samo dobija obavještenje sa terminom dostave.

**Architecture:** Zahtjev gosta se čuva u dvije nove tabele sa snimljenim cijenama i istekom; potvrda u jednoj transakciji pravi `Order`, briše zahtjev i skida zalihe. Pozadinski servis briše istekle. Svaka narudžba dobija trajni `PublicToken` za praćenje bez naloga. Frontend dobija kapiju prijava/registracija/gost, preuređenu formu i prošireni ekran zahvale.

**Tech Stack:** ASP.NET Core 9 (Clean Architecture, MediatR, FluentValidation, EF Core + PostgreSQL), Next.js 15 (TypeScript, Tailwind v4, TanStack Query), xUnit + FluentAssertions + SQLite in-memory.

**Spec:** `docs/superpowers/specs/2026-09-24-narucivanje-bez-naloga-design.md`

## Global Constraints

- Sav tekst vidljiv korisniku i sve poruke grešaka su **na srpskom**.
- Svaki endpoint vraća omotnicu `{ data, error, meta }` — koristi `ApiControllerBase` pomoćnike (`Envelope`, `EnvelopeCreated`, `EnvelopeList`), nikad ručno sklopljen odgovor.
- Greške se bacaju kao domenski izuzeci (`DomainException`→400, `NotFoundException`→404, `ConflictException`→409, `UnauthorizedException`→401); `ExceptionHandlingMiddleware` ih mapira.
- Ključevi validacionih grešaka su **camelCase pune putanje** (`Customer.Name` → `customer.name`). Frontend čita `err.fieldErrors["customer.name"]`.
- MediatR i FluentValidation se nalaze skeniranjem asemblija — **nema ručne DI registracije po handleru**.
- Enumi se čuvaju kao tekst (`HasConversion<string>()`).
- Migracije: `dotnet dotnet-ef migrations add <Ime> -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations`, iz `backend/`.
- Testovi se pokreću sa `dotnet test` iz `backend/`.
- Frontend nema test setup — provjera je `npx tsc --noEmit`, `npm run lint` i prolazak kroz tok uz pokrenut stek.
- Mock (`frontend/src/server/mock/`) se **ne dira**.
- `frontend/.env.local` mora imati `NEXT_PUBLIC_API_BASE_URL=http://localhost:5080`, inače front priča sa mockom i izmjene se ne vide.
- Mailpit za mejlove: `docker run -d --name mailpit -p 1025:1025 -p 8025:8025 axllent/mailpit`, sandučić na http://localhost:8025.

## Struktura fajlova

**Backend — novo:**
- `src/EPiljara.Domain/Entities/GuestOrderRequest.cs` — zahtjev gosta i njegove stavke
- `src/EPiljara.Application/Features/Orders/GuestOrderRequests.cs` — komande, validatori i handleri za zahtjev i potvrdu
- `src/EPiljara.Application/Features/Orders/OrderCode.cs` — generisanje broja narudžbe, dijeli ga `CreateOrder` i potvrda gosta
- `src/EPiljara.Infrastructure/Services/GuestOrderEmailTemplate.cs` — mejl sa linkom za potvrdu
- `src/EPiljara.Infrastructure/BackgroundJobs/ExpiredGuestRequestsCleaner.cs` — čistač
- `tests/EPiljara.Application.Tests/GuestOrderTests.cs` — testovi zahtjeva, potvrde i isteka
- `tests/EPiljara.Application.Tests/OrderTrackingTests.cs` — testovi trajnog tokena

**Backend — mijenja se:**
- `src/EPiljara.Domain/Entities/Order.cs` — `PublicToken`
- `src/EPiljara.Application/Common/Interfaces/Services.cs` — `IEmailService.SendGuestOrderRequestAsync`
- `src/EPiljara.Application/Common/Interfaces/IAppDbContext.cs` — dva nova `DbSet`-a
- `src/EPiljara.Application/DTOs/*` — `CustomerEmail` u `OrderDto`
- `src/EPiljara.Application/Features/Orders/CreateOrder.cs` — `PublicToken`, izdvojeno generisanje koda
- `src/EPiljara.Application/Features/Orders/OrderQueries.cs` — praćenje po tokenu
- `src/EPiljara.API/Controllers/OrdersController.cs` — tri nove akcije
- `src/EPiljara.API/Program.cs` — registracija čistača
- `src/EPiljara.Infrastructure/Persistence/AppDbContext.cs` — mapiranje novih entiteta
- `src/EPiljara.Infrastructure/Services/SmtpEmailService.cs`, `LoggingEmailService.cs` — novi mejl
- `src/EPiljara.Infrastructure/Services/OrderEmailTemplate.cs` — termin dostave i trajni link

**Frontend — novo:**
- `src/app/(shop)/checkout/nalog/page.tsx` — kapija sa tri izbora
- `src/app/(shop)/potvrda/[token]/page.tsx` — potvrda gosta
- `src/app/(shop)/narudzba/[token]/page.tsx` — praćenje narudžbe
- `src/components/shop/order-receipt.tsx` — ekran zahvale i praćenja, jedna komponenta
- `src/components/shop/check-email.tsx` — „provjeri mejl" sa odbrojavanjem

**Frontend — mijenja se:**
- `src/lib/api/contracts.ts`, `src/lib/api/endpoints.ts`, `src/types/domain.ts`
- `src/app/(shop)/cart/page.tsx` — vodi na kapiju
- `src/app/(shop)/checkout/page.tsx` — preuređena forma, grananje gost/prijavljen

---

### Task 1: Trajni token narudžbe

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/Order.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs`
- Create: `backend/src/EPiljara.Application/Features/Orders/OrderCode.cs`
- Modify: `backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs`
- Test: `backend/tests/EPiljara.Application.Tests/OrderTrackingTests.cs`

**Interfaces:**
- Consumes: ništa
- Produces: `Order.PublicToken` (string), `OrderCode.GenerateAsync(IAppDbContext db, CancellationToken ct) → Task<string>`

- [ ] **Step 1: Napiši test koji pada**

`backend/tests/EPiljara.Application.Tests/OrderTrackingTests.cs`:

```csharp
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace EPiljara.Application.Tests;

public class OrderTrackingTests
{
    [Fact]
    public async Task Nova_narudzba_dobija_javni_token()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);

        var rezultat = await scena.Naruci();

        var order = t.Db.Orders.Single(o => o.Id == rezultat.Id);
        order.PublicToken.Should().NotBeNullOrWhiteSpace();
        order.PublicToken.Should().HaveLength(32, "token je Guid bez crtica");
    }

    [Fact]
    public async Task Dvije_narudzbe_ne_dijele_token()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);

        var prva = await scena.Naruci();
        var druga = await scena.Naruci();

        var tokeni = t.Db.Orders
            .Where(o => o.Id == prva.Id || o.Id == druga.Id)
            .Select(o => o.PublicToken)
            .ToList();
        tokeni.Should().OnlyHaveUniqueItems();
    }
}
```

Pomoćna scena ide u isti fajl — pravi zonu, termin i proizvod, pa zove `CreateOrderHandler`:

```csharp
internal sealed class Scena
{
    private readonly TestDb _t;
    private readonly Guid _zoneId;
    private readonly Guid _slotId;
    private readonly Guid _productId;

    private Scena(TestDb t, Guid zoneId, Guid slotId, Guid productId)
    {
        _t = t; _zoneId = zoneId; _slotId = slotId; _productId = productId;
    }

    public static Task<Scena> NapraviAsync(TestDb t, decimal minimalniIznos = 10m)
    {
        // Product ima obavezne CategoryId i ProducerId, a TestDb ima uključene
        // strane ključeve — bez ova dva reda upis proizvoda puca.
        var category = new Category { Name = "Med i zimnica", Slug = "med-i-zimnica" };
        var producer = new Producer { Name = "Porodica Kovačević", Village = "Han Kola" };
        var zone = new DeliveryZone { Name = "Centar", MinOrderAmount = minimalniIznos, DeliveryFee = 3m, IsActive = true };
        var slot = new DeliverySlot { DayOfWeek = 4, TimeFrom = "16:00", TimeTo = "20:00", Label = "Četvrtak 16–20h" };
        t.Db.Categories.Add(category);
        t.Db.Producers.Add(producer);
        t.Db.DeliveryZones.Add(zone);
        t.Db.DeliverySlots.Add(slot);
        t.Db.SaveChanges();

        var product = new Product
        {
            Name = "Med", Slug = "med", Price = 20m, Stock = 100,
            IsAvailable = true, Unit = Domain.Enums.Unit.Kom,
            CategoryId = category.Id, ProducerId = producer.Id,
        };
        t.Db.Products.Add(product);
        t.Db.SaveChanges();
        return Task.FromResult(new Scena(t, zone.Id, slot.Id, product.Id));
    }

    public Task<DTOs.OrderDto> Naruci() =>
        new CreateOrderHandler(_t.Db, new FakeCurrentUser(null), new FakeEmail(), new FakeViber())
            .Handle(Komanda(), default);

    public CreateOrderCommand Komanda() => new(
        new CustomerInput("Mujo Mujić", "mujo@test.local", "065 111 222"),
        _zoneId, _slotId, "Kralja Petra 12", null,
        [new OrderItemInput(_productId, 1)], 44.77, 17.19);
}
```

- [ ] **Step 2: Dodaj lažne servise za mejl i Viber**

`backend/tests/EPiljara.Application.Tests/FakeNotifications.cs`:

```csharp
using EPiljara.Application.Common.Interfaces;
using EPiljara.Domain.Entities;

namespace EPiljara.Application.Tests;

/// <summary>Bilježi šta bi bilo poslano, ne šalje ništa.</summary>
internal sealed class FakeEmail : IEmailService
{
    public List<Order> Narudzbe { get; } = [];
    public List<(GuestOrderRequest Zahtjev, string Token)> ZahtjeviGosta { get; } = [];

    public Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default)
    {
        Narudzbe.Add(order);
        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(User user, string token, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SendGuestOrderRequestAsync(GuestOrderRequest zahtjev, CancellationToken ct = default)
    {
        ZahtjeviGosta.Add((zahtjev, zahtjev.Token));
        return Task.CompletedTask;
    }
}

internal sealed class FakeViber : IViberService
{
    public Task SendOrderConfirmationAsync(Order order, CancellationToken ct = default) =>
        Task.CompletedTask;
}
```

Ako `IViberService` ima drugačiji potpis, uskladi ga sa `src/EPiljara.Application/Common/Interfaces/Services.cs` — potpis je izvor istine, ne ovaj plan.

- [ ] **Step 3: Pokreni test i vidi da pada**

Run: `dotnet test --filter OrderTrackingTests`
Expected: FAIL — `Order` nema `PublicToken`, `IEmailService` nema `SendGuestOrderRequestAsync`.

- [ ] **Step 4: Dodaj polje na entitet**

U `Order.cs`, ispod `Code`:

```csharp
    /// <summary>
    /// Trajni ključ za praćenje narudžbe bez naloga. Ne ističe i ne briše se —
    /// razlikuj ga od tokena zahtjeva gosta, koji živi 15 minuta i služi da
    /// narudžba uopšte nastane. Ide u mejl i nigdje drugdje.
    /// </summary>
    public string PublicToken { get; set; } = string.Empty;
```

- [ ] **Step 5: Dodaj metodu u IEmailService i prazne implementacije**

U `Services.cs`, u `IEmailService`:

```csharp
    /// <summary>Šalje gostu link za potvrdu narudžbe. Link se gradi iz tokena zahtjeva.</summary>
    Task SendGuestOrderRequestAsync(GuestOrderRequest request, CancellationToken cancellationToken = default);
```

U `LoggingEmailService.cs` i `SmtpEmailService.cs` dodaj privremene implementacije koje bacaju `NotImplementedException` — Task 5 ih popunjava. Bez ovoga projekat se ne kompajlira.

- [ ] **Step 6: Izdvoji generisanje broja narudžbe**

`backend/src/EPiljara.Application/Features/Orders/OrderCode.cs`:

```csharp
using EPiljara.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Orders;

/// <summary>
/// Broj narudžbe u obliku EP-yyMM-NNNN. Dijele ga obično naručivanje i potvrda
/// gosta — dvije kopije istog brojača bi prije ili kasnije dale isti broj.
/// </summary>
public static class OrderCode
{
    public static async Task<string> GenerateAsync(IAppDbContext db, CancellationToken ct)
    {
        var count = await db.Orders.CountAsync(ct);
        var now = DateTime.UtcNow;
        return $"EP-{now:yyMM}-{count + 41:D4}";
    }
}
```

- [ ] **Step 7: Upotrijebi oboje u CreateOrder**

U `CreateOrder.cs` zamijeni `Code = await GenerateCodeAsync(ct),` sa:

```csharp
            Code = await OrderCode.GenerateAsync(db, ct),
            PublicToken = Guid.NewGuid().ToString("N"),
```

Obriši privatnu `GenerateCodeAsync` metodu na dnu handlera.

- [ ] **Step 8: Mapiraj kolonu i napravi migraciju**

U `AppDbContext.cs`, u konfiguraciji `Order`:

```csharp
        b.Property(o => o.PublicToken).HasMaxLength(32).IsRequired();
        b.HasIndex(o => o.PublicToken).IsUnique();
```

Napravi migraciju:

```bash
dotnet dotnet-ef migrations add JavniTokenNarudzbe -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations
```

- [ ] **Step 9: Popuni postojeće narudžbe u migraciji**

Unique + not-null kolona ne može se dodati na tabelu sa redovima. U `Up()` generisane migracije, **između** `AddColumn` i `CreateIndex`, ubaci:

```csharp
            migrationBuilder.Sql(
                """UPDATE "Orders" SET "PublicToken" = replace(gen_random_uuid()::text, '-', '') WHERE "PublicToken" = '';""");
```

`gen_random_uuid()` je ugrađen u PostgreSQL 13+, a mi smo na 16 — nema potrebe za ekstenzijom.

- [ ] **Step 10: Pokreni testove**

Run: `dotnet test --filter OrderTrackingTests`
Expected: PASS, oba testa.

- [ ] **Step 11: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat: trajni token za praćenje narudžbe bez naloga"
```

---

### Task 2: Praćenje narudžbe po tokenu

**Files:**
- Modify: `backend/src/EPiljara.Application/DTOs/OrderDtos.cs` (fajl u kojem je `OrderDto`)
- Modify: `backend/src/EPiljara.Application/Mapping/MappingExtensions.cs`
- Modify: `backend/src/EPiljara.Application/Features/Orders/OrderQueries.cs`
- Modify: `backend/src/EPiljara.API/Controllers/OrdersController.cs`
- Test: `backend/tests/EPiljara.Application.Tests/OrderTrackingTests.cs`

**Interfaces:**
- Consumes: `Order.PublicToken` iz Taska 1
- Produces: `TrackOrderQuery(string Token) : IRequest<OrderDto>`, `GET /orders/track/{token}`, `OrderDto.CustomerEmail`

- [ ] **Step 1: Napiši testove koji padaju**

Dodaj u `OrderTrackingTests.cs`:

```csharp
    [Fact]
    public async Task Pracenje_po_tokenu_vraca_narudzbu()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var narucena = await scena.Naruci();
        var token = t.Db.Orders.Single(o => o.Id == narucena.Id).PublicToken;

        var rezultat = await new TrackOrderHandler(t.Db).Handle(new TrackOrderQuery(token), default);

        rezultat.Id.Should().Be(narucena.Id);
        rezultat.CustomerEmail.Should().Be("mujo@test.local");
        rezultat.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Pracenje_sa_pogresnim_tokenom_baca_not_found()
    {
        using var t = new TestDb();

        var radnja = () => new TrackOrderHandler(t.Db)
            .Handle(new TrackOrderQuery("nepostojeci"), default);

        await radnja.Should().ThrowAsync<NotFoundException>();
    }
```

Dodaj `using EPiljara.Domain.Common;` na vrh fajla zbog `NotFoundException`.

- [ ] **Step 2: Pokreni i vidi da pada**

Run: `dotnet test --filter OrderTrackingTests`
Expected: FAIL — `TrackOrderQuery` ne postoji.

- [ ] **Step 3: Dodaj CustomerEmail u DTO i mapiranje**

U `OrderDto` dodaj uz `CustomerPhone`:

```csharp
    /// <summary>Adresa na koju je poslana potvrda. Ekran zahvale je ispisuje.</summary>
    public string? CustomerEmail { get; init; }
```

U `MappingExtensions.cs`, u `ToDto(this Order o)`, dodaj `CustomerEmail = o.CustomerEmail,`.

`PublicToken` **ne ide** u DTO — admin spisak narudžbi ga inače nosi u pretraživač svakog ko otvori panel, a token je jedina zaštita praćenja.

- [ ] **Step 4: Dodaj upit**

Na dno `OrderQueries.cs`:

```csharp
/* ---------- Praćenje narudžbe bez naloga ---------- */

public record TrackOrderQuery(string Token) : IRequest<OrderDto>;

public class TrackOrderHandler(IAppDbContext db) : IRequestHandler<TrackOrderQuery, OrderDto>
{
    public async Task<OrderDto> Handle(TrackOrderQuery q, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.DeliverySlot)
            .Include(o => o.DeliveryZone)
            .FirstOrDefaultAsync(o => o.PublicToken == q.Token, ct)
            ?? throw new NotFoundException("Narudžba nije pronađena.");

        return order.ToDto();
    }
}
```

- [ ] **Step 5: Dodaj akciju u kontroler**

U `OrdersController.cs`:

```csharp
    /// <summary>Praćenje narudžbe bez naloga. Token je jedina zaštita, pa ide pod stroži limit.</summary>
    [HttpGet("orders/track/{token}")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Track(string token) =>
        Envelope(await Mediator.Send(new TrackOrderQuery(token)));
```

Dodaj `using Microsoft.AspNetCore.RateLimiting;` na vrh fajla.

- [ ] **Step 6: Pokreni testove**

Run: `dotnet test --filter OrderTrackingTests`
Expected: PASS, četiri testa.

- [ ] **Step 7: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat: praćenje narudžbe po trajnom tokenu"
```

---

### Task 3: Tabele zahtjeva gosta

**Files:**
- Create: `backend/src/EPiljara.Domain/Entities/GuestOrderRequest.cs`
- Modify: `backend/src/EPiljara.Application/Common/Interfaces/IAppDbContext.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs`

**Interfaces:**
- Consumes: ništa
- Produces: `GuestOrderRequest`, `GuestOrderRequestItem`, `IAppDbContext.GuestOrderRequests`, `IAppDbContext.GuestOrderRequestItems`

- [ ] **Step 1: Napravi entitete**

`backend/src/EPiljara.Domain/Entities/GuestOrderRequest.cs`:

```csharp
using EPiljara.Domain.Common;
using EPiljara.Domain.Enums;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Narudžba gosta koja još nije potvrđena. Ne postaje <see cref="Order"/> dok kupac
/// ne klikne link iz mejla, a to mora unutar <see cref="TrajanjeMinuta"/> minuta.
/// Poslije isteka se briše bez traga — zato ovdje stoje snimljene cijene, a ne
/// veza na aktuelni cjenovnik.
/// </summary>
public class GuestOrderRequest : Entity
{
    public const int TrajanjeMinuta = 15;

    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string CustomerName { get; set; } = string.Empty;
    /// <summary>Obavezan — bez njega nema gdje poslati link za potvrdu.</summary>
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;

    public Guid DeliveryZoneId { get; set; }
    public DeliveryZone? DeliveryZone { get; set; }
    public Guid DeliverySlotId { get; set; }
    public DeliverySlot? DeliverySlot { get; set; }

    public string DeliveryAddress { get; set; } = string.Empty;
    public string? Note { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }

    public ICollection<GuestOrderRequestItem> Items { get; set; } = new List<GuestOrderRequestItem>();

    public bool Istekao(DateTime sada) => ExpiresAt <= sada;
}

/// <summary>
/// Stavka zahtjeva sa snimljenom cijenom, nazivom i jedinicom — isto kao
/// <see cref="OrderItem"/>, samo snimljeno u trenutku slanja forme umjesto
/// u trenutku nastanka narudžbe. Kupac plaća ono što je vidio.
/// </summary>
public class GuestOrderRequestItem : Entity
{
    public Guid RequestId { get; set; }
    public GuestOrderRequest? Request { get; set; }

    /// <summary>Bez stranog ključa namjerno — brisanje proizvoda ne smije oboriti zahtjev prije isteka.</summary>
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Unit Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public string? ImageUrl { get; set; }
}
```

- [ ] **Step 2: Dodaj u IAppDbContext**

```csharp
    DbSet<GuestOrderRequest> GuestOrderRequests { get; }
    DbSet<GuestOrderRequestItem> GuestOrderRequestItems { get; }
```

- [ ] **Step 3: Mapiraj u AppDbContext**

Dodaj `DbSet`-ove i konfiguraciju u `OnModelCreating`:

```csharp
        modelBuilder.Entity<GuestOrderRequest>(b =>
        {
            b.Property(r => r.Token).HasMaxLength(32).IsRequired();
            b.HasIndex(r => r.Token).IsUnique();
            b.HasIndex(r => r.ExpiresAt);
            b.Property(r => r.CustomerEmail).IsRequired();
            b.Property(r => r.Subtotal).HasPrecision(10, 2);
            b.Property(r => r.DeliveryFee).HasPrecision(10, 2);
            b.Property(r => r.TotalAmount).HasPrecision(10, 2);
            b.HasMany(r => r.Items)
                .WithOne(i => i.Request!)
                .HasForeignKey(i => i.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GuestOrderRequestItem>(b =>
        {
            b.Property(i => i.Unit).HasConversion<string>();
            b.Property(i => i.UnitPrice).HasPrecision(10, 2);
            b.Property(i => i.Quantity).HasPrecision(10, 3);
        });
```

Indeks po `ExpiresAt` postoji zbog čistača — on svakih pet minuta pita „ko je istekao".

- [ ] **Step 4: Napravi migraciju**

```bash
dotnet dotnet-ef migrations add ZahtjeviGosta -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations
```

- [ ] **Step 5: Provjeri da se gradi i da testovi i dalje prolaze**

Run: `dotnet test`
Expected: PASS — sve od ranije.

- [ ] **Step 6: Commit**

```bash
git add backend/src
git commit -m "feat: tabele za nepotvrđenu narudžbu gosta"
```

---

### Task 4: Mejl sa linkom za potvrdu

**Files:**
- Create: `backend/src/EPiljara.Infrastructure/Services/GuestOrderEmailTemplate.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Services/SmtpEmailService.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Services/LoggingEmailService.cs`

**Interfaces:**
- Consumes: `GuestOrderRequest` iz Taska 3, `IEmailService.SendGuestOrderRequestAsync` iz Taska 1
- Produces: `GuestOrderEmailTemplate.Build(GuestOrderRequest request, string link) → string`

- [ ] **Step 1: Napravi šablon**

`backend/src/EPiljara.Infrastructure/Services/GuestOrderEmailTemplate.cs`. Prati oblik `ConfirmEmailTemplate.cs` i `OrderEmailTemplate.cs` — tabela sa inline stilovima, bez vanjskog CSS-a, jer mejl klijenti drugo ne podnose:

```csharp
using System.Globalization;
using System.Text;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;

namespace EPiljara.Infrastructure.Services;

/// <summary>
/// Mejl kojim gost potvrđuje narudžbu. Za razliku od OrderEmailTemplate, ovaj
/// opisuje narudžbu koja još NE postoji — nastaje tek klikom na link.
/// </summary>
public static class GuestOrderEmailTemplate
{
    private const string Green = "#2c7a4b";
    private const string Ink = "#1f2421";
    private const string Muted = "#7a8580";

    private static string Money(decimal v) =>
        "KM " + v.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    private static string Enc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

    public static string Build(GuestOrderRequest request, string link)
    {
        var stavke = new StringBuilder();
        foreach (var it in request.Items)
        {
            var kolicina = it.Quantity.ToString("0.##", CultureInfo.InvariantCulture) + " " + it.Unit.ToApi();
            stavke.Append($$"""
                <tr>
                  <td style="padding:8px 0;font:600 14px Arial,sans-serif;color:{{Ink}};width:72px;vertical-align:top">{{Enc(kolicina)}}</td>
                  <td style="padding:8px 0;font:400 14px Arial,sans-serif;color:{{Ink}}">{{Enc(it.ProductName)}}</td>
                  <td style="padding:8px 0;font:600 14px Arial,sans-serif;color:{{Ink}};text-align:right;white-space:nowrap">{{Money(it.UnitPrice * it.Quantity)}}</td>
                </tr>
                """);
        }

        return $$"""
            <!doctype html><html><body style="margin:0;background:#f6f7f5">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f6f7f5;padding:24px 0">
                <tr><td align="center">
                  <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="background:#fff;border-radius:12px;padding:32px">
                    <tr><td style="font:700 22px Arial,sans-serif;color:{{Ink}};padding-bottom:8px">Potvrdi svoju narudžbu</td></tr>
                    <tr><td style="font:400 15px Arial,sans-serif;color:{{Muted}};padding-bottom:20px">
                      Narudžba <strong>još nije poslana</strong> — nastaje tek kad klikneš dugme ispod.
                      Link vrijedi <strong>15 minuta</strong>.
                    </td></tr>
                    <tr><td style="padding-bottom:20px">
                      <a href="{{Enc(link)}}" style="display:inline-block;background:{{Green}};color:#fff;font:600 15px Arial,sans-serif;padding:12px 24px;border-radius:8px;text-decoration:none">Potvrdi narudžbu</a>
                    </td></tr>
                    <tr><td style="font:400 12px Arial,sans-serif;color:{{Muted}};padding-bottom:20px;word-break:break-all">
                      Ako dugme ne radi, otvori: {{Enc(link)}}
                    </td></tr>
                    <tr><td>
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0">{{stavke}}</table>
                    </td></tr>
                    <tr><td style="font:700 16px Arial,sans-serif;color:{{Ink}};text-align:right;padding-top:12px;border-top:1px solid #eceeeb">
                      Ukupno: {{Money(request.TotalAmount)}}
                    </td></tr>
                    <tr><td style="font:400 12px Arial,sans-serif;color:{{Muted}};padding-top:20px">
                      Ako ovu narudžbu nisi poslao/la ti, ne treba ništa da radiš — bez potvrde ona nikad neće nastati.
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body></html>
            """;
    }
}
```

Provjeri da `Unit.ToApi()` postoji u `EPiljara.Domain.Enums` — `OrderEmailTemplate` ga koristi; ako se zove drugačije, uskladi.

- [ ] **Step 2: Implementiraj u SmtpEmailService**

```csharp
    public Task SendGuestOrderRequestAsync(GuestOrderRequest request, CancellationToken cancellationToken = default)
    {
        var baseUrl = (config["App:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var link = $"{baseUrl}/potvrda/{request.Token}";
        return SendHtmlAsync(
            request.CustomerEmail,
            "Potvrdi svoju narudžbu — E piljara",
            GuestOrderEmailTemplate.Build(request, link),
            cancellationToken);
    }
```

**Ne hvataj izuzetak ovdje.** `SendHtmlAsync` u ovoj klasi inače guta greške transporta — provjeri kako je napisana i, ako guta, dodaj privatnu varijantu koja **propušta** izuzetak, pa je zovi odavde. Obrazloženje ide u komentar iznad metode:

```csharp
    // Za razliku od potvrde narudžbe, ovaj mejl ne smije tiho pasti: bez njega
    // kupac nema nijedan način da potvrdi, a narudžba nikad ne nastane. Zato
    // izuzetak ide naviše i handler odbija zahtjev.
```

- [ ] **Step 3: Implementiraj u LoggingEmailService**

Ispiši u log adresu i pun link, da se u dev-u može kliknuti iz konzole bez Mailpita.

- [ ] **Step 4: Provjeri da se gradi**

Run: `dotnet build`
Expected: uspjeh, bez `NotImplementedException` ostataka iz Taska 1.

- [ ] **Step 5: Commit**

```bash
git add backend/src
git commit -m "feat: mejl sa linkom za potvrdu narudžbe gosta"
```

---

### Task 5: Slanje zahtjeva gosta

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Orders/GuestOrderRequests.cs`
- Modify: `backend/src/EPiljara.API/Controllers/OrdersController.cs`
- Test: `backend/tests/EPiljara.Application.Tests/GuestOrderTests.cs`

**Interfaces:**
- Consumes: entiteti iz Taska 3, `IEmailService.SendGuestOrderRequestAsync` iz Taska 4
- Produces: `CreateGuestOrderRequestCommand(CustomerInput Customer, Guid DeliveryZoneId, Guid DeliverySlotId, string DeliveryAddress, string? Note, IReadOnlyList<OrderItemInput> Items, double? Latitude, double? Longitude) : IRequest<GuestOrderRequestDto>`, `GuestOrderRequestDto { DateTime ExpiresAt }`

- [ ] **Step 1: Napiši testove koji padaju**

`backend/tests/EPiljara.Application.Tests/GuestOrderTests.cs`:

```csharp
using EPiljara.Application.Features.Orders;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace EPiljara.Application.Tests;

public class GuestOrderTests
{
    [Fact]
    public async Task Zahtjev_se_upisuje_i_ne_pravi_narudzbu()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var mejl = new FakeEmail();

        var rezultat = await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), mejl)
            .Handle(scena.ZahtjevGosta(), default);

        t.Db.Orders.Should().BeEmpty("narudžba nastaje tek na potvrdu");
        var zahtjev = t.Db.GuestOrderRequests.Single();
        zahtjev.Token.Should().HaveLength(32);
        zahtjev.Items.Should().ContainSingle();
        rezultat.ExpiresAt.Should().BeCloseTo(
            zahtjev.CreatedAt.AddMinutes(GuestOrderRequest.TrajanjeMinuta), TimeSpan.FromSeconds(5));
        mejl.ZahtjeviGosta.Should().ContainSingle();
    }

    [Fact]
    public async Task Zahtjev_pamti_cijenu_iz_trenutka_slanja()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);

        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);

        var stavka = t.Db.GuestOrderRequestItems.Single();
        stavka.UnitPrice.Should().Be(20m);
        stavka.ProductName.Should().Be("Med");
    }

    [Fact]
    public async Task Zahtjev_ne_skida_zalihe()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);

        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);

        t.Db.Products.Single().Stock.Should().Be(100, "zalihe se skidaju tek kad narudžba nastane");
    }

    [Fact]
    public async Task Prijavljen_kupac_ne_moze_kroz_zahtjev_gosta()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var kupac = new User { Name = "Mujo", Email = "mujo@test.local", PasswordHash = "x" };
        t.Db.Users.Add(kupac);
        t.Db.SaveChanges();

        var radnja = () => new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(kupac.Id), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);

        (await radnja.Should().ThrowAsync<DomainException>())
            .Which.Code.Should().Be("already_authenticated");
    }

    [Fact]
    public async Task Zahtjev_ispod_minimuma_pada()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t, minimalniIznos: 500m);

        var radnja = () => new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);

        (await radnja.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be("below_minimum");
    }
}
```

Dodaj `Scena` iz Taska 1 metodu koja pravi komandu zahtjeva (`minimalniIznos` je već parametar):

```csharp
    public CreateGuestOrderRequestCommand ZahtjevGosta() => new(
        new CustomerInput("Mujo Mujić", "mujo@test.local", "065 111 222"),
        _zoneId, _slotId, "Kralja Petra 12", null,
        [new OrderItemInput(_productId, 1)], 44.77, 17.19);
```

- [ ] **Step 2: Pokreni i vidi da pada**

Run: `dotnet test --filter GuestOrderTests`
Expected: FAIL — `CreateGuestOrderRequestCommand` ne postoji.

- [ ] **Step 3: Napiši komandu, validator i handler**

`GuestOrderRequests.cs`. Validator nasljeđuje ista pravila kao `CreateOrderValidator` uz **obavezan e-mail**:

```csharp
public class CreateGuestOrderRequestValidator : AbstractValidator<CreateGuestOrderRequestCommand>
{
    public CreateGuestOrderRequestValidator()
    {
        RuleFor(x => x.Customer).NotNull().WithMessage("Podaci o kupcu su obavezni.");
        When(x => x.Customer is not null, () =>
        {
            RuleFor(x => x.Customer.Name).NotEmpty().WithMessage("Ime je obavezno.");
            RuleFor(x => x.Customer.Phone).NotEmpty().WithMessage("Telefon je obavezan.");
            // Gost bez e-maila nema kako potvrditi — zato je ovdje obavezan, a kod
            // prijavljenog kupca nije.
            RuleFor(x => x.Customer.Email).NotEmpty().WithMessage("E-mail je obavezan za naručivanje bez naloga.");
            RuleFor(x => x.Customer.Email).EmailAddress().WithMessage("Neispravna e-mail adresa.")
                .When(x => !string.IsNullOrWhiteSpace(x.Customer.Email));
        });
        RuleFor(x => x.DeliveryZoneId).NotEmpty().WithMessage("Odaberite zonu dostave.");
        RuleFor(x => x.DeliverySlotId).NotEmpty().WithMessage("Odaberite termin dostave.");
        RuleFor(x => x.DeliveryAddress).NotEmpty().WithMessage("Adresa je obavezna.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Korpa je prazna.");
        RuleForEach(x => x.Items).Must(i => i.Quantity > 0).WithMessage("Neispravna količina.");
    }
}
```

Handler radi redom: odbije prijavljenog (`DomainException("Prijavljeni kupac naručuje sa svog naloga.", "already_authenticated")`), učita zonu i termin istim provjerama kao `CreateOrderHandler`, sastavi stavke sa snimljenim cijenama iz `Product`, provjeri `below_minimum`, upiše zahtjev sa `Token = Guid.NewGuid().ToString("N")` i `ExpiresAt = DateTime.UtcNow.AddMinutes(GuestOrderRequest.TrajanjeMinuta)`, sačuva, pa **pošalje mejl bez try/catch**.

Zalihe se **ne diraju** — komentar iznad tog mjesta neka to kaže, jer je razlika u odnosu na `CreateOrderHandler` namjerna.

- [ ] **Step 4: Pokreni testove**

Run: `dotnet test --filter GuestOrderTests`
Expected: PASS, pet testova.

- [ ] **Step 5: Dodaj akciju u kontroler**

```csharp
    /// <summary>Gost šalje narudžbu — narudžba ne nastaje dok ne klikne link iz mejla.</summary>
    [HttpPost("orders/guest-request")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> GuestRequest([FromBody] CreateGuestOrderRequestCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));
```

- [ ] **Step 6: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat: slanje nepotvrđene narudžbe gosta"
```

---

### Task 6: Potvrda narudžbe gosta

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Orders/GuestOrderRequests.cs`
- Modify: `backend/src/EPiljara.API/Controllers/OrdersController.cs`
- Test: `backend/tests/EPiljara.Application.Tests/GuestOrderTests.cs`

**Interfaces:**
- Consumes: sve iz Taska 5, `OrderCode.GenerateAsync` iz Taska 1
- Produces: `ConfirmGuestOrderCommand(string Token) : IRequest<OrderDto>`

- [ ] **Step 1: Napiši testove koji padaju**

```csharp
    [Fact]
    public async Task Potvrda_pravi_narudzbu_po_snimljenim_cijenama()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);
        var token = t.Db.GuestOrderRequests.Single().Token;

        // Cijena poskupi nakon slanja, prije potvrde.
        var proizvod = t.Db.Products.Single();
        proizvod.Price = 99m;
        t.Db.SaveChanges();

        var rezultat = await new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber())
            .Handle(new ConfirmGuestOrderCommand(token), default);

        rezultat.Items.Single().UnitPrice.Should().Be(20m, "kupac plaća cijenu koju je vidio");
        rezultat.Code.Should().StartWith("EP-");
        t.Db.GuestOrderRequests.Should().BeEmpty("zahtjev se briše čim narudžba nastane");
    }

    [Fact]
    public async Task Potvrda_skida_zalihe()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);
        var token = t.Db.GuestOrderRequests.Single().Token;

        await new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber())
            .Handle(new ConfirmGuestOrderCommand(token), default);

        t.Db.Products.Single().Stock.Should().Be(99);
    }

    [Fact]
    public async Task Potvrda_istekloga_pada_i_ne_pravi_narudzbu()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);
        var zahtjev = t.Db.GuestOrderRequests.Single();
        zahtjev.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        t.Db.SaveChanges();

        var radnja = () => new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber())
            .Handle(new ConfirmGuestOrderCommand(zahtjev.Token), default);

        (await radnja.Should().ThrowAsync<DomainException>())
            .Which.Code.Should().Be("guest_request_expired");
        t.Db.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Dva_klika_na_link_prave_tacno_jednu_narudzbu()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);
        var token = t.Db.GuestOrderRequests.Single().Token;
        var handler = new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber());

        await handler.Handle(new ConfirmGuestOrderCommand(token), default);
        var drugiKlik = () => handler.Handle(new ConfirmGuestOrderCommand(token), default);

        (await drugiKlik.Should().ThrowAsync<DomainException>())
            .Which.Code.Should().Be("guest_request_expired");
        t.Db.Orders.Should().ContainSingle();
    }

    [Fact]
    public async Task Potvrda_pada_ako_je_proizvod_u_medjuvremenu_ugasen()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        await new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail())
            .Handle(scena.ZahtjevGosta(), default);
        var token = t.Db.GuestOrderRequests.Single().Token;
        t.Db.Products.Single().IsAvailable = false;
        t.Db.SaveChanges();

        var radnja = () => new ConfirmGuestOrderHandler(t.Db, new FakeEmail(), new FakeViber())
            .Handle(new ConfirmGuestOrderCommand(token), default);

        var greska = (await radnja.Should().ThrowAsync<DomainException>()).Which;
        greska.Code.Should().Be("product_unavailable");
        greska.Message.Should().Contain("Med", "kupcu treba reći koji proizvod je sporan");
    }
```

- [ ] **Step 2: Pokreni i vidi da pada**

Run: `dotnet test --filter GuestOrderTests`
Expected: FAIL — `ConfirmGuestOrderCommand` ne postoji.

- [ ] **Step 3: Napiši handler**

Redom: nađi zahtjev sa `Include(r => r.Items)`, `Include`-uj zonu i termin. Ako ga nema **ili** je istekao → `ConflictException("Potvrda je istekla ili je narudžba već primljena.", "guest_request_expired")` (409; `ConflictException` nasljeđuje `DomainException`, pa testovi koji hvataju `DomainException` i dalje prolaze). Istekao zahtjev odmah obriši i sačuvaj, da ne čeka čistača.

Za svaku stavku učitaj proizvod; ako ga nema ili nije dostupan → `DomainException($"Proizvod nije dostupan ({stavka.ProductName}).", "product_unavailable")`. Inače skini zalihu: `product.Stock = Math.Max(0, product.Stock - stavka.Quantity);`.

Napravi `Order` sa `Code = await OrderCode.GenerateAsync(db, ct)`, `PublicToken = Guid.NewGuid().ToString("N")`, `CustomerId = null`, iznosima **iz zahtjeva** (ne preračunavaj), i `OrderItem`-ima iz snimljenih vrijednosti zahtjeva.

Obriši zahtjev (`db.GuestOrderRequests.Remove(zahtjev)`), pa **jedan** `SaveChangesAsync` za sve — tako potvrda ili prođe cijela ili nikako, i drugi klik ne može zateći ni pola posla.

Mejl sa detaljima ide **u try/catch** kao u `CreateOrderHandler` — narudžba već postoji i njen opstanak ne smije zavisiti od mejla. Komentar neka objasni zašto je ovdje obrnuto nego u Tasku 5.

Vrati `order.ToDto()`.

- [ ] **Step 4: Pokreni testove**

Run: `dotnet test --filter GuestOrderTests`
Expected: PASS, deset testova.

- [ ] **Step 5: Dodaj akciju u kontroler**

```csharp
    /// <summary>Gost potvrđuje narudžbu klikom iz mejla. Tek ovdje narudžba nastaje.</summary>
    [HttpPost("orders/confirm")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ConfirmGuest([FromBody] ConfirmGuestOrderCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));
```

- [ ] **Step 6: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat: potvrda narudžbe gosta klikom iz mejla"
```

---

### Task 7: Čistač isteklih zahtjeva

**Files:**
- Create: `backend/src/EPiljara.Infrastructure/BackgroundJobs/ExpiredGuestRequestsCleaner.cs`
- Modify: `backend/src/EPiljara.API/Program.cs`
- Test: `backend/tests/EPiljara.Application.Tests/GuestOrderTests.cs`

**Interfaces:**
- Consumes: `GuestOrderRequest` iz Taska 3
- Produces: `ExpiredGuestRequestsCleaner.ObrisiIstekleAsync(AppDbContext db, DateTime sada, CancellationToken ct) → Task<int>`

- [ ] **Step 1: Napiši test koji pada**

```csharp
    [Fact]
    public async Task Cistac_brise_samo_istekle()
    {
        using var t = new TestDb();
        var scena = await Scena.NapraviAsync(t);
        var handler = new CreateGuestOrderRequestHandler(t.Db, new FakeCurrentUser(null), new FakeEmail());
        await handler.Handle(scena.ZahtjevGosta(), default);
        await handler.Handle(scena.ZahtjevGosta(), default);

        var stari = t.Db.GuestOrderRequests.First();
        stari.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        t.Db.SaveChanges();

        var obrisano = await ExpiredGuestRequestsCleaner.ObrisiIstekleAsync(t.Db, DateTime.UtcNow, default);

        obrisano.Should().Be(1);
        t.Db.GuestOrderRequests.Should().ContainSingle();
        t.Db.GuestOrderRequestItems.Should().ContainSingle("stavke odlaze sa zahtjevom");
    }
```

Test zove statičku metodu, ne `BackgroundService` — petlja sa `Task.Delay` se ne testira, briše se.

- [ ] **Step 2: Pokreni i vidi da pada**

Run: `dotnet test --filter Cistac`
Expected: FAIL — `ExpiredGuestRequestsCleaner` ne postoji.

- [ ] **Step 3: Napiši servis**

```csharp
using EPiljara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EPiljara.Infrastructure.BackgroundJobs;

/// <summary>
/// Briše nepotvrđene narudžbe gosta kojima je isteklo vrijeme. Potvrda se na ovo
/// ne oslanja — ona sama provjerava istek, pa zahtjev koji čeka brisanje ne može
/// proći. Ovaj servis postoji samo da smetlje stvarno nestane iz baze.
/// </summary>
public class ExpiredGuestRequestsCleaner(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredGuestRequestsCleaner> logger) : BackgroundService
{
    private static readonly TimeSpan Razmak = TimeSpan.FromMinutes(5);

    public static async Task<int> ObrisiIstekleAsync(AppDbContext db, DateTime sada, CancellationToken ct)
    {
        var istekli = await db.GuestOrderRequests
            .Where(r => r.ExpiresAt <= sada)
            .ToListAsync(ct);

        if (istekli.Count == 0) return 0;

        db.GuestOrderRequests.RemoveRange(istekli);
        await db.SaveChangesAsync(ct);
        return istekli.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var obrisano = await ObrisiIstekleAsync(db, DateTime.UtcNow, stoppingToken);
                if (obrisano > 0)
                    logger.LogInformation("Obrisano isteklih zahtjeva gosta: {Broj}", obrisano);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Pad jednog prolaza ne smije ubiti servis — sljedeći prolaz pokušava ponovo.
                logger.LogError(ex, "Čišćenje isteklih zahtjeva gosta nije uspjelo.");
            }

            try { await Task.Delay(Razmak, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
```

`ObrisiIstekleAsync` prima `AppDbContext`, ne `IAppDbContext`, jer `RemoveRange` i scope traže konkretan tip.

- [ ] **Step 4: Registruj u Program.cs**

Uz ostale registracije servisa:

```csharp
builder.Services.AddHostedService<ExpiredGuestRequestsCleaner>();
```

- [ ] **Step 5: Pokreni testove**

Run: `dotnet test`
Expected: PASS — sve.

- [ ] **Step 6: Commit**

```bash
git add backend/src backend/tests
git commit -m "feat: pozadinsko brisanje isteklih zahtjeva gosta"
```

---

### Task 8: Termin dostave i trajni link u mejlu

**Files:**
- Modify: `backend/src/EPiljara.Infrastructure/Services/OrderEmailTemplate.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Services/SmtpEmailService.cs`

**Interfaces:**
- Consumes: `Order.PublicToken` iz Taska 1
- Produces: `OrderEmailTemplate.Build(Order order, string? trackUrl)`

- [ ] **Step 1: Dodaj termin dostave u šablon**

`OrderEmailTemplate.Build` već prima `Order` sa `DeliverySlot`. Dodaj blok iznad detalja dostave koji ispisuje **kad se očekuje dostava**: `order.DeliverySlot?.Label`, a ako je `null`, sastavi iz `DayOfWeek`, `TimeFrom` i `TimeTo`. Naziv dana na srpskom — provjeri ima li već pomoćnik u `Infrastructure`; ako nema, napiši lokalni niz `["nedjelja", "ponedjeljak", …]` indeksiran sa `DayOfWeek`.

- [ ] **Step 2: Dodaj trajni link**

Promijeni potpis u `Build(Order order, string? trackUrl)`. Kad `trackUrl` nije prazan, na dno dodaj red „Prati svoju narudžbu" sa linkom. Parametar je nullable da postojeći pozivi ne puknu odjednom.

- [ ] **Step 3: Proslijedi link iz SmtpEmailService**

```csharp
        var baseUrl = (config["App:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var trackUrl = string.IsNullOrWhiteSpace(order.PublicToken)
            ? null
            : $"{baseUrl}/narudzba/{order.PublicToken}";
```

- [ ] **Step 4: Provjeri da se gradi i da testovi prolaze**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src
git commit -m "feat: termin dostave i link za praćenje u mejlu narudžbe"
```

---

### Task 9: Frontend — ugovor i pozivi

**Files:**
- Modify: `frontend/src/lib/api/contracts.ts`
- Modify: `frontend/src/lib/api/endpoints.ts`
- Modify: `frontend/src/types/domain.ts`

**Interfaces:**
- Consumes: endpointi iz Taskova 2, 5 i 6
- Produces: `orderApi.guestRequest(body: CreateOrderRequest) → Promise<{ expiresAt: string }>`, `orderApi.confirmGuest(token: string) → Promise<Order>`, `orderApi.track(token: string) → Promise<Order>`

- [ ] **Step 1: Dopuni tipove**

U `src/types/domain.ts`, u `Order`, dodaj uz `customerPhone`:

```ts
  customerEmail?: string;
```

U `src/lib/api/contracts.ts`:

```ts
/** Odgovor na poslatu narudžbu gosta — narudžba još ne postoji, samo zahtjev sa rokom. */
export interface GuestOrderRequestResult {
  expiresAt: string;
}
```

- [ ] **Step 2: Dodaj pozive**

U `endpoints.ts`, u `orderApi`:

```ts
  /* Gost: narudžba nastaje tek na potvrdu iz mejla. */
  guestRequest: (body: CreateOrderRequest) =>
    apiFetch<GuestOrderRequestResult>("/orders/guest-request", { method: "POST", body }).then(
      (r) => r.data,
    ),

  confirmGuest: (token: string) =>
    apiFetch<Order>("/orders/confirm", { method: "POST", body: { token } }).then((r) => r.data),

  track: (token: string) =>
    apiFetch<Order>(`/orders/track/${token}`).then((r) => r.data),
```

- [ ] **Step 3: Provjeri**

Run: `npx tsc --noEmit && npm run lint`
Expected: bez grešaka.

- [ ] **Step 4: Commit**

```bash
git add frontend/src
git commit -m "feat: frontend ugovor za narudžbu gosta i praćenje"
```

---

### Task 10: Kapija prijava / registracija / gost

**Files:**
- Create: `frontend/src/app/(shop)/checkout/nalog/page.tsx`
- Modify: `frontend/src/app/(shop)/cart/page.tsx:101`
- Modify: `frontend/src/app/(shop)/login/page.tsx`, `frontend/src/app/(shop)/register/page.tsx`

**Interfaces:**
- Consumes: `useAuth()` iz `@/lib/auth/auth-context`
- Produces: ruta `/checkout/nalog`, parametar `?next=` na prijavi i registraciji, parametar `?gost=1` na checkoutu

- [ ] **Step 1: Napravi stranicu**

Tri kartice jedna ispod druge, svaka sa ikonicom, naslovom i rečenicom objašnjenja:

- **Prijavi se** — „Tvoje adrese i istorija narudžbi su već tu." → `/login?next=/checkout`
- **Registruj se** — „Napravi nalog da sljedeći put ide brže." → `/register?next=/checkout`
- **Naruči bez naloga** — „Bez registracije. Poslaćemo ti mejl da potvrdiš narudžbu." → `/checkout?gost=1`

Ako je `user` prijavljen, `useEffect` odmah radi `router.replace("/checkout")` — prijavljenom kupcu kapija nema šta da pita. Ako je korpa prazna, isti `EmptyState` kao na checkoutu.

Treća kartica **mora** reći da slijedi potvrda mejlom. Kupac koji to sazna tek poslije slanja forme misli da narudžba nije prošla.

- [ ] **Step 2: Preusmjeri korpu na kapiju**

U `cart/page.tsx` promijeni `href="/checkout"` u `href="/checkout/nalog"`. Tekst dugmeta ostaje „Nastavi na dostavu".

- [ ] **Step 3: Podrži ?next= na prijavi i registraciji**

U obje stranice pročitaj `useSearchParams().get("next")` i poslije uspjeha vodi tamo umjesto na podrazumijevanu stranicu. Prihvati **samo putanje koje počinju sa `/`** — otvoren `next` je poziv na preusmjeravanje na tuđi sajt:

```ts
const next = searchParams.get("next");
const safeNext = next && next.startsWith("/") && !next.startsWith("//") ? next : null;
```

- [ ] **Step 4: Provjeri**

Run: `npx tsc --noEmit && npm run lint`

Pa uz pokrenut stek: korpa → „Nastavi na dostavu" → vidi se kapija; prijavljen kupac ide pravo na checkout.

- [ ] **Step 5: Commit**

```bash
git add frontend/src
git commit -m "feat: izbor prijave, registracije ili naručivanja bez naloga"
```

---

### Task 11: Preuređena forma za dostavu

**Files:**
- Modify: `frontend/src/app/(shop)/checkout/page.tsx`

**Interfaces:**
- Consumes: `AddressChooser`, `useAuth`, `useDeliveryZones`, `useDeliverySlots`
- Produces: stanje forme sa `ime`, `prezime`, `ulica`, `broj`, `stan`, `telefon`, `email`, `zoneId`, `slotId`, `note`

- [ ] **Step 1: Razdvoji polja u stanju**

Zamijeni `name` sa `ime` i `prezime`, a `address` sa `ulica`, `broj` i `stan`. Predpopunjavanje iz profila razdvaja `user.name` na prvi razmak: prvi dio je ime, ostatak prezime.

- [ ] **Step 2: Sastavi vrijednosti koje idu na server**

```ts
// Backend i dalje prima jedno ime i jednu adresu — admin, dispečerski ekran i
// vozačka aplikacija čitaju upravo to. Razdvojena polja su samo oblik forme.
const punoIme = [form.ime, form.prezime].filter(Boolean).join(" ").trim();
const ulicaIBroj = [form.ulica, form.broj].filter(Boolean).join(" ").trim();
const punaAdresa = form.stan ? `${ulicaIBroj}, stan ${form.stan}` : ulicaIBroj;
```

- [ ] **Step 3: Hrani AddressChooser spojenom ulicom i brojem**

`AddressChooser` traži tačku na mapi po tekstu. Ako mu daš samo ulicu, pin pada na sredinu ulice umjesto na kuću, a vozačka aplikacija navigira po koordinatama. Zato mu `value` mora biti `ulicaIBroj`, a `onChange` upisuje nazad u `form.ulica` (broj korisnik drži u svom polju).

Poslije izmjene kućnog broja ponovo pokreni geokodiranje — inače pin ostaje na starom broju. Ako `AddressChooser` to ne podržava, dodaj mu `key={ulicaIBroj}` da se premontira.

- [ ] **Step 4: Preuredi korak „Podaci"**

Dvije kolone na širokom ekranu, jedna na uskom:

- Ime i Prezime jedno pored drugog, oba obavezna
- Telefon sa fiksnim prefiksom `+387` lijevo od polja (statičan tekst u okviru, ne bira se država — dostavljamo samo u BiH)
- E-mail — obavezan kad je gost (`gost === "1"` ili nema `user`), inače neobavezan; ispod polja rečenica: „Na ovu adresu šaljemo link za potvrdu narudžbe."
- Napomena — `Textarea` sa `maxLength={237}` i brojačem `{form.note.length}/237` dolje desno

- [ ] **Step 5: Dodaj sažetak narudžbe sa strane**

Na širokom ekranu desna kolona koja stoji uz formu (`lg:sticky lg:top-24`): stavke sa količinom i cijenom, pa iznos, trošak dostave i ukupno. Na uskom ekranu ide iznad dugmadi. Postojeći iznos u dnu ostaje kao i sad.

- [ ] **Step 6: Uskladi validaciju koraka**

```ts
const gost = !user;
const step1Valid = !!form.ulica && !!form.broj && !!form.zoneId && !!form.slotId;
const step2Valid = !!form.ime && !!form.prezime && !!form.telefon && (!gost || !!form.email);
```

- [ ] **Step 7: Provjeri**

Run: `npx tsc --noEmit && npm run lint`

Pa uz pokrenut stek pošalji narudžbu **kao prijavljen kupac** i provjeri u admin panelu da su ime i adresa stigli spojeni kako treba, i da narudžba ima tačku na mapi.

- [ ] **Step 8: Commit**

```bash
git add frontend/src
git commit -m "feat: preuređena forma za dostavu"
```

---

### Task 12: Tok gosta do mejla

**Files:**
- Create: `frontend/src/components/shop/check-email.tsx`
- Modify: `frontend/src/app/(shop)/checkout/page.tsx`

**Interfaces:**
- Consumes: `orderApi.guestRequest` iz Taska 9
- Produces: `<CheckEmail email={string} expiresAt={string} />`

- [ ] **Step 1: Napravi ekran „provjeri mejl"**

Kartica sa ikonicom koverte, naslovom „Provjeri svoj mejl", rečenicom da je link poslan na ispisanu adresu, i **odbrojavanjem** do `expiresAt` u obliku `mm:ss`, osvježenim svake sekunde kroz `setInterval` koji se čisti u `useEffect` povratku.

Kad odbrojavanje dođe do nule, tekst se mijenja u poruku da je link istekao i da narudžbu treba poslati ponovo, sa dugmetom na `/checkout`. Korpa je i dalje puna, pa kupac ne gubi ništa.

Ispod stoji sitnim slovima da narudžba **još ne postoji** dok se ne potvrdi.

- [ ] **Step 2: Granaj slanje u checkoutu**

```ts
const guestMutation = useMutation({
  mutationFn: (body: CreateOrderRequest) => orderApi.guestRequest(body),
  onSuccess: (res) => {
    // Korpa se NE čisti — narudžba još ne postoji, a ako potvrda istekne
    // kupac mora imati čemu da se vrati.
    setSentTo({ email: form.email, expiresAt: res.expiresAt });
    window.scrollTo({ top: 0, behavior: "smooth" });
  },
  onError: (err) => {
    if (err instanceof ApiRequestError) setErrors(err.fieldErrors);
  },
});
```

U `submit` biraj: prijavljen → postojeći `mutation`, gost → `guestMutation`.

- [ ] **Step 3: Prikaži ekran**

Iznad provjere `if (placed)` dodaj `if (sentTo) return <CheckEmail email={sentTo.email} expiresAt={sentTo.expiresAt} />;`.

- [ ] **Step 4: Provjeri**

Run: `npx tsc --noEmit && npm run lint`

Pa uz pokrenut stek i Mailpit: odjavi se, napuni korpu, prođi kroz kapiju kao gost, pošalji — vidi ekran sa odbrojavanjem, a u Mailpitu (http://localhost:8025) mejl sa linkom. U admin panelu **ne smije** biti nove narudžbe.

- [ ] **Step 5: Commit**

```bash
git add frontend/src
git commit -m "feat: ekran čekanja potvrde za narudžbu gosta"
```

---

### Task 13: Ekran zahvale i praćenja

**Files:**
- Create: `frontend/src/components/shop/order-receipt.tsx`
- Create: `frontend/src/app/(shop)/potvrda/[token]/page.tsx`
- Create: `frontend/src/app/(shop)/narudzba/[token]/page.tsx`
- Modify: `frontend/src/app/(shop)/checkout/page.tsx`

**Interfaces:**
- Consumes: `orderApi.confirmGuest`, `orderApi.track` iz Taska 9
- Produces: `<OrderReceipt order={Order} mode="zahvala" | "pracenje" />`

- [ ] **Step 1: Izdvoji ekran u komponentu**

Prebaci postojeću `Confirmation` funkciju iz `checkout/page.tsx` u `order-receipt.tsx` i proširi je. Dvije kolone na širokom ekranu:

*Lijevo:* zaglavlje („Hvala ti!" kad je `mode="zahvala"`, inače broj narudžbe i `StatusBadge`), pa rečenica da je potvrda poslana na `order.customerEmail`; kartica **Narudžba** sa terminom dostave u zaglavlju i stavkama (`SmartImage`, naziv, količina, cijena); kartica **Podaci o dostavi** (ime, telefon, adresa, napomena); kartica **Način plaćanja** — „Pouzećem pri dostavi".

*Desno:* **Ukupna cijena** — iznos stavki, trošak dostave, ukupno; pa dugmad „Nastavi kupovinu" i, samo za prijavljene, „Moje narudžbe".

U `checkout/page.tsx` zamijeni poziv stare komponente sa `<OrderReceipt order={placed} mode="zahvala" />`.

- [ ] **Step 2: Napravi stranicu potvrde**

`potvrda/[token]/page.tsx`: na montiranju zove `orderApi.confirmGuest(token)`.

- Uspjeh → `clear()` korpe **tu i samo tu**, pa `<OrderReceipt order={order} mode="zahvala" />`
- Greška `guest_request_expired` → kartica „Potvrda je istekla" sa objašnjenjem da link vrijedi 15 minuta, **i rečenicom da je narudžba možda već primljena ako je link otvoren dvaput — u tom slučaju neka provjeri mejl prije nego šalje ponovo**, pa dugme „Vrati u korpu" na `/checkout`
- Greška `product_unavailable` → poruka sa servera (nosi naziv proizvoda) i dugme na `/cart`

Pozovi mutaciju **tačno jednom**: `useRef` zastavica ili `useMutation` sa ručnim `mutate` u `useEffect` sa praznim nizom zavisnosti. Dupli poziv u React strict modu bi drugim klikom dobio 410 i pokazao „isteklo" iako je narudžba prošla.

- [ ] **Step 3: Napravi stranicu praćenja**

`narudzba/[token]/page.tsx`: `useQuery` na `orderApi.track(token)` → `<OrderReceipt order={order} mode="pracenje" />`. Bez `clear()` i bez odbrojavanja. 404 → poruka da narudžba nije pronađena ili je link pogrešan.

- [ ] **Step 4: Provjeri**

Run: `npx tsc --noEmit && npm run lint`

Pa uz pokrenut stek: klikni link iz Mailpita → narudžba se pojavi u admin panelu, korpa se isprazni, ekran pokazuje stavke i iznose. Osvježi stranicu → sad piše „potvrda je istekla", a narudžba je i dalje jedna. Otvori link za praćenje iz drugog mejla → vidi se status.

- [ ] **Step 5: Commit**

```bash
git add frontend/src
git commit -m "feat: ekran zahvale i praćenje narudžbe bez naloga"
```

---

### Task 14: Dokumentacija i prolaz kroz oba toka

**Files:**
- Modify: `CLAUDE.md`
- Modify: `PRODUCTION.md`

- [ ] **Step 1: Dopuni CLAUDE.md**

U dio o ugovoru dodaj da gost ne naručuje kroz `POST /orders` nego kroz `guest-request` + `confirm`, da narudžba nastaje tek na potvrdu i da zahtjev traje 15 minuta. U dio o mejlovima dodaj razliku: mejl za potvrdu gosta **nije** best-effort, za razliku od svih ostalih.

- [ ] **Step 2: Dopuni PRODUCTION.md**

`App:BaseUrl` sad gradi i link za potvrdu i link za praćenje — ako je pogrešan, gost ne može potvrditi narudžbu. Napiši i da `GET /orders/track/{token}` vraća podatke kupca svakome ko ima token, pa link ne smije u sisteme koji loguju pune URL-ove.

- [ ] **Step 3: Pusti sve testove**

Run: `dotnet test` iz `backend/`, pa `npx tsc --noEmit && npm run lint` iz `frontend/`
Expected: sve prolazi.

- [ ] **Step 4: Prođi kroz oba toka uz pokrenut stek**

Gost: korpa → kapija → forma → ekran čekanja → mejl → potvrda → narudžba u admin panelu → link za praćenje radi.
Prijavljen: korpa → preskočena kapija → forma → narudžba odmah → mejl sa terminom i linkom → `/orders` je vidi.

Provjeri i da nakon 15 minuta zahtjev nestane iz `GuestOrderRequests`, i da se čistač javi u logu.

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md PRODUCTION.md
git commit -m "docs: opiši naručivanje bez naloga i potvrdu mejlom"
```
