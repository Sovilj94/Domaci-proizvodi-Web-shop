# P6a — Ocjena isporuke: plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Kupac ocijeni isporučenu narudžbu (1–5, komentar, dozvola za objavu); admin vidi ocjene i od dozvoljene pravi neobjavljen utisak.

**Architecture:** Novi entitet `OcjenaIsporuke` (1:1 sa `Order`, FK na ocjeni), navigacija `Order.Ocjena`, `Utisak.OcjenaId`. Komande ocjenjivanja za vlasnika i za gosta (token) dijele jednu jezgru. `OrderDto` dobija `ocjena` i `mozeSeOcijeniti`. Admin: lista sa sažetkom i „napravi utisak".

**Tech Stack:** ASP.NET Core 9, EF Core 9 + PostgreSQL, MediatR, FluentValidation, xUnit + SQLite; Next.js 15, TypeScript, Tailwind v4, TanStack Query, lucide-react.

**Spec:** `docs/superpowers/specs/2026-10-14-p6a-ocjena-isporuke-design.md`

## Global Constraints

- Omotnica `{ data, error, meta }` preko `Envelope`/`EnvelopeList`/`EnvelopeCreated`; nikad `Ok(dto)`.
- Kod greške je drugi argument konstruktora; `DomainException`→400, `NotFoundException`→404, `ConflictException`→409.
- JSON izostavlja null; TS `?:`, nikad `| null`.
- `TestDb` dijeli jedan `DbContext` — `t.Db.ChangeTracker.Clear()` prije tvrdnji o učitanom.
- `dotnet ef` sa `--project src/EPiljara.Infrastructure --startup-project src/EPiljara.Infrastructure --configuration Release`; ako API radi, i `dotnet test --configuration Release`. Migraciju primijeni na dev bazu (`database update`).
- Migracija dira samo svoje: tabela `OcjeneIsporuke` + kolona/indeks `Utisci.OcjenaId`. Ništa drugo.
- **Svaki novi validator:** prvo `RuleFor(x => x.Input).NotNull().WithMessage("Neispravan zahtjev.")`, pa `SetValidator(...).When(x => x.Input is not null)`; **svako pravilo ima svoju srpsku `.WithMessage`** (P5 lekcija: lanac `Length().Matches().WithMessage()` daje poruku samo zadnjem pravilu, prvo ide na engleski). Ključevi grešaka `input.<polje>` (`OverridePropertyName`, kao u `Features/Utisci/Utisci.cs`).
- Javni endpoint bez prijave ide iza `[EnableRateLimiting("auth")]`.
- Srpski/ijekavica; kodovi grešaka `snake_case`.
- Kontrast WCAG AA; zvjezdice: popunjena `text-honey-500` je ikonica (3:1 dovoljno), tekst uz nju mora biti AA.
- `npm run build` samo u zadnjem tasku, sa ugašenim dev serverom.
- Next mock se ne proširuje; ako tsc traži nova polja na mock `Order`, dodaj `mozeSeOcijeniti: false`.
- Commit poruke: prazan red i `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Upit koji ne učita `Ocjena`** — `mozeSeOcijeniti` laže (true za već ocijenjenu). Test po svakom od tri kupčeva upita (Task 1).
2. **Tuđa narudžba** — prijavljen kupac koji ocjenjuje tuđi `orderId` dobija 404, ne 403 (ne otkriva postojanje). Test (Task 1).
3. **Ime za utisak** — prvo ime + inicijal ZADNJE riječi, oboje sa velikim početnim slovom, višestruki razmaci sažeti: „Marko Marković" → „Marko M.", „marko" → „Marko", „  ana  maria  kovač " → „Ana K.". Test (Task 2).
4. **Dupla ocjena u trci** — dva istovremena zahtjeva: jedinstven indeks na `OrderId` je brana; `DbUpdateException` zbog njega se prevodi u 409 `vec_ocijenjeno`, ne 500 (Task 1).
5. **Brisanje utiska iz ocjene** oslobađa ocjenu (može se ponovo napraviti utisak) — `SetNull` (Task 2 test).

---

### Task 1: Ocjena — entitet, ocjenjivanje, `OrderDto`

**Files:** Create `backend/src/EPiljara.Domain/Entities/OcjenaIsporuke.cs`; Modify `Order.cs` (navigacija), `Utisak.cs` (`OcjenaId`), `IAppDbContext.cs`, `AppDbContext.cs`; Create `backend/src/EPiljara.Application/Features/Ocjene/Ocjene.cs`; Modify `DTOs/OrderDtos.cs`, `Mapping/MappingExtensions.cs` (`Order.ToDto`), `Features/Orders/OrderQueries.cs` (tri `Include`-a); Modify `API/Controllers/OrdersController.cs`; migracija `DodajOcjeneIsporuke`; Test `backend/tests/EPiljara.Application.Tests/OcjeneTests.cs`.

**Interfaces — Produces:**
- `OcjenaInput(int Ocjena, string? Komentar, bool DozvolaZaObjavu)`.
- `OcijeniNarudzbuCommand(Guid OrderId, OcjenaInput Input) : IRequest<OcjenaDto>` (vlasnik, `ICurrentUser`); `OcijeniPrekoTokenaCommand(string Token, OcjenaInput Input) : IRequest<OcjenaDto>`.
- `OcjenaDto { Ocjena (int), Komentar (string), DozvolaZaObjavu (bool), Kreirana }`.
- `OrderDto.Ocjena` (`OcjenaDto?`, odsutno kad nema) i `OrderDto.MozeSeOcijeniti` (bool).
- Rute: `POST /orders/{id:guid}/ocjena` (`[Authorize]`), `POST /orders/track/{token}/ocjena` (`[EnableRateLimiting("auth")]`). Oba `EnvelopeCreated`.
- `Utisak.OcjenaId` (`Guid?`) — samo kolona i FK u ovom tasku; koristi ga Task 2.

- [ ] **Step 1: Model**

```csharp
public class OcjenaIsporuke : Entity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public int Ocjena { get; set; }
    public string Komentar { get; set; } = string.Empty;
    public bool DozvolaZaObjavu { get; set; }
    public DateTime Kreirana { get; set; } = DateTime.UtcNow;
}
```

`Order`: `public OcjenaIsporuke? Ocjena { get; set; }` (XML komentar: FK je na ocjeni; `ToDto` čita ovu navigaciju, pa upiti koji pune `mozeSeOcijeniti` moraju je `Include`-ovati). `Utisak`: `public Guid? OcjenaId { get; set; }` + `public OcjenaIsporuke? Ocjena { get; set; }`.

`AppDbContext`: `DbSet<OcjenaIsporuke> OcjeneIsporuke`;

```csharp
        b.Entity<OcjenaIsporuke>(e =>
        {
            e.Property(x => x.Komentar).HasMaxLength(1000).IsRequired();
            // Jedna ocjena po narudžbi — i brana za dva istovremena zahtjeva (Review Focus 4).
            e.HasIndex(x => x.OrderId).IsUnique();
            e.HasOne(x => x.Order).WithOne(o => o.Ocjena)
                .HasForeignKey<OcjenaIsporuke>(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Kreirana);
        });
```

i u bloku `Utisak`:

```csharp
            e.HasOne(x => x.Ocjena).WithMany()
                .HasForeignKey(x => x.OcjenaId).OnDelete(DeleteBehavior.SetNull);
            // Od jedne ocjene najviše jedan utisak; filter jer je većina utisaka bez ocjene.
            e.HasIndex(x => x.OcjenaId).IsUnique().HasFilter("\"OcjenaId\" IS NOT NULL");
```

- [ ] **Step 2: Testovi** (`OcjeneTests.cs`, pomoćnik pravi kategoriju, zonu, slot, kupca i narudžbu sa zadatim statusom — pogledaj kako to rade `CreateOrderVlasnikTests.cs`/`Scenario.cs` i iskoristi postojeći pomoćnik gdje ide). Svaki slučaj jedan test:
  1. Vlasnik ocjenjuje `Delivered` narudžbu → upis (trim komentara), vraćen `OcjenaDto`.
  2. Narudžba u statusu `Pending`/`OutForDelivery`/`FailedDelivery` → `DomainException` `narudzba_nije_isporucena`.
  3. Druga ocjena iste narudžbe → `ConflictException` `vec_ocijenjeno`.
  4. Drugi kupac (`FakeCurrentUser` sa drugim id-em) → `NotFoundException` (Review Focus 2); nepostojeći id → `NotFoundException`.
  5. Gost preko `PublicToken`-a → upis; pogrešan token → `NotFoundException`.
  6. Validator: ocjena 0 i 6 odbijene, komentar 1001 znak odbijen, `null` Input odbijen sa ključem `input`; ispravan bez komentara prolazi; nijedna poruka ne sadrži „must" ni apostrof (engleski default).
  7. `OrderDto`: za svaki od `GetMyOrdersHandler`, `GetOrderByIdHandler`, `TrackOrderHandler` — isporučena neocijenjena → `MozeSeOcijeniti == true`, `Ocjena == null`; poslije ocjene (`ChangeTracker.Clear()`) → `false` i `Ocjena.Ocjena == n` (Review Focus 1).

- [ ] **Step 3:** focused → ne prevodi (RED). Zapiši izlaz.

- [ ] **Step 4: Feature** `Features/Ocjene/Ocjene.cs` — jezgra:

```csharp
internal static class OcjenaPravila
{
    /// <summary>Zajedničko za vlasnika i gosta: provjera statusa, jedna ocjena, upis.</summary>
    public static async Task<OcjenaDto> OcijeniAsync(IAppDbContext db, Order order, OcjenaInput input, CancellationToken ct)
    {
        if (order.Status != OrderStatus.Delivered)
            throw new DomainException("Ocijeniti se može samo isporučena narudžba.", "narudzba_nije_isporucena");
        if (await db.OcjeneIsporuke.AnyAsync(o => o.OrderId == order.Id, ct))
            throw new ConflictException("Ova narudžba je već ocijenjena.", "vec_ocijenjeno");

        var ocjena = new OcjenaIsporuke
        {
            OrderId = order.Id,
            Ocjena = input.Ocjena,
            Komentar = (input.Komentar ?? "").Trim(),
            DozvolaZaObjavu = input.DozvolaZaObjavu,
        };
        db.OcjeneIsporuke.Add(ocjena);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Dva istovremena zahtjeva prođu AnyAsync; jedinstven indeks propusti samo prvi.
            throw new ConflictException("Ova narudžba je već ocijenjena.", "vec_ocijenjeno");
        }
        return ocjena.ToDto();
    }
}
```

(Provjeri da `ConflictException` ima konstruktor `(poruka, kod)` — `AdminProducers` ga tako zove. Ako `DbUpdateException` nije dostupan u Application sloju bez reference na EF Core — jeste, `Microsoft.EntityFrameworkCore` se već koristi.) Vlasnik: učita narudžbu `o.Id == cmd.OrderId && o.CustomerId == current.Id`, inače `NotFoundException("Narudžba nije pronađena.")`. Gost: po `PublicToken`. Validator `OcjenaInputValidator`: `Ocjena` `InclusiveBetween(1,5)` „Ocjena mora biti od 1 do 5.", `Komentar` (trimovan) `MaximumLength(1000)` „Komentar može imati najviše 1000 znakova." — plus `NotNull` na `Input` u oba command validatora.

`OrderDto`: `public OcjenaDto? Ocjena { get; init; }`, `public bool MozeSeOcijeniti { get; init; }`; u `ToDto()`: `Ocjena = o.Ocjena?.ToDto(), MozeSeOcijeniti = o.Status == OrderStatus.Delivered && o.Ocjena is null`. Tri kupčeva upita: `.Include(o => o.Ocjena)`.

- [ ] **Step 5: Kontroler** — u `OrdersController` dvije rute (vlasnik sa `[Authorize]`, gost sa `[EnableRateLimiting("auth")]`), tijelo `[FromBody] OcjenaInput input`.
- [ ] **Step 6:** testovi PASS; pun `dotnet test`.
- [ ] **Step 7:** migracija `DodajOcjeneIsporuke` + `database update`; `Up()` samo tabela, indeksi, FK i kolona `Utisci.OcjenaId`.
- [ ] **Step 8: Frontend tip** — `frontend/src/types/domain.ts` `Order`: `ocjena?: OcjenaIsporuke` (`{ ocjena: number; komentar: string; dozvolaZaObjavu: boolean; kreirana: string }`), `mozeSeOcijeniti: boolean`; `contracts.ts`: `OcjenaInput`; `endpoints.ts`: `orderApi.ocijeni(id, body)`, `orderApi.ocijeniPrekoTokena(token, body)` (`auth: false`). `npx tsc --noEmit`.
- [ ] **Step 9: Commit** `feat(p6a): ocjena isporuke — entitet, ocjenjivanje za vlasnika i gosta`

---

### Task 2: Admin — ocjene, sažetak, utisak iz ocjene

**Files:** Modify `Features/Ocjene/Ocjene.cs` (admin upit i komanda), `Features/Utisci/Utisci.cs` (`UtisakDto.IzOcjene`), `DTOs`; Modify `AdminController.cs`; Test `OcjeneAdminTests.cs`.

**Interfaces — Produces:** `GET /admin/ocjene?ocjena=` → `OcjeneAdminDto { Sazetak: { Prosjek (decimal, 1 decimala, 0 kad nema), Broj, PoBroju: int[5] (indeks 0 = jedna zvjezdica) }, Stavke: [OcjenaAdminDto { Id, Ocjena, Komentar, DozvolaZaObjavu, Kreirana, OrderId, KodNarudzbe, Kupac, Zona, UtisakId? }] }` (sažetak se računa nad **svim** ocjenama, filter `ocjena` sužava samo `Stavke`); `POST /admin/ocjene/{id:guid}/utisak` → `UtisakDto`. `UtisakDto.IzOcjene` (bool).

- [ ] **Step 1: Testovi** — sažetak (prosjek 4.3 za [5,4,4], raspodjela, prazna tabela → 0 i nule); filter vraća samo traženu ocjenu ali sažetak ostaje nad svim; stavke najnovije prve sa kodom narudžbe, kupcem, zonom; „napravi utisak": `Utisak` neobjavljen, `Ime` „Marko M." iz „marko marković" (veliko početno slovo, inicijal zadnje riječi), „Marko" ostaje „Marko", višestruki razmaci se sažimaju, `Mjesto` = naziv zone, `Tekst` = komentar, `OcjenaId` upisan, `Redoslijed` 0; drugi put → 409 `utisak_vec_postoji`; bez dozvole → 400 `ocjena_nije_za_objavu`; dozvola ali prazan komentar → isto; komentar kraći od 10 znakova (Utisak traži 10–500) → isto 400 `ocjena_nije_za_objavu` („Komentar je prekratak za utisak."); komentar duži od 500 → tekst utiska se **skraćuje na 497 znakova + „…"** (admin ga ionako uređuje prije objave) — test to potvrđuje. Brisanje tog utiska (`DeleteUtisakHandler`) → `UtisakId` u admin listi postaje null i utisak se može napraviti ponovo (Review Focus 5).
- [ ] **Step 2:** RED.
- [ ] **Step 3: Implementacija** — `ImeZaUtisak(string punoIme)` kao `internal static` u `OcjenaPravila` (testirana kroz komandu). Prosjek: `Math.Round(avg, 1, MidpointRounding.AwayFromZero)`. Sortiranje u memoriji je prihvatljivo (kao ostali admin upiti). `UtisakDto.IzOcjene = u.OcjenaId != null` u mapiranju utisaka.
- [ ] **Step 4: Kontroler** — dvije admin rute.
- [ ] **Step 5:** PASS; pun `dotnet test`.
- [ ] **Step 6: Frontend API** — tipovi `OcjeneAdmin`, `OcjenaAdmin`; `adminApi.ocjene(ocjena?)`, `adminApi.napraviUtisak(id)`; `queryKeys.adminOcjene(ocjena?)` = `["admin","ocjene", ocjena ?? "sve"]`; `Utisak.izOcjene: boolean`. `npx tsc --noEmit`.
- [ ] **Step 7: Commit** `feat(p6a): admin ocjene — sažetak, lista, utisak iz ocjene`

---

### Task 3: Ocjena na stranicama kupca

**Files:** Create `frontend/src/components/ocjena/ocjena-forma.tsx`, `frontend/src/components/ocjena/zvjezdice.tsx`; Modify `frontend/src/app/(shop)/narudzba/[token]/page.tsx`, `frontend/src/app/(shop)/moj-nalog/narudzbe/page.tsx`.

**Interfaces — Consumes:** `Order.ocjena?`, `Order.mozeSeOcijeniti`, `orderApi.ocijeni`, `orderApi.ocijeniPrekoTokena`, `OcjenaInput` (Task 1). Ključevi grešaka `input.ocjena`, `input.komentar`.

- [ ] **Step 1: `zvjezdice.tsx`** — `Zvjezdice({ ocjena, velicina? })`: pet `Star` ikona, popunjene `fill-honey-500 text-honey-500`, ostale `text-line`; `role="img"` `aria-label={`${ocjena} od 5`}`.
- [ ] **Step 2: `ocjena-forma.tsx`** — `OcjenaForma({ posalji: (input: OcjenaInput) => Promise<unknown>, onUspjeh: () => void })`:
  - zvjezdice kao **radio grupa** (`role="radiogroup"` `aria-label="Ocjena isporuke"`, pet `<input type="radio" name="ocjena" className="sr-only">` sa `<label>` koji nosi zvjezdicu i `aria-label` „N od 5"; strelice rade jer su to pravi radio inputi); hover/odabir boji zvjezdice do odabrane;
  - `Textarea` „Komentar (nije obavezan)", `maxLength={1000}`, brojač;
  - checkbox „Dozvoljavam da se moj komentar objavi na sajtu, potpisan imenom i inicijalom prezimena." (podrazumijevano isključen);
  - dugme „Pošalji ocjenu" (`disabled` dok ocjena nije izabrana);
  - greške: `fieldErrors["input.ocjena"]`, `["input.komentar"]` ispod polja; 409 `vec_ocijenjeno` → poruka „Ova narudžba je već ocijenjena." i `onUspjeh()` (osvježi podatke); 429 → „Previše pokušaja, pokušajte za minut."; ostalo → toast. Pogledaj `components/partneri/partner-forma.tsx` (P5) za obrazac čitanja `ApiRequestError.status`/`code`/`fieldErrors`.
  - poslije uspjeha: zahvala „Hvala na ocjeni!" (forma nestaje), `onUspjeh()`.
- [ ] **Step 3: Stranica praćenja** — kad `order.mozeSeOcijeniti`: kartica „Kako je prošla isporuka?" sa `OcjenaForma` (`posalji={(i) => orderApi.ocijeniPrekoTokena(token, i)}`, `onUspjeh` invalidira ključ upita te stranice); kad `order.ocjena`: kartica „Vaša ocjena" sa `Zvjezdice` i komentarom.
- [ ] **Step 4: Moje narudžbe** — po narudžbi: kad `mozeSeOcijeniti`, dugme „Ocijeni isporuku" koje otvara `OcjenaForma` ispod te narudžbe (jedna otvorena odjednom); kad `ocjena` — `Zvjezdice` u redu narudžbe. Invalidira `queryKeys.myOrders()`.
- [ ] **Step 5:** `npx tsc --noEmit`, `npm run lint`.
- [ ] **Step 6: Commit** `feat(p6a): ocjena isporuke na stranici praćenja i u Mojim narudžbama`

---

### Task 4: Admin ekran ocjena

**Files:** Create `frontend/src/app/admin/ocjene/page.tsx`; Modify `frontend/src/app/admin/utisci/page.tsx`, `frontend/src/components/admin/admin-nav.tsx`.

**Interfaces — Consumes:** `adminApi.ocjene`, `adminApi.napraviUtisak`, `queryKeys.adminOcjene`, `queryKeys.adminUtisci`, `Zvjezdice` (Task 3), `Utisak.izOcjene` (Task 2).

- [ ] **Step 1: Stranica** — naslov „Ocjene isporuke"; sažetak: prosjek (`toFixed(1)` sa zarezom: `prosjek.toLocaleString("bs-BA", { minimumFractionDigits: 1, maximumFractionDigits: 1 })`) uz `Zvjezdice` zaokružene, broj ocjena (riječ po broju: 1 ocjena, 2–4 ocjene, 5+ ocjena, 11–14 ocjena), raspodjela 5→1 kao horizontalne trake (širina = udio; broj uz traku); filter dugmad „Sve, 5, 4, 3, 2, 1"; lista: zvjezdice, komentar (`whitespace-pre-line`, prazan → „bez komentara" prigušeno), kod narudžbe, kupac, zona, datum; oznaka „Dozvoljena objava"; dugme „Napravi utisak" (samo kad je dozvoljeno i ima komentar i nema `utisakId`) → poziv, toast „Utisak napravljen — objavite ga na stranici Utisci", invalidira `adminOcjene` prefiks `["admin","ocjene"]` i `adminUtisci`; kad postoji `utisakId` → tekst „Utisak napravljen" + link na `/admin/utisci`. Greške u toast.
- [ ] **Step 2: Utisci** — u listi oznaka „iz ocjene" (mali bedž) kad `izOcjene`.
- [ ] **Step 3: Nav** — „Ocjene" (`Star`) poslije „Utisci".
- [ ] **Step 4:** `npx tsc --noEmit`, `npm run lint`.
- [ ] **Step 5: Commit** `feat(p6a): admin ekran ocjena isporuke`

---

### Task 5: Provjera i dokumentacija

- [ ] `dotnet test` (Release ako API radi); ugašen dev server, obrisan `.next`, `tsc` + `lint` + `npm run build`.
- [ ] Ručno (API + dev server): narudžbu u dev bazi prošetati do `Delivered` kroz admin; ocijeniti kao gost na `/narudzba/{token}`; Mujinu isporučenu narudžbu ocijeniti iz „Mojih narudžbi"; admin `/admin/ocjene` — sažetak, filter, „Napravi utisak" → `/admin/utisci` (neobjavljen, „iz ocjene") → objavi → vidi se na početnoj → obriši utisak → ocjena opet nudi „Napravi utisak". Test podatke ukloniti ili zapisati u STANJE.md.
- [ ] `STANJE.md` (P6a, endpointi, entitet, ekrani, nalazi), `CLAUDE.md` (sekcija „Ocjena isporuke (P6a)": tri upita moraju `Include`-ovati `Ocjena`; tuđa narudžba → 404; utisak iz ocjene samo uz dozvolu, „Ime P.", neobjavljen).
- [ ] Commit `docs(p6a): STANJE.md i CLAUDE.md`
