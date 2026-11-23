# Narudžba za nekog drugog — plan izrade

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prijavljeni kupac može naručiti za drugu osobu (primaoca) na njenu adresu; primalac se pamti u adresaru, vozač zove primaoca, potvrda ide samo naručiocu.

**Architecture:** `Address` i `Order` dobijaju nullable `PrimalacIme`/`PrimalacTelefon`. `POST /orders` prima opcioni `primalac`; `Order` ga snima. Sve što služi isporuci (`RouteStopDto`, pakovanje) bira primaoca kad postoji. Checkout korak 1 nudi adresar + „Naruči za nekog drugog", a poslije uspješne narudžbe klijent čuva novu adresu sa primaocem u adresar.

**Tech Stack:** ASP.NET Core 9, EF Core + Postgres, MediatR, FluentValidation, xUnit + SQLite; Next.js 15, TanStack Query, TypeScript.

**Spec:** `docs/superpowers/specs/2026-11-23-narudzba-za-drugog-design.md`

## Global Constraints

- Poruke i UI tekst na srpskom (latinica u kodu).
- `POST /orders` vezuje `CreateOrderRequest`, nikad komandu.
- API izostavlja null polja → u TS `?:`, nikad `| null`.
- Primalac: ime i telefon **u paru** (oba ili nijedno); ime ≤ 160, telefon ≤ 40.
- Gost (`guest-request`, `GuestOrderRequest`) **ne** dobija primaoca.
- Potvrda (mejl/Viber) ide samo naručiocu.
- Vozačka aplikacija se ne mijenja; `RouteStopDto.CustomerName/CustomerPhone` nose primaoca kad postoji.
- Migracije: `dotnet dotnet-ef migrations add <Ime> -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations` (iz `backend/`), bez podataka.
- Frontend gotov tek kad prođu `npx tsc --noEmit`, `npm run lint`, `npm run build`.

## Review Focus

1. Kupac izabere adresu sa primaocem pa pređe na svoju → primalac nestaje iz narudžbe (Task 4, test ručnog toka + čist reducer).
2. Samo ime primaoca bez telefona (ili obrnuto) → 400 sa ključem `primalac` (Task 2 test).
3. Narudžba bez primaoca → vozač i pakovanje i dalje vide kupca (Task 2 test).
4. Adresar pun (10) ili adresa već postoji → narudžba uspije, nema duplikata (Task 4, `sacuvajPrimaoca` ne baca).
5. Unaprijed izabrana adresa nikad nije adresa sa primaocem (Task 4, `podrazumijevanaAdresa` test u `node --test`).

---

### Task 1: Adresar pamti primaoca (backend)

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/Address.cs`
- Modify: `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs` (blok `b.Entity<Address>`)
- Modify: `backend/src/EPiljara.Application/Features/Account/AddressCommands.cs` (komande, `IAddressFields`, `AddressRules`, handleri)
- Modify: `backend/src/EPiljara.Application/DTOs/AccountDtos.cs` + mapiranje `AddressDto`
- Modify: `backend/src/EPiljara.API/Controllers/AccountController.cs` (`UpdateAddressBody`, poziv `UpdateAddressCommand`)
- Create: migracija `AdresaPrimalac`
- Test: `backend/tests/EPiljara.Application.Tests/AddressCommandsTests.cs`

**Interfaces:**
- Produces: `Address.PrimalacIme : string?`, `Address.PrimalacTelefon : string?` (max 160 / 40); `CreateAddressCommand(..., Guid? DeliveryZoneId, string? PrimalacIme = null, string? PrimalacTelefon = null)`, isto za `UpdateAddressCommand`; `IAddressFields` dobija oba svojstva; `AddressDto.PrimalacIme?`, `AddressDto.PrimalacTelefon?`. Prazan/whitespace string se čuva kao `null`.

- [ ] **Step 1: Failing testovi** u `AddressCommandsTests`:
  - `Adresa_pamti_primaoca`: create sa `("Mama i tata", "Kozaračka 5", lat, lon, null, "Petar Petrović", "+387 65 111 222")` → DTO i red u bazi (`ChangeTracker.Clear()` pa čitanje) imaju oba polja.
  - `Primalac_bez_telefona_je_odbijen`: validator `CreateAddressValidator` za `("x","Ulica 1",..., "Petar", null)` → greška sa `PropertyName == "primalac"`.
  - `Izmjena_brise_primaoca_kad_su_polja_prazna`: update sa `"", ""` → oba `null`.
- [ ] **Step 2:** `dotnet test --filter AddressCommandsTests` → FAIL (kompilacija).
- [ ] **Step 3:** Implementirati polja, EF `HasMaxLength(160)`/`(40)`, pravilo para u `AddressRules.Apply` (`Must(x => string.IsNullOrWhiteSpace(x.PrimalacIme) == string.IsNullOrWhiteSpace(x.PrimalacTelefon)).OverridePropertyName("primalac").WithMessage("Unesi i ime i telefon primaoca.")`) + dužine; handleri trimuju i pretvaraju prazno u `null`; controller prosljeđuje polja.
- [ ] **Step 4:** Migracija `AdresaPrimalac`; `dotnet build` bez upozorenja; `dotnet test` → PASS.
- [ ] **Step 5: Commit** `feat(adresar): primalac uz adresu`.

### Task 2: Narudžba nosi primaoca (backend)

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/Order.cs`, `AppDbContext.cs` (blok `Order`)
- Modify: `backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs` (`PrimalacInput`, `CreateOrderCommand`, `CreateOrderRequest.ToCommand`, validator, snimak na `Order`)
- Modify: `backend/src/EPiljara.Application/DTOs/OrderDtos.cs`, `Mapping/MappingExtensions.cs` (`Order.ToDto`, `RouteStop.ToDto`)
- Modify: `backend/src/EPiljara.Application/Features/Admin/Pakovanje.cs` (`Kupac`/`Telefon`), `Features/Admin/AdminOrders.cs` (pretraga)
- Modify: `backend/src/EPiljara.Infrastructure/Services/OrderEmailTemplate.cs` (red „Prima: …")
- Create: migracija `NarudzbaPrimalac`
- Test: Create `backend/tests/EPiljara.Application.Tests/NarudzbaZaDrugogTests.cs` (priprema kao u `CreateOrderVlasnikTests`)

**Interfaces:**
- Produces: `public record PrimalacInput(string Ime, string Telefon);` u `CreateOrder.cs`; `CreateOrderCommand(..., bool IskoristiPoene = false, PrimalacInput? Primalac = null)`; `CreateOrderRequest(..., bool IskoristiPoene = false, PrimalacInput? Primalac = null)`; `Order.PrimalacIme/PrimalacTelefon : string?`; `OrderDto.PrimalacIme?/PrimalacTelefon?`. JSON: `primalac: { ime, telefon }`.
- `CreateGuestOrderRequestCommand` se **ne** mijenja.

- [ ] **Step 1: Failing testovi** u `NarudzbaZaDrugogTests`:
  - `Narudzba_snima_primaoca_a_kupac_ostaje_narucilac`: komanda sa `Primalac: new("Petar Petrović", "+387 65 111 222")` → `Order.CustomerName == "Marko"`, `Order.PrimalacIme == "Petar Petrović"`, `OrderDto.PrimalacTelefon == "+387 65 111 222"`.
  - `Stanica_rute_nosi_primaoca`: `new RouteStop { Order = order }.ToDto()` → `CustomerName == "Petar Petrović"`, `CustomerPhone == "+387 65 111 222"`.
  - `Stanica_bez_primaoca_nosi_kupca`: isto bez primaoca → `"Marko"`, `"065"`.
  - `Primalac_mora_imati_i_telefon`: `CreateOrderValidator` sa `Primalac: new("Petar", "")` → greška sa `PropertyName` koji počinje na `"Primalac"` (camelCase na žici: `primalac.telefon`).
  - `Lista_pakovanja_pokazuje_primaoca`: preko `Pakovanje` handlera za dan isporuke → `Kupac == "Petar Petrović"`.
- [ ] **Step 2:** `dotnet test --filter NarudzbaZaDrugogTests` → FAIL.
- [ ] **Step 3:** Implementirati: validator `RuleFor(x => x.Primalac!.Ime).NotEmpty().MaximumLength(160)` i `Telefon` `.NotEmpty().MaximumLength(40)` `.When(x => x.Primalac is not null)`; handler trimuje i snima; `RouteStop.ToDto` i `Pakovanje` koriste `o.PrimalacIme ?? o.CustomerName` / `o.PrimalacTelefon ?? o.CustomerPhone`; pretraga u `AdminOrders` dodaje oba polja; mejl dobija red `Prima: {ime} · {telefon}` ispod reda „Kupac" kad postoji.
- [ ] **Step 4:** Migracija `NarudzbaPrimalac`; `dotnet build` bez upozorenja; cijeli `dotnet test` → PASS.
- [ ] **Step 5: Commit** `feat(narudzbe): primalac na narudžbi, vozač i pakovanje ga vide`.

### Task 3: Tipovi i adresar na frontu

**Files:**
- Modify: `frontend/src/types/domain.ts` (`Address`, `Order`), `frontend/src/lib/api/contracts.ts` (`AddressInput`, `CreateOrderRequest`)
- Modify: `frontend/src/components/nalog/adresa-forma.tsx`, kartica adrese u `frontend/src/app/(shop)/moj-nalog/adrese/page.tsx`

**Interfaces:**
- Produces: `Address.primalacIme?: string; primalacTelefon?: string`; `Order.primalacIme?`, `Order.primalacTelefon?`; `AddressInput.primalacIme?`, `primalacTelefon?`; `CreateOrderRequest.primalac?: { ime: string; telefon: string }`.

- [ ] **Step 1:** Dodati tipove.
- [ ] **Step 2:** Forma adrese: checkbox „Ova adresa je za nekog drugog" otkriva „Ime i prezime primaoca" i „Telefon primaoca" (+387 prefiks kao checkout korak 2; šalje `+387 {broj}`); bez checkboxa šalje prazne stringove (briše primaoca). Greška `primalac` iz servera ispod polja.
- [ ] **Step 3:** Kartica adrese: red „Prima: {ime} · {telefon}" kad postoji.
- [ ] **Step 4:** `npx tsc --noEmit` i `npm run lint` → čisto.
- [ ] **Step 5: Commit** `feat(adresar): primalac u formi i na kartici`.

### Task 4: Checkout — adresar i „Naruči za nekog drugog"

**Files:**
- Create: `frontend/src/lib/checkout-adrese.ts` + `frontend/src/lib/checkout-adrese.test.ts`
- Modify: `frontend/src/components/shop/address-chooser.tsx`
- Modify: `frontend/src/app/(shop)/checkout/page.tsx`

**Interfaces:**
- Consumes: tipovi iz Task 3, `accountApi.adrese()`, `accountApi.dodajAdresu()`.
- Produces (u `checkout-adrese.ts`, čiste funkcije):
  - `podrazumijevanaAdresa(adresar: Address[], ranije: string[]): Address | string | null` — default iz adresara bez primaoca → prva iz adresara bez primaoca → `ranije[0]` → `null`. Adresa sa primaocem nikad.
  - `ranijeBezAdresara(adresar: Address[], ranije: string[]): string[]` — ranije adrese čiji tekst (trim, bez razlike u velikim slovima) nije u adresaru.
  - `async function sacuvajPrimaoca(postojece: Address[], unos: AddressInput): Promise<"sacuvano" | "postoji" | "pun" | "greska">` — ne baca nikad; `"postoji"` kad isti `fullAddress` već postoji; `"pun"` kad ih ima 10.
- `AddressChooser` dobija props: `adresar: Address[]`, `onPrimalac: (p: { ime: string; telefon: string } | null) => void`, `onZaDrugog: (podaci: { naziv: string; ime: string; telefon: string } | null) => void`.

- [ ] **Step 1: Failing testovi** `checkout-adrese.test.ts` (`node --test`):
  - `podrazumijevanaAdresa` preskače default sa primaocem i vraća prvu „moju"; vraća `ranije[0]` kad je adresar sav sa primaocima; `null` za prazno.
  - `ranijeBezAdresara` izbacuje `" kozaračka 5 "` kad adresar ima `"Kozaračka 5"`.
  - `sacuvajPrimaoca` vraća `"postoji"` i `"pun"` bez poziva API-ja (API mock koji baca ako se pozove).
- [ ] **Step 2:** `npm test` → FAIL.
- [ ] **Step 3:** Implementirati `checkout-adrese.ts` (`sacuvajPrimaoca` prima API kao parametar sa podrazumijevanim `accountApi.dodajAdresu`, da bi test mogao da ga zamijeni).
- [ ] **Step 4:** `npm test` → PASS.
- [ ] **Step 5:** `AddressChooser`: lista = adresar (naziv, adresa, „Prima: …"; adresa sa tačkom → `onCoordinates` njene tačke bez geokodiranja; bez tačke → postojeće geokodiranje) + `ranijeBezAdresara`. Izbor adrese sa primaocem → `onPrimalac({ime, telefon})`; svaki drugi izbor, „Nova adresa" i povratak na listu → `onPrimalac(null)` i `onZaDrugog(null)`. Dugme **„Naruči za nekog drugog"** otvara `AddressPicker` + polja Naziv (opciono, placeholder „npr. Mama i tata"), Ime i prezime primaoca, Telefon primaoca (+387); promjene javlja `onZaDrugog`.
- [ ] **Step 6:** `checkout/page.tsx`: `useQuery` adresara samo za prijavljenog; početni izbor preko `podrazumijevanaAdresa`; stanje `primalac`; `step1Valid` u režimu „za drugog" traži ime i telefon primaoca; tijelo narudžbe šalje `primalac` samo kad postoji; korak 3 ispod adrese „Prima: {ime} · {telefon}"; u `mutation.onSuccess`, ako je režim „za drugog", poziva `sacuvajPrimaoca` (bez `await` koji blokira prikaz potvrde) i za `"pun"`/`"greska"` toast „Adresa nije sačuvana u adresar."; poslije toga invalidira upit adresara. Korak 2 bez promjene.
- [ ] **Step 7:** `npx tsc --noEmit`, `npm run lint`, `npm run build` → čisto.
- [ ] **Step 8: Commit** `feat(checkout): naruči za nekog drugog`.

### Task 5: Prikaz primaoca i dokumentacija

**Files:**
- Modify: `frontend/src/components/shop/order-receipt.tsx` (kupac, praćenje), `frontend/src/components/admin/order-card.tsx`
- Modify: `CLAUDE.md`

- [ ] **Step 1:** `order-receipt`: red „Prima: {ime} · {telefon}" kad postoji. `order-card`: „Naručio: {customerName}" i „Prima: …" kad postoji.
- [ ] **Step 2:** `CLAUDE.md`: kratka sekcija „Primalac (narudžba za nekog drugog)" — naručilac = `Customer*`, primalac = `Primalac*`; isporuka (`RouteStopDto`, pakovanje) bira primaoca; potvrda samo naručiocu; gost ga namjerno nema (drugo mjesto koje pravi `Order`).
- [ ] **Step 3:** `npx tsc --noEmit`, `npm run lint`, `npm run build`; `dotnet test` → sve zeleno.
- [ ] **Step 4: Commit** `feat: primalac u prikazu narudžbe + CLAUDE.md`.
