# Moj nalog i adresar — plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kupac dobija stranicu „Moj nalog" na kojoj mijenja ime i telefon i vodi adresar sa više adresa, a na checkoutu bira jednu od sačuvanih ili unosi sasvim novu.

**Architecture:** Nova tabela `Addresses` vezana za `Users`, sa CRUD endpointima pod `/account/*` u CQRS obrascu koji repozitorij već koristi (MediatR + FluentValidation, auto-discovery, bez DI registracije po handleru). Adresar zamjenjuje dosadašnji spisak izveden iz narudžbi. Kolone `Users.Address` i `Users.DeliveryZoneId` se brišu **tek u zadnjem zadatku**, kad je novi put dokazan.

**Tech Stack:** ASP.NET Core 9, EF Core + PostgreSQL, MediatR, FluentValidation, Next.js 15, TanStack Query, Tailwind v4.

**Spec:** `docs/superpowers/specs/2026-09-04-moj-nalog-adresar-design.md`

## Global Constraints

- **Nema test framework-a.** `CLAUDE.md` kaže doslovno: nema test projekata i nema `test` skripte; izmjene se provjeravaju pokretanjem steka i prolaskom kroz tok. Koraci „provjeri" u ovom planu su zato **stvarne komande** (`curl`, `psql`, `tsc`, `lint`, `dotnet build`), a ne unit testovi. Ne izmišljati test fajlove; ako se želi test projekat, to je zaseban dogovor prije ovog plana.
- Backend na `http://localhost:5080`, frontend na `http://localhost:3000`, Postgres kroz `docker compose up -d postgres`.
- Svaki endpoint vraća omotač `{ data, error, meta }` — koristiti `Envelope`/`EnvelopeList`/`EnvelopeCreated` iz `ApiControllerBase`, nikad ručno graditi odgovor.
- Ključevi validacionih grešaka su camelCase pune putanje (`Input.Name` → `input.name`).
- Korisnički vidljivi tekstovi su na **srpskom**.
- Domenske greške se bacaju (`DomainException` → 400, `NotFoundException` → 404, `ForbiddenException` → 403, `ConflictException` → 409); middleware ih mapira.
- Demo nalozi: `admin@epiljara.ba` / `admin123`, `mujo@example.com` / `mujo123`.
- Migracije: `dotnet dotnet-ef migrations add <Ime> -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations`.
- Najviše **10** adresa po korisniku.
- Prije svakog `dotnet build` ubiti pokrenuti `EPiljara.API`, inače build pukne na zaključanom DLL-u.

---

### Task 1: Entitet `Address`, tabela i prenos postojećih adresa

Ovaj zadatak samo **dodaje**. Kolone na `Users` ostaju netaknute, aplikacija radi kao i prije, a u bazi se pojavljuje adresar popunjen iz postojećih podataka.

**Files:**
- Create: `backend/src/EPiljara.Domain/Entities/Address.cs`
- Modify: `backend/src/EPiljara.Application/Common/Interfaces/IAppDbContext.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs` (oko linije 68, uz `Entity<User>`)
- Create: `backend/src/EPiljara.Infrastructure/Persistence/Migrations/<timestamp>_AddAddresses.cs` (generisan)

**Interfaces:**
- Produces: entitet `Address` sa poljima `UserId`, `Label`, `FullAddress`, `Latitude`, `Longitude`, `DeliveryZoneId`, `IsDefault`, `CreatedAt`; `IAppDbContext.Addresses` tipa `DbSet<Address>`.

- [ ] **Step 1: Napisati entitet**

`backend/src/EPiljara.Domain/Entities/Address.cs`:

```csharp
using EPiljara.Domain.Common;

namespace EPiljara.Domain.Entities;

/// <summary>
/// Sačuvana adresa dostave jednog kupca.
///
/// Narudžba NE pokazuje na ovaj red — ona čuva adresu kao tekst, isto kao što
/// OrderItem čuva cijenu u trenutku narudžbe. Zato brisanje adrese ne mijenja
/// nijednu raniju narudžbu, i zato ovdje namjerno nema veze ka Order.
/// </summary>
public class Address : Entity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>„Kuća", „Posao"… Opciono; kupac ne mora imenovati adresu.</summary>
    public string? Label { get; set; }

    public string FullAddress { get; set; } = string.Empty;

    /// <summary>
    /// Nullable jer adrese prenesene iz User.Address nemaju pin i nikad ga nisu imale.
    /// Izmišljena tačka je gora od nikakve — vozača šalje na pogrešno mjesto sa punim
    /// povjerenjem, dok prazna tačka samo vrati rutu na sortiranje po zonama.
    /// </summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public Guid? DeliveryZoneId { get; set; }
    public DeliveryZone? DeliveryZone { get; set; }

    /// <summary>Tačno jedna glavna po korisniku; održava se u handlerima.</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Dodati DbSet u interfejs**

U `backend/src/EPiljara.Application/Common/Interfaces/IAppDbContext.cs`, ispod `DbSet<User> Users { get; }`:

```csharp
    DbSet<Address> Addresses { get; }
```

- [ ] **Step 3: Dodati DbSet i konfiguraciju u AppDbContext**

U `AppDbContext` uz ostale `DbSet` deklaracije:

```csharp
    public DbSet<Address> Addresses => Set<Address>();
```

I u `OnModelCreating`, odmah iza bloka `b.Entity<User>(...)`:

```csharp
        b.Entity<Address>(e =>
        {
            e.Property(x => x.Label).HasMaxLength(60);
            // 400 je isto kao Order.DeliveryAddress — adresa putuje odavde u narudžbu,
            // pa kraće polje ovdje bi značilo da se nešto što je stalo u narudžbu
            // ne može sačuvati u adresar.
            e.Property(x => x.FullAddress).HasMaxLength(400).IsRequired();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.DeliveryZone).WithMany()
                .HasForeignKey(x => x.DeliveryZoneId).OnDelete(DeleteBehavior.Restrict);
        });
```

- [ ] **Step 4: Generisati migraciju**

```bash
cd backend
dotnet dotnet-ef migrations add AddAddresses -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations
```

- [ ] **Step 5: Ručno dopisati prenos podataka u migraciju**

EF generiše samo `CreateTable`. Na **kraj** metode `Up` dopisati prenos — bez njega postojeći kupci ostaju bez ijedne adrese:

```csharp
            // Prenos postojećih adresa sa korisnika u adresar. Koordinate ostaju NULL:
            // te adrese nikad nisu imale pin i izmišljanje tačke bi poslalo vozača
            // na pogrešno mjesto. Kolone na Users se OVDJE ne diraju — brišu se tek
            // u zadnjem zadatku, kad novi put bude dokazan.
            migrationBuilder.Sql("""
                INSERT INTO "Addresses" ("Id", "UserId", "Label", "FullAddress",
                                         "Latitude", "Longitude", "DeliveryZoneId",
                                         "IsDefault", "CreatedAt")
                SELECT gen_random_uuid(), u."Id", NULL, u."Address",
                       NULL, NULL, u."DeliveryZoneId",
                       TRUE, NOW() AT TIME ZONE 'UTC'
                FROM "Users" u
                WHERE u."Address" IS NOT NULL AND btrim(u."Address") <> '';
                """);
```

U metodu `Down`, **prije** `DropTable`, ništa se ne dodaje — tabela se briše u cjelini.

- [ ] **Step 6: Pustiti migraciju i provjeriti prenos**

```bash
cd backend
dotnet dotnet-ef database update -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure
docker exec epiljara-postgres psql -U epiljara -d epiljara -c 'SELECT u."Email", a."FullAddress", a."IsDefault" FROM "Addresses" a JOIN "Users" u ON u."Id"=a."UserId" ORDER BY u."Email";'
```

Očekivano: po jedan red za svakog korisnika koji je imao nepraznu `Users.Address`, svi sa `IsDefault = t`. Uporediti broj sa:

```bash
docker exec epiljara-postgres psql -U epiljara -d epiljara -c 'SELECT count(*) FROM "Users" WHERE "Address" IS NOT NULL AND btrim("Address") <> '"''"';'
```

Brojevi moraju biti isti.

- [ ] **Step 7: Build i commit**

```bash
cd backend && dotnet build -v q --nologo
git add backend/src/EPiljara.Domain/Entities/Address.cs backend/src/EPiljara.Application/Common/Interfaces/IAppDbContext.cs backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs backend/src/EPiljara.Infrastructure/Persistence/Migrations/
git commit -m "feat: tabela Addresses i prenos postojecih adresa sa korisnika"
```

---

### Task 2: Čitanje adresara — `GET /account/addresses`

**Files:**
- Create: `backend/src/EPiljara.Application/DTOs/AccountDtos.cs`
- Create: `backend/src/EPiljara.Application/Features/Account/AddressQueries.cs`
- Create: `backend/src/EPiljara.API/Controllers/AccountController.cs`
- Modify: `backend/src/EPiljara.Application/Mapping/MappingExtensions.cs`

**Interfaces:**
- Consumes: `Address` entitet i `IAppDbContext.Addresses` iz Task 1.
- Produces: `AddressDto` (polja `Id`, `Label`, `FullAddress`, `Latitude`, `Longitude`, `DeliveryZoneId`, `DeliveryZoneName`, `DeliveryZoneActive`, `IsDefault`); `GetAccountAddressesQuery`; `AccountController`.

> Ime je `GetAccountAddressesQuery`, a ne `GetMyAddressesQuery`, jer stari upit tog imena još postoji u `Features/Orders/OrderQueries.cs` i briše se tek u Task 6. Dva tipa istog imena u istom assembly-ju ne mogu koegzistirati.

- [ ] **Step 1: Napisati DTO**

`backend/src/EPiljara.Application/DTOs/AccountDtos.cs`:

```csharp
namespace EPiljara.Application.DTOs;

public class AddressDto
{
    public Guid Id { get; init; }
    public string? Label { get; init; }
    public string FullAddress { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public Guid? DeliveryZoneId { get; init; }
    public string? DeliveryZoneName { get; init; }

    /// <summary>
    /// Zona se može ugasiti nakon što je adresa sačuvana. Klijent to mora znati da
    /// ne bi predlagao zonu koju CreateOrderValidator odbija sa `invalid_zone` tek
    /// pri slanju narudžbe.
    /// </summary>
    public bool DeliveryZoneActive { get; init; }

    public bool IsDefault { get; init; }
}
```

- [ ] **Step 2: Dodati mapiranje**

Na kraj `backend/src/EPiljara.Application/Mapping/MappingExtensions.cs`, unutar iste statičke klase:

```csharp
    public static AddressDto ToDto(this Address a) => new()
    {
        Id = a.Id,
        Label = a.Label,
        FullAddress = a.FullAddress,
        Latitude = a.Latitude,
        Longitude = a.Longitude,
        DeliveryZoneId = a.DeliveryZoneId,
        DeliveryZoneName = a.DeliveryZone?.Name,
        DeliveryZoneActive = a.DeliveryZone?.IsActive ?? false,
        IsDefault = a.IsDefault,
    };
```

- [ ] **Step 3: Napisati upit**

`backend/src/EPiljara.Application/Features/Account/AddressQueries.cs`:

```csharp
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Account;

public record GetAccountAddressesQuery : IRequest<IReadOnlyList<AddressDto>>;

public class GetAccountAddressesHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<GetAccountAddressesQuery, IReadOnlyList<AddressDto>>
{
    public async Task<IReadOnlyList<AddressDto>> Handle(GetAccountAddressesQuery q, CancellationToken ct)
    {
        // Glavna prva, pa najskorije dodane — klijent uzima prvu kao predloženu.
        var addresses = await db.Addresses
            .Include(a => a.DeliveryZone)
            .Where(a => a.UserId == current.Id)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        return addresses.Select(a => a.ToDto()).ToList();
    }
}
```

- [ ] **Step 4: Napisati kontroler**

`backend/src/EPiljara.API/Controllers/AccountController.cs`:

```csharp
using EPiljara.Application.Features.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EPiljara.API.Controllers;

/// <summary>Nalog prijavljenog kupca: njegovi podaci i njegov adresar.</summary>
[Authorize]
public class AccountController : ApiControllerBase
{
    [HttpGet("account/addresses")]
    public async Task<IActionResult> Addresses() =>
        EnvelopeList(await Mediator.Send(new GetAccountAddressesQuery()));
}
```

- [ ] **Step 5: Pokrenuti API i provjeriti odgovor**

```bash
cd backend && dotnet run --project src/EPiljara.API
```

U drugom terminalu:

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/auth/login -H "Content-Type: application/json" \
  -d '{"email":"mujo@example.com","password":"mujo123"}' | grep -o '"token":"[^"]*' | cut -d'"' -f4)
curl -s http://localhost:5080/account/addresses -H "Authorization: Bearer $TOKEN"
```

Očekivano: `{"data":[...],"error":null,"meta":{"total":N}}` sa adresom prenesenom u Task 1, `"isDefault":true`.

- [ ] **Step 6: Provjeriti da neprijavljen dobija 401**

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5080/account/addresses
```

Očekivano: `401`.

- [ ] **Step 7: Commit**

```bash
git add backend/src/EPiljara.Application/DTOs/AccountDtos.cs backend/src/EPiljara.Application/Features/Account/ backend/src/EPiljara.API/Controllers/AccountController.cs backend/src/EPiljara.Application/Mapping/MappingExtensions.cs
git commit -m "feat: GET /account/addresses"
```

---

### Task 3: Pisanje adresara — dodaj, izmijeni, obriši, postavi glavnu

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Account/AddressCommands.cs`
- Modify: `backend/src/EPiljara.API/Controllers/AccountController.cs`

**Interfaces:**
- Consumes: `AddressDto`, `GetAccountAddressesQuery` iz Task 2.
- Produces: `CreateAddressCommand(string? Label, string FullAddress, double? Latitude, double? Longitude, Guid? DeliveryZoneId)`, `UpdateAddressCommand(Guid Id, …ista polja)`, `DeleteAddressCommand(Guid Id)`, `SetDefaultAddressCommand(Guid Id)`.

- [ ] **Step 1: Napisati komande, validator i handlere**

`backend/src/EPiljara.Application/Features/Account/AddressCommands.cs`:

```csharp
using EPiljara.Application.Common;
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Account;

public record CreateAddressCommand(
    string? Label,
    string FullAddress,
    double? Latitude,
    double? Longitude,
    Guid? DeliveryZoneId) : IRequest<AddressDto>;

public record UpdateAddressCommand(
    Guid Id,
    string? Label,
    string FullAddress,
    double? Latitude,
    double? Longitude,
    Guid? DeliveryZoneId) : IRequest<AddressDto>;

public record DeleteAddressCommand(Guid Id) : IRequest<Unit>;

public record SetDefaultAddressCommand(Guid Id) : IRequest<AddressDto>;

/// <summary>
/// Pravila za tačku su ista kao u CreateOrderValidator i to je namjerno: adresa
/// odavde putuje u narudžbu, pa bi blaža provjera ovdje samo pomjerila grešku na
/// kasniji trenutak, kad je kupac već u checkoutu.
///
/// Sve poruke o tački idu pod ključ `fullAddress` jer forma ima jedno polje sa
/// mapom — poruka pod "latitude" ne bi imala gdje da se prikaže.
/// </summary>
public static class AddressRules
{
    public static void Apply<T>(AbstractValidator<T> v,
        Func<T, string> address,
        Func<T, double?> lat,
        Func<T, double?> lon)
    {
        v.RuleFor(x => address(x)).NotEmpty().WithName("fullAddress")
            .WithMessage("Adresa je obavezna.");
        v.RuleFor(x => address(x)).MaximumLength(400).WithName("fullAddress")
            .WithMessage("Adresa je predugačka.");
        v.RuleFor(x => lat(x)).InclusiveBetween(-90, 90)
            .When(x => lat(x).HasValue).WithName("fullAddress")
            .WithMessage("Neispravna geografska širina.");
        v.RuleFor(x => lon(x)).InclusiveBetween(-180, 180)
            .When(x => lon(x).HasValue).WithName("fullAddress")
            .WithMessage("Neispravna geografska dužina.");
        // Pola tačke nije tačka — pin bez jedne ose bi se vodio kao "ima lokaciju".
        v.RuleFor(x => x).Must(x => lat(x).HasValue == lon(x).HasValue)
            .WithName("fullAddress")
            .WithMessage("Potrebne su i geografska širina i dužina.");
        v.RuleFor(x => x)
            .Must(x => GeoBounds.Contains(lat(x)!.Value, lon(x)!.Value))
            .When(x => lat(x) is >= -90 and <= 90 && lon(x) is >= -180 and <= 180)
            .WithName("fullAddress")
            .WithMessage(GeoBounds.OutsideMessage);
    }
}

public class CreateAddressValidator : AbstractValidator<CreateAddressCommand>
{
    public CreateAddressValidator() =>
        AddressRules.Apply(this, x => x.FullAddress, x => x.Latitude, x => x.Longitude);
}

public class UpdateAddressValidator : AbstractValidator<UpdateAddressCommand>
{
    public UpdateAddressValidator() =>
        AddressRules.Apply(this, x => x.FullAddress, x => x.Latitude, x => x.Longitude);
}

public class CreateAddressHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<CreateAddressCommand, AddressDto>
{
    /// <summary>Granica postoji da spisak i tabela ne rastu bez kraja.</summary>
    public const int MaxAdresa = 10;

    public async Task<AddressDto> Handle(CreateAddressCommand cmd, CancellationToken ct)
    {
        // Broji se u handleru, ne u validatoru: validator ne vidi bazu.
        var moje = await db.Addresses.Where(a => a.UserId == current.Id!.Value).ToListAsync(ct);
        if (moje.Count >= MaxAdresa)
            throw new DomainException(
                $"Možete sačuvati najviše {MaxAdresa} adresa.", "address_limit");

        var address = new Address
        {
            UserId = current.Id!.Value,
            Label = string.IsNullOrWhiteSpace(cmd.Label) ? null : cmd.Label.Trim(),
            FullAddress = cmd.FullAddress.Trim(),
            Latitude = cmd.Latitude,
            Longitude = cmd.Longitude,
            DeliveryZoneId = cmd.DeliveryZoneId,
            // Prva adresa je glavna sama od sebe — inače bi kupac imao adresar
            // bez glavne adrese i checkout bi se ponašao kao za gosta.
            IsDefault = moje.Count == 0,
        };

        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct);

        await db.Addresses.Entry(address).Reference(a => a.DeliveryZone).LoadAsync(ct);
        return address.ToDto();
    }
}

public class UpdateAddressHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<UpdateAddressCommand, AddressDto>
{
    public async Task<AddressDto> Handle(UpdateAddressCommand cmd, CancellationToken ct)
    {
        var address = await db.Addresses
            .Include(a => a.DeliveryZone)
            .FirstOrDefaultAsync(a => a.Id == cmd.Id, ct)
            ?? throw new NotFoundException("Adresa nije pronađena.");

        // Vlasništvo se provjerava poslije dohvata, a ne filterom u upitu, da razlika
        // između "ne postoji" i "nije tvoja" ostane namjerna odluka.
        if (address.UserId != current.Id)
            throw new ForbiddenException("Ova adresa nije vaša.");

        address.Label = string.IsNullOrWhiteSpace(cmd.Label) ? null : cmd.Label.Trim();
        address.FullAddress = cmd.FullAddress.Trim();
        address.Latitude = cmd.Latitude;
        address.Longitude = cmd.Longitude;
        address.DeliveryZoneId = cmd.DeliveryZoneId;

        await db.SaveChangesAsync(ct);
        await db.Addresses.Entry(address).Reference(a => a.DeliveryZone).LoadAsync(ct);
        return address.ToDto();
    }
}

public class DeleteAddressHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<DeleteAddressCommand, Unit>
{
    public async Task<Unit> Handle(DeleteAddressCommand cmd, CancellationToken ct)
    {
        // Sve korisnikove adrese u JEDNOM upitu, izmjena u memoriji, jedan SaveChanges.
        // Isti razlog kao u AdminDispatch: entitet označen za brisanje je u bazi sve
        // do SaveChanges, pa bi ponovni upit vratio red koji upravo brišemo.
        var moje = await db.Addresses.Where(a => a.UserId == current.Id!.Value).ToListAsync(ct);

        var address = moje.FirstOrDefault(a => a.Id == cmd.Id);
        if (address is null)
        {
            // Postoji li uopšte, ili je tuđa? Razlika je vidljiva korisniku.
            var postoji = await db.Addresses.AnyAsync(a => a.Id == cmd.Id, ct);
            if (postoji) throw new ForbiddenException("Ova adresa nije vaša.");
            throw new NotFoundException("Adresa nije pronađena.");
        }

        db.Addresses.Remove(address);

        // Ako je obrisana bila glavna, oznaka ide na najskorije dodanu preostalu.
        if (address.IsDefault)
        {
            var sljedeca = moje.Where(a => a.Id != address.Id)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();
            if (sljedeca is not null) sljedeca.IsDefault = true;
        }

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public class SetDefaultAddressHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<SetDefaultAddressCommand, AddressDto>
{
    public async Task<AddressDto> Handle(SetDefaultAddressCommand cmd, CancellationToken ct)
    {
        var moje = await db.Addresses
            .Include(a => a.DeliveryZone)
            .Where(a => a.UserId == current.Id!.Value)
            .ToListAsync(ct);

        var address = moje.FirstOrDefault(a => a.Id == cmd.Id);
        if (address is null)
        {
            var postoji = await db.Addresses.AnyAsync(a => a.Id == cmd.Id, ct);
            if (postoji) throw new ForbiddenException("Ova adresa nije vaša.");
            throw new NotFoundException("Adresa nije pronađena.");
        }

        // Gašenje stare i paljenje nove u istom SaveChanges — nikad dva reda sa IsDefault.
        foreach (var a in moje) a.IsDefault = a.Id == address.Id;

        await db.SaveChangesAsync(ct);
        return address.ToDto();
    }
}
```

- [ ] **Step 2: Dopuniti kontroler**

U `AccountController` dodati ispod postojeće akcije:

```csharp
    [HttpPost("account/addresses")]
    public async Task<IActionResult> CreateAddress([FromBody] CreateAddressCommand command) =>
        EnvelopeCreated(await Mediator.Send(command));

    [HttpPut("account/addresses/{id:guid}")]
    public async Task<IActionResult> UpdateAddress(Guid id, [FromBody] UpdateAddressBody body) =>
        Envelope(await Mediator.Send(new UpdateAddressCommand(
            id, body.Label, body.FullAddress, body.Latitude, body.Longitude, body.DeliveryZoneId)));

    [HttpDelete("account/addresses/{id:guid}")]
    public async Task<IActionResult> DeleteAddress(Guid id)
    {
        await Mediator.Send(new DeleteAddressCommand(id));
        return NoContent();
    }

    [HttpPost("account/addresses/{id:guid}/default")]
    public async Task<IActionResult> SetDefault(Guid id) =>
        Envelope(await Mediator.Send(new SetDefaultAddressCommand(id)));
```

I na dno istog fajla, izvan klase kontrolera:

```csharp
/// <summary>Tijelo PUT-a bez Id-a — Id dolazi iz putanje, da se ne mogu razići.</summary>
public record UpdateAddressBody(
    string? Label,
    string FullAddress,
    double? Latitude,
    double? Longitude,
    Guid? DeliveryZoneId);
```

- [ ] **Step 3: Provjeriti cio ciklus preko API-ja**

Uz pokrenut backend:

```bash
TOKEN=$(curl -s -X POST http://localhost:5080/auth/login -H "Content-Type: application/json" \
  -d '{"email":"mujo@example.com","password":"mujo123"}' | grep -o '"token":"[^"]*' | cut -d'"' -f4)
ZONA=$(curl -s http://localhost:5080/delivery/zones -H "Authorization: Bearer $TOKEN" | grep -o '"id":"[^"]*' | head -1 | cut -d'"' -f4)

# dodaj
NOVA=$(curl -s -X POST http://localhost:5080/account/addresses \
  -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" \
  -d "{\"label\":\"Posao\",\"fullAddress\":\"Kralja Petra 10\",\"latitude\":44.7722,\"longitude\":17.1910,\"deliveryZoneId\":\"$ZONA\"}")
echo "$NOVA"
ID=$(echo "$NOVA" | grep -o '"id":"[^"]*' | head -1 | cut -d'"' -f4)

# postavi kao glavnu, pa pročitaj spisak
curl -s -X POST "http://localhost:5080/account/addresses/$ID/default" -H "Authorization: Bearer $TOKEN" > /dev/null
curl -s http://localhost:5080/account/addresses -H "Authorization: Bearer $TOKEN"
```

Očekivano: nova adresa je prva u spisku, `"isDefault":true`, a ranija adresa `"isDefault":false` — **tačno jedna** sa `true`.

- [ ] **Step 4: Provjeriti zaštitu tuđe adrese**

```bash
TUDJ=$(curl -s -X POST http://localhost:5080/auth/login -H "Content-Type: application/json" \
  -d '{"email":"ana@example.com","password":"ana123"}' | grep -o '"token":"[^"]*' | cut -d'"' -f4)
curl -s -o /dev/null -w "%{http_code}\n" -X DELETE "http://localhost:5080/account/addresses/$ID" -H "Authorization: Bearer $TUDJ"
```

Očekivano: `403`. Ako lozinka za `ana@example.com` nije `ana123`, uzeti bilo koji drugi Customer nalog iz `DbSeeder` i njegovu lozinku.

- [ ] **Step 5: Provjeriti granicu od 10**

Dodati adrese dok ih ne bude 10, pa pokušati jedanaestu:

```bash
for i in $(seq 1 12); do
  curl -s -o /dev/null -w "$i: %{http_code}\n" -X POST http://localhost:5080/account/addresses \
    -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" \
    -d "{\"fullAddress\":\"Test ulica $i\"}"
done
```

Očekivano: prvih nekoliko `201`, a od trenutka kad ih ima 10 → `400` sa kodom `address_limit`.

- [ ] **Step 6: Očistiti testne adrese**

```bash
docker exec epiljara-postgres psql -U epiljara -d epiljara -c 'DELETE FROM "Addresses" WHERE "FullAddress" LIKE '"'"'Test ulica %'"'"' OR "FullAddress" = '"'"'Kralja Petra 10'"'"';'
```

Zatim provjeriti da je svakom korisniku ostala tačno jedna glavna adresa:

```bash
docker exec epiljara-postgres psql -U epiljara -d epiljara -c 'SELECT "UserId", count(*) FILTER (WHERE "IsDefault") AS glavnih, count(*) AS ukupno FROM "Addresses" GROUP BY 1;'
```

Ako je neki korisnik ostao sa `glavnih = 0` jer je čišćenje obrisalo baš glavnu, postaviti glavnu ručno — i zabilježiti da brisanje kroz API to radi samo, a direktan `DELETE` u bazi ne.

- [ ] **Step 7: Commit**

```bash
git add backend/src/EPiljara.Application/Features/Account/AddressCommands.cs backend/src/EPiljara.API/Controllers/AccountController.cs
git commit -m "feat: dodavanje, izmjena, brisanje i postavljanje glavne adrese"
```

---

### Task 4: Izmjena podataka o korisniku — `PUT /account/profile`

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Account/ProfileCommands.cs`
- Modify: `backend/src/EPiljara.API/Controllers/AccountController.cs`

**Interfaces:**
- Produces: `UpdateProfileCommand(string Name, string? Phone)` → `AuthUserDto`.

- [ ] **Step 1: Napisati komandu i handler**

`backend/src/EPiljara.Application/Features/Account/ProfileCommands.cs`:

```csharp
using EPiljara.Application.Common.Interfaces;
using EPiljara.Application.DTOs;
using EPiljara.Application.Mapping;
using EPiljara.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Features.Account;

/// <summary>
/// Namjerno bez e-maila: e-mail je i korisničko ime i potvrđena adresa, pa bi
/// njegova izmjena tražila ponovnu potvrdu i rukovanje sudarom sa postojećim
/// nalogom. To je zaseban posao, ne usputno polje u ovoj formi.
/// </summary>
public record UpdateProfileCommand(string Name, string? Phone) : IRequest<AuthUserDto>;

public class UpdateProfileValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Ime je obavezno.");
        RuleFor(x => x.Name).MaximumLength(160).WithMessage("Ime je predugačko.");
        RuleFor(x => x.Phone).MaximumLength(40).WithMessage("Telefon je predugačak.");
    }
}

public class UpdateProfileHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<UpdateProfileCommand, AuthUserDto>
{
    public async Task<AuthUserDto> Handle(UpdateProfileCommand cmd, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct)
            ?? throw new NotFoundException("Korisnik nije pronađen.");

        user.Name = cmd.Name.Trim();
        user.Phone = string.IsNullOrWhiteSpace(cmd.Phone) ? null : cmd.Phone.Trim();

        await db.SaveChangesAsync(ct);
        return user.ToAuthDto();
    }
}
```

- [ ] **Step 2: Dopuniti kontroler**

```csharp
    [HttpPut("account/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileCommand command) =>
        Envelope(await Mediator.Send(command));
```

- [ ] **Step 3: Provjeriti**

```bash
curl -s -X PUT http://localhost:5080/account/profile \
  -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"Mujo Mujic","phone":"065111222"}'
curl -s http://localhost:5080/auth/me -H "Authorization: Bearer $TOKEN"
```

Očekivano: oba odgovora nose novo ime i telefon. Prazno ime → `400` sa `errors.name`.

- [ ] **Step 4: Commit**

```bash
git add backend/src/EPiljara.Application/Features/Account/ProfileCommands.cs backend/src/EPiljara.API/Controllers/AccountController.cs
git commit -m "feat: PUT /account/profile za ime i telefon"
```

---

### Task 5: Stranica „Moj nalog"

**Files:**
- Modify: `frontend/src/types/domain.ts`
- Modify: `frontend/src/lib/api/contracts.ts`
- Modify: `frontend/src/lib/api/endpoints.ts`
- Modify: `frontend/src/lib/api/hooks.ts`
- Create: `frontend/src/app/(shop)/account/page.tsx`
- Create: `frontend/src/components/shop/address-form.tsx`
- Modify: `frontend/src/components/layout/shop-navbar.tsx`

**Interfaces:**
- Consumes: endpointi iz Task 2, 3 i 4.
- Produces: tip `Address`, `accountApi`, hook `useMyAddresses()`, komponenta `AddressForm`.

- [ ] **Step 1: Dodati tip**

U `frontend/src/types/domain.ts`, uz ostale interfejse:

```ts
export interface Address {
  id: string;
  label?: string;
  fullAddress: string;
  latitude?: number;
  longitude?: number;
  deliveryZoneId?: string;
  deliveryZoneName?: string;
  /** Zona je mogla biti ugašena nakon što je adresa sačuvana. */
  deliveryZoneActive: boolean;
  isDefault: boolean;
}
```

- [ ] **Step 2: Dodati ugovore zahtjeva**

U `frontend/src/lib/api/contracts.ts`:

```ts
export interface SaveAddressRequest {
  label?: string;
  fullAddress: string;
  latitude?: number;
  longitude?: number;
  deliveryZoneId?: string;
}

export interface UpdateProfileRequest {
  name: string;
  phone?: string;
}
```

- [ ] **Step 3: Dodati API sloj**

U `frontend/src/lib/api/endpoints.ts`, uz `orderApi`:

```ts
/* ---------- Nalog kupca ---------- */

export const accountApi = {
  addresses: () => apiFetch<Address[]>("/account/addresses").then((r) => r.data),
  createAddress: (body: SaveAddressRequest) =>
    apiFetch<Address>("/account/addresses", { method: "POST", body }).then((r) => r.data),
  updateAddress: (id: string, body: SaveAddressRequest) =>
    apiFetch<Address>(`/account/addresses/${id}`, { method: "PUT", body }).then((r) => r.data),
  deleteAddress: (id: string) =>
    apiFetch<null>(`/account/addresses/${id}`, { method: "DELETE" }).then(() => undefined),
  setDefaultAddress: (id: string) =>
    apiFetch<Address>(`/account/addresses/${id}/default`, { method: "POST" }).then((r) => r.data),
  updateProfile: (body: UpdateProfileRequest) =>
    apiFetch<AuthUser>("/account/profile", { method: "PUT", body }).then((r) => r.data),
};
```

Dopuniti `import type` na vrhu fajla sa `Address` i `AuthUser` iz `@/types/domain`, te `SaveAddressRequest` i `UpdateProfileRequest` iz `./contracts`.

> `deleteAddress` gađa endpoint koji vraća `204` bez tijela. `apiFetch` pokušava `res.json()` i pada nazad na `payload = null`, a onda `!payload` obara zahtjev u grešku. Zato **u Task 5 mora ići i sitna izmjena `client.ts`**: kad je `res.status === 204`, vratiti `{ data: undefined as T, meta: null }` prije parsiranja tijela.

- [ ] **Step 4: Popraviti `apiFetch` za 204**

U `frontend/src/lib/api/client.ts`, odmah nakon uspješnog `fetch`-a i prije parsiranja tijela:

```ts
  // 204 nema tijelo; bez ovoga bi res.json() pao i uspješno brisanje bi izgledalo
  // kao greška.
  if (res.status === 204) return { data: undefined as T, meta: null };
```

- [ ] **Step 5: Dodati hook**

U `frontend/src/lib/api/hooks.ts`:

```ts
export function useMyAddresses(enabled: boolean) {
  return useQuery({
    queryKey: ["account", "addresses"],
    queryFn: () => accountApi.addresses(),
    enabled,
  });
}
```

Dopuniti import `accountApi` iz `./endpoints`.

- [ ] **Step 6: Napisati formu adrese**

`frontend/src/components/shop/address-form.tsx` — jedna forma za dodavanje i izmjenu, sa mapom kroz postojeći `AddressPicker`:

```tsx
"use client";

import { useState } from "react";
import { AddressPicker } from "@/components/shop/address-picker";
import { Button } from "@/components/ui/button";
import { Field, Input } from "@/components/ui/field";
import { useZones } from "@/lib/api/hooks";
import type { Address } from "@/types/domain";
import type { SaveAddressRequest } from "@/lib/api/contracts";

export function AddressForm({
  postojeca,
  onSave,
  onCancel,
  saving,
  greske,
}: {
  postojeca?: Address;
  onSave: (body: SaveAddressRequest) => void;
  onCancel: () => void;
  saving: boolean;
  greske: Record<string, string[]>;
}) {
  const zones = useZones();
  const [label, setLabel] = useState(postojeca?.label ?? "");
  const [address, setAddress] = useState(postojeca?.fullAddress ?? "");
  const [zoneId, setZoneId] = useState(postojeca?.deliveryZoneId ?? "");
  const [coords, setCoords] = useState<{ lat: number; lon: number } | null>(
    postojeca?.latitude != null && postojeca?.longitude != null
      ? { lat: postojeca.latitude, lon: postojeca.longitude }
      : null,
  );

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        onSave({
          label: label.trim() || undefined,
          fullAddress: address,
          latitude: coords?.lat,
          longitude: coords?.lon,
          deliveryZoneId: zoneId || undefined,
        });
      }}
      className="space-y-4 rounded-card border border-line bg-surface p-5"
    >
      <Field label="Naziv (opciono)">
        <Input value={label} onChange={setLabel} placeholder="Kuća, Posao…" />
      </Field>

      <Field label="Adresa" required error={greske["fullAddress"]?.[0]}>
        <AddressPicker value={address} onChange={setAddress} onCoordinates={setCoords} />
      </Field>

      <Field label="Zona dostave">
        <select
          value={zoneId}
          onChange={(e) => setZoneId(e.target.value)}
          className="w-full rounded-xl border border-line bg-sand px-3.5 py-2.5 text-sm"
        >
          <option value="">Bez zone</option>
          {zones.data
            ?.filter((z) => z.isActive)
            .map((z) => (
              <option key={z.id} value={z.id}>
                {z.name}
              </option>
            ))}
        </select>
      </Field>

      {!coords && (
        <p className="text-xs text-muted">
          Bez tačke na mapi vozač dobija samo tekst adrese. Kliknite na mapu da
          označite ulaz.
        </p>
      )}

      <div className="flex gap-2">
        <Button type="submit" disabled={saving || !address}>
          {saving ? "Čuvam…" : "Sačuvaj"}
        </Button>
        <Button type="button" variant="secondary" onClick={onCancel}>
          Otkaži
        </Button>
      </div>
    </form>
  );
}
```

> Prije pisanja provjeriti stvarni potpis `Field` i `Input` u `frontend/src/components/ui/field.tsx` i uskladiti propse (`label`, `required`, `error`, `value`, `onChange`) — gornji kod pretpostavlja `onChange` koji prima string, kao u `register/page.tsx:140`.

- [ ] **Step 7: Napisati stranicu**

`frontend/src/app/(shop)/account/page.tsx` sa dvije sekcije. Struktura:

```tsx
"use client";

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { MapPin, Pencil, Star, Trash2 } from "lucide-react";
import Link from "next/link";
import { AddressForm } from "@/components/shop/address-form";
import { Button, buttonVariants } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/empty-state";
import { Field, Input } from "@/components/ui/field";
import { LoadingBlock } from "@/components/ui/spinner";
import { useToast } from "@/components/ui/toast";
import { useAuth } from "@/lib/auth/auth-context";
import { useMyAddresses } from "@/lib/api/hooks";
import { accountApi } from "@/lib/api/endpoints";
import { ApiRequestError } from "@/lib/api/client";
import { cn } from "@/lib/utils";
import type { Address } from "@/types/domain";

export default function AccountPage() {
  const { user, loading, refresh } = useAuth();
  const addresses = useMyAddresses(!!user);
  const qc = useQueryClient();
  const { show } = useToast();
  const [uredjujem, setUredjujem] = useState<Address | "nova" | null>(null);
  const [greske, setGreske] = useState<Record<string, string[]>>({});

  const osvjezi = () => qc.invalidateQueries({ queryKey: ["account", "addresses"] });

  const sacuvaj = useMutation({
    mutationFn: (v: { id?: string; body: Parameters<typeof accountApi.createAddress>[0] }) =>
      v.id ? accountApi.updateAddress(v.id, v.body) : accountApi.createAddress(v.body),
    onSuccess: () => {
      setUredjujem(null);
      setGreske({});
      show("Adresa sačuvana");
      osvjezi();
    },
    onError: (e) => {
      if (e instanceof ApiRequestError) {
        setGreske(e.fieldErrors);
        if (Object.keys(e.fieldErrors).length === 0) show(e.message);
      }
    },
  });

  const obrisi = useMutation({
    mutationFn: (id: string) => accountApi.deleteAddress(id),
    onSuccess: () => { show("Adresa obrisana"); osvjezi(); },
  });

  const glavna = useMutation({
    mutationFn: (id: string) => accountApi.setDefaultAddress(id),
    onSuccess: () => { show("Glavna adresa promijenjena"); osvjezi(); },
  });

  if (loading) return <LoadingBlock />;
  if (!user) {
    return (
      <div className="mx-auto max-w-2xl px-4 py-12">
        <EmptyState
          title="Prijavite se da vidite svoj nalog"
          description="Podaci i adrese vezani su za vaš nalog."
          action={<Link href="/login" className={cn(buttonVariants())}>Prijava</Link>}
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl px-4 py-8">
      <h1 className="font-display text-3xl font-bold text-ink">Moj nalog</h1>
      {/* Sekcija 1: Podaci — ime i telefon izmjenjivi, e-mail zaključan.
          Sekcija 2: Adrese — spisak kartica + AddressForm.
          Detalji obje sekcije u koracima 8 i 9. */}
    </div>
  );
}
```

- [ ] **Step 8: Dopisati sekciju „Podaci"**

Unutar `AccountPage`, ispod naslova. Forma sa lokalnim stanjem za ime i telefon i mutacijom nad `accountApi.updateProfile`; nakon uspjeha pozvati `refresh()` iz `useAuth` da navbar i checkout vide novo ime. E-mail se prikazuje kao tekst uz rečenicu: „E-mail se ne može mijenjati jer je ujedno i vaše korisničko ime."

> Provjeriti da `useAuth()` zaista izlaže `refresh`. Ako ne izlaže, dodati ga u `auth-context.tsx` kao funkciju koja ponovo zove `authApi.me()` i postavlja korisnika — bez toga navbar poslije izmjene imena pokazuje staro ime do sljedeće prijave.

- [ ] **Step 9: Dopisati sekciju „Adrese"**

Spisak kartica; svaka nosi `label` (ili sam `fullAddress` ako nema oznake), adresu, ime zone, značku „Glavna" za `isDefault`, i dvije napomene kad treba:

- ako `latitude`/`longitude` nedostaju → „Bez tačke na mapi"
- ako `deliveryZoneId` postoji a `deliveryZoneActive` je `false` → „Zona više nije u ponudi"

Radnje po kartici: „Izmijeni" (otvara `AddressForm` sa `postojeca`), „Postavi kao glavnu" (skriveno za već glavnu), „Obriši". Iznad spiska dugme „Dodaj adresu" koje postavlja `uredjujem = "nova"`.

Brisanje mora tražiti potvrdu, ali **ne kroz `confirm()`** — koristiti postojeći obrazac potvrde u repozitoriju ako ga ima, inače dvostepeno dugme (drugi klik potvrđuje).

- [ ] **Step 10: Dodati link u navbar**

U `frontend/src/components/layout/shop-navbar.tsx`, u korisničkom meniju za prijavljenog korisnika, iznad „Moje narudžbe" (i u desktop i u mobilnom bloku):

```tsx
                  <Link
                    href="/account"
                    className={cn(
                      "flex items-center gap-2 rounded-full px-3 py-2 text-sm font-medium transition",
                      nadHerojem ? "text-cream hover:bg-white/10" : "text-ink hover:bg-sand",
                    )}
                  >
                    <User className="h-4 w-4" />
                    Moj nalog
                  </Link>
```

Postojeći link ka `/orders` koji trenutno nosi ikonu `User` i ime korisnika preimenovati u „Moje narudžbe" sa ikonom `Package`, da dvije stavke ne izgledaju isto.

- [ ] **Step 11: Provjeriti u pretraživaču**

```bash
cd frontend && npx tsc --noEmit && npm run lint
```

Zatim, uz pokrenut backend i frontend, prijaviti se kao `mujo@example.com` i na `http://localhost:3000/account`:
- dodati adresu sa pinom → pojavi se u spisku sa zonom
- postaviti drugu kao glavnu → značka se pomjeri, samo jedna je „Glavna"
- obrisati glavnu → značka pređe na drugu adresu
- promijeniti ime → navbar odmah pokazuje novo ime

- [ ] **Step 12: Commit**

```bash
git add frontend/src
git commit -m "feat: stranica Moj nalog sa adresarom i izmjenom podataka"
```

---

### Task 6: Checkout koristi adresar

**Files:**
- Modify: `frontend/src/components/shop/address-chooser.tsx`
- Modify: `frontend/src/app/(shop)/checkout/page.tsx`
- Modify: `frontend/src/lib/api/endpoints.ts` (brisanje `orderApi.addresses`)
- Modify: `frontend/src/lib/api/hooks.ts` (brisanje starog hooka za adrese)
- Modify: `backend/src/EPiljara.Application/Features/Orders/OrderQueries.cs` (brisanje `GetMyAddressesQuery`)
- Modify: `backend/src/EPiljara.API/Controllers/OrdersController.cs` (brisanje `orders/addresses`)

**Interfaces:**
- Consumes: tip `Address` i `useMyAddresses` iz Task 5.
- Produces: `AddressChooser` koji prima `savedAddresses: Address[]` i javlja izbor kroz `onSelect(address: Address | null)`.

- [ ] **Step 1: Prepisati `AddressChooser` da radi sa `Address[]`**

Ključna razlika prema sadašnjem ponašanju: izbor sačuvane adrese sad **nosi svoju tačku i zonu**, umjesto da ih briše. Zamijeniti potpis i logiku:

```tsx
export function AddressChooser({
  value,
  onChange,
  savedAddresses,
  onSelect,
  onLocalities,
  onCoordinates,
}: {
  value: string;
  onChange: (value: string) => void;
  savedAddresses: Address[];
  /** Sačuvana adresa nosi svoju tačku i zonu; `null` znači "unosim novu". */
  onSelect: (address: Address | null) => void;
  onLocalities?: (localities: string[]) => void;
  onCoordinates?: (coords: { lat: number; lon: number } | null) => void;
}) {
```

U spisku, klik na sačuvanu adresu poziva `onChange(a.fullAddress)` i `onSelect(a)`. Dugme „Nova adresa" poziva `onSelect(null)` i otvara `AddressPicker` kao i sada.

- [ ] **Step 2: Prevesti checkout na adresar**

U `frontend/src/app/(shop)/checkout/page.tsx`:

Zamijeniti stari izvor adresa novim hookom:

```tsx
  const addresses = useMyAddresses(!!user);
  const savedAddresses = addresses.data ?? [];
```

Zamijeniti prefill efekat (sada na linijama 91–94):

```tsx
  // Predložena adresa je glavna iz adresara; ona nosi i svoju zonu i tačku.
  useEffect(() => {
    const glavna = savedAddresses.find((a) => a.isDefault) ?? savedAddresses[0];
    if (!glavna) return;
    setForm((f) => (f.address ? f : { ...f, address: glavna.fullAddress }));
    primijeniAdresu(glavna);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [addresses.data]);
```

I dodati pomoćnu funkciju koja jedina zna kako se sačuvana adresa prenosi u formu:

```tsx
  // Jedno mjesto koje prevodi sačuvanu adresu u stanje forme. Zona se preskače ako
  // je u međuvremenu ugašena — CreateOrderValidator bi takvu narudžbu odbio sa
  // `invalid_zone` tek pri slanju, a kupac ne bi znao zašto.
  const primijeniAdresu = useCallback((a: Address | null) => {
    if (!a) return;
    setCoords(
      a.latitude != null && a.longitude != null ? { lat: a.latitude, lon: a.longitude } : null,
    );
    setForm((f) => ({
      ...f,
      address: a.fullAddress,
      zoneId: a.deliveryZoneId && a.deliveryZoneActive ? a.deliveryZoneId : f.zoneId,
    }));
  }, []);
```

Proslijediti `onSelect={primijeniAdresu}` u `AddressChooser` (linija oko 222).

Iz prefill efekta na liniji 86 ukloniti `zoneId: f.zoneId || user.deliveryZoneId || ""` — zona sad dolazi iz adrese, a `user.deliveryZoneId` nestaje u Task 7.

- [ ] **Step 3: Obrisati stari put**

Backend — iz `OrderQueries.cs` obrisati `GetMyAddressesQuery` i `GetMyAddressesHandler`; iz `OrdersController.cs` obrisati akciju `Addresses()`.

Frontend — iz `endpoints.ts` obrisati `orderApi.addresses`; iz `hooks.ts` obrisati hook sa `queryKey: ["orders", "addresses"]`.

- [ ] **Step 4: Provjeriti da nigdje nije ostao poziv**

```bash
cd frontend && grep -rn "orders/addresses\|orderApi.addresses" src/ ; echo "---"
cd ../backend && grep -rn "GetMyAddresses" src/ --include=*.cs | grep -v obj
```

Očekivano: oba pretraživanja bez rezultata.

- [ ] **Step 5: Provjeriti tok od kraja do kraja**

Ovo je razlog cijelog posla i mora se vidjeti u cjelini:

1. Na `/account` dodati adresu **sa pinom** i zonom.
2. Ubaciti proizvod u korpu, otvoriti `/checkout` — adresa je predložena, zona popunjena.
3. Poslati narudžbu.
4. Provjeriti da je tačka stigla:

```bash
docker exec epiljara-postgres psql -U epiljara -d epiljara -c 'SELECT "Code", "DeliveryAddress", "Latitude", "Longitude" FROM "Orders" ORDER BY "CreatedAt" DESC LIMIT 1;'
```

Očekivano: `Latitude` i `Longitude` **nisu NULL**. Prije ove izmjene bili bi NULL kad god se bira sačuvana adresa — to je bug koji ovaj zadatak zatvara.

5. Obrisati testnu narudžbu nakon provjere.

- [ ] **Step 6: Commit**

```bash
cd frontend && npx tsc --noEmit && npm run lint
cd ../backend && dotnet build -v q --nologo
git add frontend/src backend/src
git commit -m "feat: checkout bira iz adresara, sacuvana adresa nosi zonu i tacku"
```

---

### Task 7: Brisanje `Users.Address` i `Users.DeliveryZoneId`

Radi se **zadnji**, kad novi put već radi. Do ovog trenutka kolone su bile mrtve ali prisutne, pa je svaki raniji zadatak mogao biti vraćen bez gubitka podataka.

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/User.cs`
- Modify: `backend/src/EPiljara.Application/DTOs/AuthDtos.cs`
- Modify: `backend/src/EPiljara.Application/Mapping/MappingExtensions.cs:125-126`
- Modify: `backend/src/EPiljara.Application/Features/Auth/AuthCommands.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/DbSeeder.cs`
- Modify: `frontend/src/types/domain.ts` (`AuthUser`)
- Modify: `frontend/src/app/(shop)/register/page.tsx`
- Create: `backend/src/EPiljara.Infrastructure/Persistence/Migrations/<timestamp>_DropUserAddress.cs`

- [ ] **Step 1: Registracija pravi adresu umjesto polja na korisniku**

U `RegisterHandler.Handle`, ukloniti `Address` i `DeliveryZoneId` iz inicijalizacije `new User { … }`, pa nakon `db.Users.Add(user)` dodati:

```csharp
        // Adresa uneseta pri registraciji postaje prva, glavna adresa u adresaru.
        // Bez pina — registracija nema mapu, a izmišljena tačka bi poslala vozača
        // na pogrešno mjesto.
        if (!string.IsNullOrWhiteSpace(cmd.Address))
        {
            db.Addresses.Add(new Address
            {
                UserId = user.Id,
                FullAddress = cmd.Address.Trim(),
                DeliveryZoneId = cmd.DeliveryZoneId,
                IsDefault = true,
            });
        }
```

`RegisterCommand` zadržava polja `Address` i `DeliveryZoneId` — forma registracije se ne mijenja, mijenja se samo gdje podatak završava.

- [ ] **Step 2: Ukloniti polja iz entiteta, DTO-a i mapiranja**

- `User.cs`: obrisati `public string? Address` i `public Guid? DeliveryZoneId`.
- `AuthDtos.cs`: obrisati `Address` i `DeliveryZoneId` iz `AuthUserDto`.
- `MappingExtensions.cs`: obrisati linije `Address = u.Address,` i `DeliveryZoneId = u.DeliveryZoneId,` iz `ToAuthDto`.
- `AppDbContext.cs`: ako `Entity<User>` konfiguracija sadrži `HasOne(...DeliveryZone...)` za korisnika, obrisati je.
- `DbSeeder.cs`: ukloniti postavljanje tih polja; ako seeder pravi kupce sa adresom, umjesto toga dodati `Address` red sa `IsDefault = true`.

- [ ] **Step 3: Generisati i provjeriti migraciju**

```bash
cd backend
dotnet dotnet-ef migrations add DropUserAddress -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations
```

U generisanoj migraciji provjeriti da `Up` sadrži `DropColumn` za obje kolone. U `Down` dopisati vraćanje glavne adrese, jer EF ne zna odakle bi je uzeo:

```csharp
            migrationBuilder.Sql("""
                UPDATE "Users" u
                SET "Address" = a."FullAddress", "DeliveryZoneId" = a."DeliveryZoneId"
                FROM "Addresses" a
                WHERE a."UserId" = u."Id" AND a."IsDefault" = TRUE;
                """);
```

> `Down` vraća samo glavnu adresu; ostale se pri vraćanju gube. To je prihvaćeno — `Down` postoji za rollback u razvoju, ne kao garancija očuvanja podataka.

- [ ] **Step 4: Pustiti migraciju**

```bash
dotnet dotnet-ef database update -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure
docker exec epiljara-postgres psql -U epiljara -d epiljara -c '\d "Users"'
```

Očekivano: kolona `Address` i `DeliveryZoneId` više nema.

- [ ] **Step 5: Uskladiti frontend**

- `frontend/src/types/domain.ts`: obrisati `address?` i `deliveryZoneId?` iz `AuthUser`.
- `frontend/src/app/(shop)/register/page.tsx`: ostaje kako jeste — i dalje šalje `address` u `RegisterRequest`.
- Provjeriti da nigdje nije ostalo čitanje:

```bash
cd frontend && grep -rn "user?.address\|user\.deliveryZoneId" src/
```

Očekivano: bez rezultata.

- [ ] **Step 6: Provjeriti registraciju od kraja do kraja**

Registrovati novi nalog kroz `/register` sa upisanom adresom, potvrditi e-mail preko Mailpita (`http://localhost:8025`), prijaviti se i otvoriti `/account`.

Očekivano: adresa iz registracije je već u adresaru i označena kao glavna.

- [ ] **Step 7: Commit**

```bash
cd backend && dotnet build -v q --nologo
cd ../frontend && npx tsc --noEmit && npm run lint
git add backend/src frontend/src
git commit -m "refactor: adresa korisnika zivi samo u adresaru"
```

---

## Samoprovjera plana

Provjereno prema specu:

| Zahtjev iz spec-a | Zadatak |
|---|---|
| Entitet `Address` sa svim poljima | 1 |
| `DbSet` i konfiguracija, indeks na `UserId` | 1 |
| Prenos postojećih adresa | 1 |
| Nullable koordinate | 1 (entitet), 3 (validacija), 5 (napomena u UI) |
| Tačno jedna glavna, u jednom `SaveChanges` | 3 |
| Brisanje glavne prebacuje oznaku | 3 |
| Granica od 10 adresa | 3 |
| Svih šest endpointa | 2 (GET), 3 (write), 4 (profil) |
| Provjera vlasništva → 403 | 3 |
| Pravila za tačku ista kao u narudžbi, greške pod jednim ključem | 3 |
| Brisanje ne dira istoriju | 1 (bez FK ka `Order`), 6 (provjera) |
| Adresa bez zone se nudi, ne popunjava zonu | 6 |
| Neaktivna zona se preskače u prefillu | 2 (`DeliveryZoneActive`), 6 (prefill) |
| Stranica `/account`, dvije sekcije | 5 |
| Link u navbaru | 5 |
| `AddressChooser` prima `Address[]` | 6 |
| Brisanje `GetMyAddressesQuery` i `/orders/addresses` | 6 |
| Migracija u tri koraka | 1 (tabela + prenos), 7 (brisanje kolona) |
| `RegisterCommand` pravi adresu | 7 |
| `MeDto` i `AuthUser` gube polja | 7 |
| Provjera: pin stiže do narudžbe i mape | 6 |

Dvije stvari koje su se pojavile tek pri pisanju plana i nisu bile u specu:

- **`apiFetch` ne podnosi `204`** — `DELETE` bi izgledao kao greška. Popravka je u Task 5, korak 4.
- **`useAuth` možda nema `refresh`** — bez njega navbar poslije izmjene imena pokazuje staro. Provjera i po potrebi dodavanje su u Task 5, korak 8.
