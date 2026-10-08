# P2a — Moj nalog: plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kupac dobija `/moj-nalog` sa četiri sekcije — Pregled, Lični podaci, Adrese, Narudžbe — i konačno može promijeniti svoje ime i telefon i voditi adresar koji backend već ima.

**Architecture:** Ljuska su ugnježdene rute pod `app/(shop)/moj-nalog/`, po uzoru na `app/admin/layout.tsx`: jedan `layout.tsx` drži stražu i navigaciju, sekcije su prave podrute. Backend dobija tačno jedan nov endpoint (`PUT /account/profile`); adresar je već kompletan i samo se poziva.

**Tech Stack:** ASP.NET Core 9, EF Core 9 + PostgreSQL, MediatR (CQRS), FluentValidation, xUnit + SQLite in-memory; Next.js 15 (App Router), TypeScript, Tailwind v4, TanStack Query.

**Spec:** `docs/superpowers/specs/2026-10-08-p2a-moj-nalog-design.md`

## Global Constraints

- **Grana:** `moj-nalog-p2a`, izvedena iz `redizajn-opcija3`.
- **Jezik:** svi korisnički tekstovi, poruke grešaka i XML doc komentari na **srpskom, ijekavica**. Imena komandi i članova takođe na srpskom.
- **Backend komande iz `backend/`, frontend iz `frontend/`.**
- Svaki endpoint vraća kovertu `{ data, error, meta }` preko `Envelope`/`EnvelopeList` iz `ApiControllerBase`.
- Domenski izuzeci se bacaju, ne sastavljaju: `DomainException` → 400, `NotFoundException` → 404, `ForbiddenException` → 403, `ConflictException` → 409. **Svaki nosi specifičan `code` kao drugi argument** — middleware klijentu šalje `ex.Code`, nikad poruku. (`NotFoundException` je izuzetak: `sealed`, tvrdo kodira `not_found`.)
- MediatR i FluentValidation se otkrivaju skeniranjem assemblyja — **nema registracije po handleru**.
- Handler uzima prijavljenog korisnika iz `ICurrentUser` (`Guid? Id`, `bool IsAuthenticated`), kao `CreateAddressHandler`.
- **API izostavlja null polja** — nullable backend polje je na žici *odsutno*, pa TS tipovi koriste `?:`, ne `| null`.
- Ekrani zovu isključivo `frontend/src/lib/api/endpoints.ts`. Nikad `fetch`, nikad `apiFetch` direktno. Tipovi domena u `types/domain.ts`, oblici zahtjeva u `lib/api/contracts.ts`.
- Mock pod `frontend/src/server/mock/` se **ne dopunjava**.
- Frontend provjera: `npx tsc --noEmit`, `npm run lint` **i `npm run build`**. Build nije opcion — `useSearchParams()` bez `<Suspense>` prolazi prva dva a obara treći.
- **Ne pokretati `npm run build` dok dev server radi** — dijele `.next` i build obori sve rute. Ako dev server radi, prvo ga ugasiti.
- Granice: ime najviše **160** znakova (prati `AppDbContext.cs:135`), telefon najviše **40**.

---

### Task 1: Backend — `PUT /account/profile`

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Account/ProfileCommands.cs`
- Modify: `backend/src/EPiljara.API/Controllers/AccountController.cs`
- Test: `backend/tests/EPiljara.Application.Tests/ProfileCommandsTests.cs`

**Interfaces:**
- Consumes: `ICurrentUser` (`Guid? Id`), `IAppDbContext.Users`, `User.ToAuthDto()` (`MappingExtensions.cs:132`), `AuthUserDto`
- Produces: `record AzurirajProfilCommand(string Ime, string Telefon) : IRequest<AuthUserDto>`; endpoint `PUT /account/profile`

- [ ] **Step 1: Napiši test koji pada**

`backend/tests/EPiljara.Application.Tests/ProfileCommandsTests.cs`:

```csharp
using EPiljara.Application.Features.Account;
using EPiljara.Domain.Common;
using EPiljara.Domain.Entities;
using EPiljara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EPiljara.Application.Tests;

public class ProfileCommandsTests
{
    private static User Korisnik(TestDb t, string email = "mujo@test.local")
    {
        var u = new User
        {
            Name = "Mujo Mujić",
            Email = email,
            Phone = "065111222",
            PasswordHash = "x",
            Role = UserRole.Customer,
        };
        t.Db.Users.Add(u);
        t.Db.SaveChanges();
        return u;
    }

    [Fact]
    public async Task Mijenja_ime_i_telefon_i_vraca_azuriran_dto()
    {
        using var t = new TestDb();
        var u = Korisnik(t);

        var dto = await new AzurirajProfilHandler(t.Db, new FakeCurrentUser(u.Id))
            .Handle(new AzurirajProfilCommand("Haso Hasić", "065999888"), default);

        Assert.Equal("Haso Hasić", dto.Name);
        Assert.Equal("065999888", dto.Phone);

        var iz_baze = await t.Db.Users.AsNoTracking().SingleAsync(x => x.Id == u.Id);
        Assert.Equal("Haso Hasić", iz_baze.Name);
        Assert.Equal("065999888", iz_baze.Phone);
    }

    [Fact]
    public async Task Ne_dira_email_ni_ulogu()
    {
        using var t = new TestDb();
        var u = Korisnik(t);

        await new AzurirajProfilHandler(t.Db, new FakeCurrentUser(u.Id))
            .Handle(new AzurirajProfilCommand("Haso Hasić", "065999888"), default);

        var iz_baze = await t.Db.Users.AsNoTracking().SingleAsync(x => x.Id == u.Id);
        Assert.Equal("mujo@test.local", iz_baze.Email);
        Assert.Equal(UserRole.Customer, iz_baze.Role);
    }

    [Fact]
    public async Task Dira_samo_svog_korisnika()
    {
        using var t = new TestDb();
        var moj = Korisnik(t, "moj@test.local");
        var tudji = Korisnik(t, "tudji@test.local");

        await new AzurirajProfilHandler(t.Db, new FakeCurrentUser(moj.Id))
            .Handle(new AzurirajProfilCommand("Novo Ime", "065000000"), default);

        var drugi = await t.Db.Users.AsNoTracking().SingleAsync(x => x.Id == tudji.Id);
        Assert.Equal("Mujo Mujić", drugi.Name);
        Assert.Equal("065111222", drugi.Phone);
    }

    [Fact]
    public async Task Nepostojeci_korisnik_baca_not_found()
    {
        using var t = new TestDb();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new AzurirajProfilHandler(t.Db, new FakeCurrentUser(Guid.NewGuid()))
                .Handle(new AzurirajProfilCommand("Ime", "065"), default));
    }

    [Theory]
    [InlineData("", "065111222")]
    [InlineData("   ", "065111222")]
    [InlineData("Mujo", "")]
    [InlineData("Mujo", "   ")]
    public void Prazno_ime_ili_telefon_ne_prolaze_validaciju(string ime, string telefon)
    {
        var rezultat = new AzurirajProfilValidator()
            .Validate(new AzurirajProfilCommand(ime, telefon));
        Assert.False(rezultat.IsValid);
    }

    [Fact]
    public void Predugo_ime_ne_prolazi_validaciju()
    {
        var rezultat = new AzurirajProfilValidator()
            .Validate(new AzurirajProfilCommand(new string('a', 161), "065111222"));
        Assert.False(rezultat.IsValid);
    }

    [Fact]
    public void Ispravan_unos_prolazi_validaciju()
    {
        var rezultat = new AzurirajProfilValidator()
            .Validate(new AzurirajProfilCommand("Mujo Mujić", "065111222"));
        Assert.True(rezultat.IsValid);
    }
}
```

- [ ] **Step 2: Pokreni test i potvrdi da pada**

Run: `dotnet test --filter ProfileCommandsTests` (iz `backend/`)
Expected: FAIL — kompilacija puca, `AzurirajProfilCommand` ne postoji.

- [ ] **Step 3: Napiši komandu, validator i handler**

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
/// Kupac mijenja ono malo što smije o sebi: ime i telefon. E-mail je korisničko
/// ime i potvrđena adresa, lozinka ne postoji za one koji ulaze preko Googlea —
/// oboje je zaseban posao. Vraća isti AuthUserDto koji auth/me daje, da klijent
/// osvježi svoj kontekst iz odgovora umjesto drugim pozivom.
/// </summary>
public record AzurirajProfilCommand(string Ime, string Telefon) : IRequest<AuthUserDto>;

public class AzurirajProfilValidator : AbstractValidator<AzurirajProfilCommand>
{
    /// <summary>Prati AppDbContext: User.Name je varchar(160).</summary>
    public const int MaxIme = 160;

    /// <summary>Kolona nema granicu; validator je uvodi da predug unos ne stigne do baze.</summary>
    public const int MaxTelefon = 40;

    public AzurirajProfilValidator()
    {
        RuleFor(x => x.Ime)
            .NotEmpty().WithMessage("Ime je obavezno.")
            .MaximumLength(MaxIme).WithMessage($"Ime može imati najviše {MaxIme} znakova.");

        RuleFor(x => x.Telefon)
            .NotEmpty().WithMessage("Telefon je obavezan.")
            .MaximumLength(MaxTelefon).WithMessage($"Telefon može imati najviše {MaxTelefon} znakova.");
    }
}

public class AzurirajProfilHandler(IAppDbContext db, ICurrentUser current)
    : IRequestHandler<AzurirajProfilCommand, AuthUserDto>
{
    public async Task<AuthUserDto> Handle(AzurirajProfilCommand c, CancellationToken ct)
    {
        var korisnik = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id, ct)
            ?? throw new NotFoundException("Korisnik nije pronađen.");

        korisnik.Name = c.Ime.Trim();
        korisnik.Phone = c.Telefon.Trim();

        await db.SaveChangesAsync(ct);
        return korisnik.ToAuthDto();
    }
}
```

- [ ] **Step 4: Dodaj rutu u `AccountController`**

U `backend/src/EPiljara.API/Controllers/AccountController.cs`, kao prvu akciju (profil prije adresara, jer je „ko sam ja" uži pojam od „gdje stanujem"):

```csharp
    [HttpPut("account/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] AzurirajProfilCommand command) =>
        Envelope(await Mediator.Send(command));
```

Kontroler je već `[Authorize]`, pa ruta nasljeđuje zaštitu.

- [ ] **Step 5: Pokreni testove**

Run: `dotnet test --filter ProfileCommandsTests` pa `dotnet test`
Expected: PASS — deset novih slučajeva (sedam metoda, od kojih jedan `[Theory]` nosi četiri `[InlineData]`), i svi postojeći i dalje prolaze.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "feat(nalog): PUT /account/profile za ime i telefon

Prvi endpoint kojim kupac uopšte može promijeniti nešto o sebi — do sada je
auth/me samo čitao. Validator uvodi i granicu dužine imena koju registracija
nema a kolona ima, pa predug unos daje 400 umjesto 500 sa baze."
```

---

### Task 2: Frontend — tipovi, `accountApi`, i profil u auth kontekstu

**Files:**
- Modify: `frontend/src/types/domain.ts`
- Modify: `frontend/src/lib/api/contracts.ts`
- Modify: `frontend/src/lib/api/endpoints.ts`
- Modify: `frontend/src/lib/auth/auth-context.tsx`

**Interfaces:**
- Consumes: `AddressDto` i `AuthUserDto` sa backenda, postojeći `apiFetch`
- Produces: tip `Address`; `accountApi` sa `adrese`, `dodajAdresu`, `izmijeniAdresu`, `obrisiAdresu`, `postaviGlavnu`, `azurirajProfil`; `useAuth().azurirajProfil(body)`

- [ ] **Step 1: Dodaj tip `Address` u `domain.ts`**

Preslikava `AccountDtos.cs:3` jedan na jedan. Nullable backend polja su na žici **odsutna**, pa `?:`:

```ts
export interface Address {
  id: string;
  label?: string;
  fullAddress: string;
  latitude?: number;
  longitude?: number;
  deliveryZoneId?: string;
  deliveryZoneName?: string;
  /** Zona se može ugasiti nakon što je adresa sačuvana — klijent to mora znati. */
  deliveryZoneActive: boolean;
  isDefault: boolean;
}
```

- [ ] **Step 2: Dodaj oblike zahtjeva u `contracts.ts`**

```ts
export interface AddressInput {
  label?: string;
  fullAddress: string;
  latitude?: number;
  longitude?: number;
  deliveryZoneId?: string;
}

export interface ProfileInput {
  ime: string;
  telefon: string;
}
```

- [ ] **Step 3: Dodaj `accountApi` u `endpoints.ts`**

Prvo pročitaj `lib/api/client.ts` i potvrdi potpis `apiFetch` — ne mijenjaj ga, prilagodi pozive njemu. Zatim, uz ostale `*Api` blokove:

```ts
/* ---------- Nalog ---------- */

export const accountApi = {
  adrese: () =>
    apiFetch<Address[]>("/account/addresses").then((r) => r.data),

  dodajAdresu: (body: AddressInput) =>
    apiFetch<Address>("/account/addresses", { method: "POST", body }).then((r) => r.data),

  izmijeniAdresu: (id: string, body: AddressInput) =>
    apiFetch<Address>(`/account/addresses/${id}`, { method: "PUT", body }).then((r) => r.data),

  obrisiAdresu: (id: string) =>
    apiFetch<void>(`/account/addresses/${id}`, { method: "DELETE" }).then((r) => r.data),

  postaviGlavnu: (id: string) =>
    apiFetch<Address>(`/account/addresses/${id}/default`, { method: "POST" }).then((r) => r.data),

  azurirajProfil: (body: ProfileInput) =>
    apiFetch<AuthUser>("/account/profile", { method: "PUT", body }).then((r) => r.data),
};
```

- [ ] **Step 4: Izloži `azurirajProfil` kroz auth kontekst**

`AuthContextValue` (`auth-context.tsx:16-28`) nema načina da spolja osvježi `user`, a poslije izmjene profila zaglavlje i dalje pokazuje staro ime. Dodaj metodu koja poziva API i upiše rezultat — isti obrazac kao `login`/`confirmEmail`, koji već rade `setUser(result.user)`:

```ts
  /** Mijenja ime i telefon i odmah osvježava kontekst, da zaglavlje ne pokazuje staro ime. */
  azurirajProfil: (body: ProfileInput) => Promise<AuthUser>;
```

U `AuthProvider`:

```ts
  const azurirajProfil = useCallback(async (body: ProfileInput) => {
    const azuriran = await accountApi.azurirajProfil(body);
    setUser(azuriran);
    return azuriran;
  }, []);
```

Dodaj je u `useMemo` vrijednost konteksta i u listu zavisnosti, pored postojećih metoda.

Uvoz `accountApi` u `auth-context.tsx` **ne pravi ciklus** — provjereno: `auth-context.tsx:13`
već uvozi `authApi` iz `endpoints.ts`, a `endpoints.ts` i `client.ts` ne uvoze ništa iz
`lib/auth/`. Smjer je već uspostavljen, samo ga slijediš.

- [ ] **Step 5: Provjeri tipove**

Run (iz `frontend/`): `npx tsc --noEmit && npm run lint`
Expected: oba čista. Build se ne pokreće — ovaj task ne dodaje rutu.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/types frontend/src/lib
git commit -m "feat(nalog): accountApi i azurirajProfil u auth kontekstu

Adresar je na backendu od septembra i nijedan poziv mu nikad nije došao iz UI-ja;
ovo je prvi. azurirajProfil upisuje odgovor nazad u kontekst da zaglavlje ne
pokazuje staro ime poslije izmjene."
```

---

### Task 3: Ljuska `/moj-nalog` — layout, straža, navigacija, Pregled

**Files:**
- Create: `frontend/src/app/(shop)/moj-nalog/layout.tsx`
- Create: `frontend/src/app/(shop)/moj-nalog/page.tsx`
- Create: `frontend/src/components/nalog/nalog-nav.tsx`

**Interfaces:**
- Consumes: `useAuth()` (`user`, `loading`), postojeće `Button`/`buttonVariants`, `cn`
- Produces: ruta `/moj-nalog`; `<NalogSidebar />` i `<NalogMobileBar />`; `NALOG_STAVKE` lista

- [ ] **Step 1: Pročitaj uzor**

Run: `sed -n '1,60p' frontend/src/app/admin/layout.tsx` i `sed -n '25,60p' frontend/src/components/admin/admin-nav.tsx`

`ITEMS` je `NavItem[]` sa `{ href, label, icon }` (`admin-nav.tsx:34-40`), a `AdminSidebar` i `AdminMobileBar` mapiraju isti niz — jedna izmjena ažurira obje površine. Preslikaj taj oblik; **ne kopiraj datoteku**, nego je prati.

- [ ] **Step 2: Napravi navigaciju**

`frontend/src/components/nalog/nalog-nav.tsx` — ista struktura kao `admin-nav.tsx`, sa `usePathname()` za aktivnu stavku. Četiri žive stavke plus dvije zaključane:

```tsx
type NalogStavka = {
  href: string;
  label: string;
  icon: LucideIcon;
  /** Stiže u kasnijem potprojektu — prikazuje se, ali ne vodi nigdje. */
  uskoro?: boolean;
};

const STAVKE: NalogStavka[] = [
  { href: "/moj-nalog", label: "Pregled", icon: LayoutDashboard },
  { href: "/moj-nalog/podaci", label: "Lični podaci", icon: User },
  { href: "/moj-nalog/adrese", label: "Adrese", icon: MapPin },
  { href: "/moj-nalog/narudzbe", label: "Narudžbe", icon: ShoppingBag },
  { href: "/moj-nalog/korpica", label: "Moja korpica", icon: ShoppingBasket, uskoro: true },
  { href: "/moj-nalog/pretplate", label: "Pretplate", icon: RefreshCw, uskoro: true },
];
```

Zaključana stavka se renderuje kao `<span>`, ne `<Link>`, sa natpisom „uskoro" i prigušenom bojom — mora izgledati kao najava, ne kao pokvaren link.

- [ ] **Step 3: Napravi layout sa stražom**

`frontend/src/app/(shop)/moj-nalog/layout.tsx`. Straža stoji **ovdje, jednom** — ne u četiri stranice. Neprijavljenom se nudi prijava, bez preusmjeravanja, kao `orders/page.tsx:26` danas:

```tsx
"use client";

import { useAuth } from "@/lib/auth/auth-context";
import { NalogSidebar, NalogMobileBar } from "@/components/nalog/nalog-nav";
import { LoadingBlock } from "@/components/ui/loading-block";
import { EmptyState } from "@/components/ui/empty-state";
import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import Link from "next/link";

export default function NalogLayout({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();

  if (loading) return <LoadingBlock />;

  if (!user) {
    return (
      <EmptyState
        title="Prijavi se da vidiš svoj nalog"
        description="Tvoji podaci, adrese i narudžbe su na jednom mjestu — čim se prijaviš."
      >
        <Link href="/login" className={cn(buttonVariants())}>
          Prijavi se
        </Link>
      </EmptyState>
    );
  }

  return (
    <div className="mx-auto flex w-full max-w-6xl gap-8 px-4 py-8">
      <NalogSidebar />
      <main className="min-w-0 flex-1">{children}</main>
      <NalogMobileBar />
    </div>
  );
}
```

Prilagodi `LoadingBlock`/`EmptyState` njihovim stvarnim potpisima — pročitaj `components/ui/` prije pisanja; ako `EmptyState` ne prima djecu, stavi dugme pored njega.

- [ ] **Step 4: Napravi Pregled**

`frontend/src/app/(shop)/moj-nalog/page.tsx` — mreža pločica koje vode na radnje, **ne forma**. Četiri žive: „Izmijeni podatke" → `/moj-nalog/podaci`, „Moje adrese" → `/moj-nalog/adrese`, „Moje narudžbe" → `/moj-nalog/narudzbe`, „Korpice u ponudi" → `/korpice`. Dvije prigušene sa natpisom „uskoro": „Izmjena sadržaja korpice" i „Pretplate".

Svaka pločica: ikona, naslov, jedna rečenica šta tu radi. Koristi `rounded-card` i tokene shopa kao ostali ekrani.

- [ ] **Step 5: Provjeri**

Run (iz `frontend/`): `npx tsc --noEmit && npm run lint`

Zatim, sa pokrenutim stekom, otvori `http://localhost:3000/moj-nalog` prijavljen i odjavljen. Prijavljen vidi sidebar i pločice; odjavljen vidi poziv na prijavu, a ne grešku.

**Ne pokretati `npm run build`** dok dev server radi.

- [ ] **Step 6: Commit**

```bash
git add frontend/
git commit -m "feat(nalog): ljuska /moj-nalog sa stražom i pregledom

Ugnježdene rute po uzoru na app/admin/layout.tsx. Straža je u layoutu, jednom,
umjesto u svakoj sekciji. Korpica i Pretplate stoje kao zaključane stavke —
P2b i P3 se ukopčavaju bez prekrajanja ljuske."
```

---

### Task 4: Sekcija „Lični podaci"

**Files:**
- Create: `frontend/src/app/(shop)/moj-nalog/podaci/page.tsx`

**Interfaces:**
- Consumes: `useAuth()` (`user`, `azurirajProfil`), `ApiRequestError` za greške polja
- Produces: ruta `/moj-nalog/podaci`

- [ ] **Step 1: Pročitaj kako postojeća forma prikazuje greške polja**

Run: `grep -rn "fieldErrors" frontend/src --include=*.tsx | head -5`

Backend šalje ključeve validacije kao camelCase pune putanje svojstva (`Ime` → `ime`), i forme čitaju `err.fieldErrors["ime"]`. Prati taj obrazac; ne izmišljaj drugi.

- [ ] **Step 2: Napravi stranicu**

Dvije kartice, odozgo nadolje.

**Kartica 1 — Lični podaci.** Polja `Ime i prezime` i `Broj telefona`, oba obavezna, predpopunjena iz `user`. Ispod telefona helper tekst: „Na ovaj broj te zove vozač kad stigne." Dugme „Sačuvaj" zove `azurirajProfil({ ime, telefon })`, prikazuje toast na uspjeh i inline greške polja na 400.

Dugme je onemogućeno dok se ništa nije promijenilo i dok traje slanje — forma koja dozvoljava da se prazan zahtjev pošalje pravi buku bez razloga.

**Kartica 2 — Podaci za prijavu.** `E-mail` i `Lozinka`, oboje **samo za čitanje**. E-mail prikazuje `user.email`; lozinka prikazuje `••••••••`. Ispod obje, jedna rečenica: „E-mail je i tvoje korisničko ime, pa se mijenja uz ponovnu potvrdu — javi nam se na kontakt ako treba."

Prikazati ih sivo je poštenije nego izostaviti ih: kupac traži gdje se mijenja lozinka i mora dobiti odgovor, makar bio „ne ovdje".

- [ ] **Step 3: Provjeri**

Run: `npx tsc --noEmit && npm run lint`

Uz pokrenut stek: promijeni ime, sačuvaj, i potvrdi da se **zaglavlje odmah osvježi** (to dokazuje da `azurirajProfil` upisuje u kontekst). Pošalji prazno ime i potvrdi da se greška pojavi uz polje, ne kao sirovi toast.

- [ ] **Step 4: Commit**

```bash
git add frontend/
git commit -m "feat(nalog): sekcija Lični podaci

Ime i telefon se mijenjaju; e-mail i lozinka stoje sivo sa objašnjenjem zašto
ne ovdje. Kupac koji traži gdje mijenja lozinku mora dobiti odgovor, makar
bio 'ne ovdje'."
```

---

### Task 5: Sekcija „Adrese"

**Files:**
- Create: `frontend/src/app/(shop)/moj-nalog/adrese/page.tsx`
- Create: `frontend/src/components/nalog/adresa-forma.tsx`

**Interfaces:**
- Consumes: `accountApi` (svih pet adresnih metoda), `Address`, `AddressInput`, `deliveryApi` za zone
- Produces: ruta `/moj-nalog/adrese`

- [ ] **Step 1: Pročitaj backend pravila koja UI mora poštovati**

Run: `sed -n '100,135p' backend/src/EPiljara.Application/Features/Account/AddressCommands.cs`

Tri pravila su već na backendu i UI ih mora odražavati, ne dupli­rati: najviše **10** adresa po kupcu (`MaxAdresa`), tačno jedna glavna (jedinstveni indeks sa filterom), i `DeliveryZoneId` mora postojati ako je poslat.

- [ ] **Step 2: Napravi formu adrese**

`frontend/src/components/nalog/adresa-forma.tsx` — panel koji klizi s desne strane, isti obrazac kao `components/admin/product-form.tsx` (pročitaj ga prvo). Polja: `Naziv` (opciono, npr. „Kuća"), `Adresa` (obavezno), `Zona dostave` (izbor iz `deliveryApi`).

Koordinate se u ovom tasku **ne unose ručno** — `latitude`/`longitude` se prosljeđuju samo ako ih forma već ima. Birač na mapi je zaseban posao i nije u specu.

- [ ] **Step 3: Napravi stranicu**

Iznad liste, uputstvo u tri numerisane stavke i napomena — oblik preuzet iz analize uzora jer stvarno pomaže:

> 1. Adresu svake dostave biraš posebno, na checkoutu.
> 2. U svakom trenutku možeš izmijeniti podatke svake adrese.
> 3. Adresu koju više ne koristiš možeš obrisati.
>
> Nisi kod kuće u trenutku dostave? Napomenu za vozača ostavljaš pri naručivanju.

Ispod: mreža kartica. Pločica `+ Dodaj adresu` otvara formu. Svaka kartica nosi naziv, punu adresu, zonu, oznaku **Glavna** ako jeste, i radnje izmijeni / postavi kao glavnu / obriši.

Stanja: `LoadingBlock` dok se učitava, `EmptyState` kad nema nijedne adrese, i poruka kad ih ima 10 — „Dostigao si najviše 10 adresa. Obriši jednu da dodaš novu." — sa onemogućenom pločicom za dodavanje.

Ako `deliveryZoneActive` na adresi stoji `false`, kartica to mora pokazati: zona je ugašena poslije čuvanja, pa checkout tu adresu odbija. Tiho je prikazati kao ispravnu znači pustiti kupca da to otkrije tek pri slanju narudžbe.

Mutacije invalidiraju upit adresa da se lista osvježi bez ručnog reloada.

- [ ] **Step 4: Provjeri**

Run: `npx tsc --noEmit && npm run lint`

Uz pokrenut stek, prođi cio tok: dodaj adresu, izmijeni je, postavi drugu kao glavnu i potvrdi da prva **prestane** biti glavna, pa obriši jednu.

**Ovo je prvi put da iko poziva adresar iz UI-ja.** Kod je pisan u septembru i nikad izvršen kroz ekran. Ako nešto iskoči — pogrešan oblik odgovora, neočekivan status, zona koja se ne šalje kako backend očekuje — to je nalaz, ne smetnja: prijavi ga umjesto da ga zaobiđeš u komponenti.

- [ ] **Step 5: Commit**

```bash
git add frontend/
git commit -m "feat(nalog): sekcija Adrese nad postojećim adresarom

Prvi poziv backend adresara iz UI-ja otkako je napisan u septembru. UI odražava
backend pravila umjesto da ih duplira: najviše 10 adresa, tačno jedna glavna,
i upozorenje kad je zona adrese ugašena."
```

---

### Task 6: Preseljenje narudžbi

**Files:**
- Create: `frontend/src/app/(shop)/moj-nalog/narudzbe/page.tsx`
- Modify: `frontend/src/app/(shop)/orders/page.tsx`

**Interfaces:**
- Consumes: postojeći sadržaj `orders/page.tsx`
- Produces: ruta `/moj-nalog/narudzbe`; `/orders` kao preusmjerenje

- [ ] **Step 1: Preseli sadržaj**

Prenesi tijelo `frontend/src/app/(shop)/orders/page.tsx` (101 linija) u `moj-nalog/narudzbe/page.tsx` **bez redizajna**. Jedina dozvoljena izmjena: ukloni njegovu sopstvenu provjeru prijave i poziv na prijavu, jer to sada radi layout — zadržati je značilo bi dvije straže koje mogu reći različito.

- [ ] **Step 2: Pretvori staru rutu u preusmjerenje**

`frontend/src/app/(shop)/orders/page.tsx` postaje preusmjerenje na `/moj-nalog/narudzbe`. Provjeri kako Next 15 radi `redirect` iz App Routera (`next/navigation`) i koristi serversko preusmjerenje ako stranica može biti serverska — jeftinije je od klijentskog skoka.

Postojeći linkovi i bookmarkovi ne smiju puknuti; to je jedini razlog što stara ruta ostaje.

- [ ] **Step 3: Nađi i ažuriraj unutrašnje linkove**

Run: `grep -rn '"/orders"' frontend/src --include=*.tsx`

Svaki pogođeni link preusmjeri na `/moj-nalog/narudzbe`. Preusmjerenje je mreža za tuđe linkove, ne izgovor da naši pokazuju na staru rutu.

- [ ] **Step 4: Provjeri**

Run: `npx tsc --noEmit && npm run lint`

Uz pokrenut stek: otvori `/orders` i potvrdi da završiš na `/moj-nalog/narudzbe` sa istom listom; otvori novu rutu direktno; otvori obje odjavljen i potvrdi da dobiješ poziv na prijavu iz layouta, a ne praznu stranicu.

- [ ] **Step 5: Commit**

```bash
git add frontend/
git commit -m "feat(nalog): narudžbe se sele pod /moj-nalog/narudzbe

Sadržaj prenesen bez redizajna; uklonjena samo njegova straža, koju sada drži
layout. /orders ostaje kao preusmjerenje da postojeći linkovi ne puknu."
```

---

### Task 7: Dokumentacija i završna provjera

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Ispravi tvrdnju koja je postala netačna**

Run: `grep -n "Adresar has no frontend" CLAUDE.md`

`CLAUDE.md` tvrdi da adresar nema frontend i da ga ništa ne poziva. Od Taska 5 to više nije istina. Prepiši taj pasus: adresar sada ima svoj ekran pod `/moj-nalog/adrese`, `accountApi` je njegova jedina površina, i backend pravila (10 adresa, jedna glavna, provjera zone) su tamo odražena a ne duplirana.

**Zastarjela dokumentacija je gora od nikakve** — sljedeća sesija će joj vjerovati.

- [ ] **Step 2: Dopuni sekciju o nalogu**

Dodaj, među postojeće invarijante:

- `/moj-nalog` je ugnježdena ruta sa stražom u `layout.tsx`, **jednom** — sekcije je ne ponavljaju. Oblik prati `app/admin/layout.tsx`. Razlog što nije `?sekcija=`: `useSearchParams()` bez `<Suspense>` prolazi `tsc` i `lint` a obara `npm run build`, i to se u P1 desilo dvaput.
- `PUT /account/profile` mijenja **samo** ime i telefon i vraća isti `AuthUserDto` koji `auth/me` daje, da klijent osvježi kontekst iz odgovora. E-mail, lozinka i brisanje naloga su svjesno izvan — svaki je zaseban posao, razlozi su u specu P2a.
- `useAuth().azurirajProfil` je jedini put kojim se `user` u kontekstu mijenja izvan prijave; zaglavlje ovisi o tome.

- [ ] **Step 3: Puna provjera**

```bash
cd backend && dotnet test
```

Zatim **ugasi dev server**, pa iz `frontend/`:

```bash
npx tsc --noEmit && npm run lint && npm run build
```

Sva tri moraju biti čista. U izlazu builda potvrdi da se `/moj-nalog`, `/moj-nalog/podaci`, `/moj-nalog/adrese` i `/moj-nalog/narudzbe` pojavljuju u tabeli ruta.

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: nalog u CLAUDE.md, i ispravka tvrdnje o adresaru

Adresar više nema 'no frontend' — ima ekran od P2a. Zastarjela dokumentacija je
gora od nikakve, jer joj sljedeća sesija vjeruje."
```

---

## Šta P2a NE pokriva

Provjereno prema specu — sve ispod je namjerno izvan:

| Izostavljeno | Gdje ide |
|---|---|
| Kupčeva izmjena sadržaja korpice, budžet 95–100% | P2b |
| Sedmica na `Order`, pakerska lista po narudžbama | P2b (preduslov editora) |
| Pretplate, pauziranje, promjena paketa | P3 |
| Promjena e-maila i lozinke | zaseban posao, razlozi u specu |
| Brisanje naloga (GDPR) | zaseban posao — istorija narudžbi se po zakonu čuva |
| Birač adrese na mapi | nije u specu; forma prosljeđuje koordinate samo ako ih ima |
