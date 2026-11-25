# Korpica po budžetu — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Korpica bez „mrvica": najmanja količina po namirnici, kupčev editor kao budžet (−/+, dodaj, popuni, sačuvaj), generator koji osnovu stavlja punu i preskače kulturu umjesto da je smanji.

**Architecture:** Pravilo količine živi na `Product` (domen) i dijele ga generator, PUT kupčeve korpice i novi „popuni" endpoint. DTO-i nose `najmanje` i `korak`, pa frontend ne kopira pravilo. Editor drži lokalni nacrt i šalje ga jednim PUT-om.

**Tech Stack:** ASP.NET Core 9 + EF Core (Postgres, SQLite u testovima), xUnit; Next.js 15 + TanStack Query, `node --test`.

**Spec:** `docs/superpowers/specs/2026-11-23-korpica-po-budzetu-design.md`

## Global Constraints

- Budžet 95–100 % `CiljnaVrijednost` ostaje (`IzmijeniMojuKorpicuHandler.DonjaGranica/GornjaGranica`).
- Marža: nabavni trošak ≤ `cijenaKorpice × (1 − MinimalnaMarza)` ili ne veći nego prije izmjene.
- Cijene uvijek čita server (snimak reda → šablon → katalog); kupcu se ne prikazuju pojedinačne cijene.
- Najmanja količina: `NajmanjeUKorpici ?? KolicinaMala ?? korak` (korak: kom 1, kg/l 0,1).
- Korak editora: kom 1; kg/l `min(0,5, najmanja)`.
- Težina generatora: do `TezinaDo × 1,10`.
- Kodovi grešaka su camelCase/snake kao postojeći; novi: `kolicina_premala`, `marza`.

## Review Focus

1. Zadržan red iz šablona sa količinom ispod nove najmanje — mora se moći sačuvati nepromijenjen.
2. Kupac pošalje količinu koja nije višekratnik koraka zaokruživanja (0,25 kg) — odbiti, ne zaokružiti tiho.
3. Popuni kad je korpica već preko 100 % — vraća nacrt nepromijenjen, ne baca.
4. Popuni ne smije dodati artikal sa kupčevog spiska „ne stavljaj mi".
5. Komad (kom) sa decimalnom količinom — odbiti.

---

### Task 1: `NajmanjeUKorpici` na proizvodu + postavke

**Files:** `Domain/Entities/Product.cs`, `Infrastructure/Persistence/AppDbContext.cs`, nova migracija `NajmanjeUKorpici`,
`Application/Features/Korpice/PostavkeKorpica.cs`, `Application/DTOs/CatalogDtos.cs`, `Application/Mapping/MappingExtensions.cs`,
frontend `types/domain.ts`, `lib/api/endpoints.ts` (`ArtikalKorpiceUnos`), `app/admin/korpice/postavke/page.tsx`.
Test: `tests/.../NajmanjeUKorpiciTests.cs`.

**Interfaces — Produces:**
- `Product.NajmanjeUKorpici : decimal?`
- `Product.NajmanjaKolicinaUKorpici() : decimal` — `NajmanjeUKorpici ?? KolicinaMala ?? korak`, nikad < korak.
- `Product.KorakUKorpici() : decimal` — kom 1; inače `min(0.5, NajmanjaKolicinaUKorpici())`, najmanje 0,1.
- `Product.KorakZaokruzivanja(Unit) : decimal` (static) — kom 1, inače 0,1.
- `ArtikalKorpiceUnos.NajmanjeUKorpici : decimal?` (zadnji, opcioni parametar).

- [ ] Test: fallback lanac (polje → KolicinaMala → korak), korak 0,5 / najmanja / 1 kom; `SacuvajArtikleKorpica` upisuje polje, odbija ≤ 0 i decimalu za kom.
- [ ] Run → FAIL (compile).
- [ ] Implement + migracija (`dotnet ef migrations add NajmanjeUKorpici`), precision (10,3).
- [ ] Admin postavke: kolona „Najmanje" pored zadatih količina, placeholder = zadata za malu.
- [ ] Run tests → PASS; `npx tsc --noEmit`.

### Task 2: Generator

**Files:** `GenerisiSedmicniSadrzaj.cs` (`Kandidat`, `Sastavi`), testovi generatora.

**Consumes:** `Product.NajmanjaKolicinaUKorpici()`.

- [ ] Testovi: (a) kultura sa zadatom 0,3 i najmanjom 0,3 nikad ne dobija 0,1; (b) kultura čija najmanja ne staje u budžet/zalihu se preskače; (c) prioritet 0 ulazi u punoj zadatoj količini; (d) težina smije do 5,5 kg za `TezinaDo` 5, ne preko; (e) marža nikad preko budžeta.
- [ ] Run → FAIL.
- [ ] `Kandidat`: najmanje = max(`NajmanjaKolicinaUKorpici`, korak); prioritet ≤ 0 → najmanje = standardno; standardno ≥ najmanje; ako `najvise < najmanje` → null (bez smanjivanja). `najvecaTezina = TezinaDo × 1,10` (const `DozvoljenoPrekoTezine = 0.10m`).
- [ ] Popravi postojeće testove koji su očekivali „pola zadate" — ledger ruling po testu.
- [ ] Run suite → PASS.

### Task 3: PUT sa količinama

**Files:** `MojaKorpica.cs` (`IzmijeniMojuKorpicuCommand/Handler`, `GetMojaKorpicaHandler`), `KorpiceDtos.cs`, `AccountController.cs` (body), `Scenario.cs`, testovi `MojaKorpicaIzmjenaTests.cs`.

**Produces:**
- `record StavkaKorpiceUnos(Guid ProductId, decimal? Kolicina)`
- `IzmijeniMojuKorpicuCommand(Guid OrderItemId, IReadOnlyList<StavkaKorpiceUnos> Stavke)`; null količina = šablonska za zadržan, najmanja za nov.
- Body `PUT /account/korpice/{id}`: `{ stavke?: [{productId, kolicina?}], productIds?: [] }` (productIds = sve null količine).
- `StavkaMojeKorpiceDto` i `PonudaArtiklaDto` dobijaju `Najmanje`, `Korak`.

- [ ] Testovi: povećanje krompira za korak prolazi; nov artikal ispod najmanje → `kolicina_premala`; 0,25 kg → `kolicina_premala`; decimalni komad → odbijen; zadržan šablonski red ispod nove najmanje bez promjene prolazi; preko nabavnog budžeta (a skuplje nego prije) → `marza`; DTO nosi najmanje/korak.
- [ ] Run → FAIL. Implement. Run suite → PASS.

### Task 4: Popuni umjesto mene

**Files:** nov `PopuniMojuKorpicu.cs`, `AccountController.cs`, test `PopuniMojuKorpicuTests.cs`.

**Produces:** `PopuniMojuKorpicuCommand(Guid OrderItemId, IReadOnlyList<StavkaKorpiceUnos> Stavke) : IRequest<IReadOnlyList<StavkaMojeKorpiceDto>>`; `POST /account/korpice/{id}/popuni` — **ne čuva**, vraća predlog.

Algoritam: dok vrijednost < 95 %: (1) povećaj stavku prioriteta 0 za korak, (2) dodaj kandidata (`AutoZamjena.KandidatiAsync`, bez izbjegnutih i već u nacrtu) u najmanjoj količini, (3) povećaj ostale stavke za korak — svaki korak samo ako vrijednost ostaje ≤ 100 %, marža i zaliha prolaze; bez napretka → kraj.

- [ ] Testovi: nacrt ispod 95 % se dopuni u 95–100 %; ne dodaje izbjegnute; preko 100 % vraća isto; marža se ne krši.
- [ ] Run → FAIL. Implement. Run → PASS.

### Task 5: Kupčev editor

**Files:** nov `frontend/src/lib/korpica-budzet.ts` + `.test.ts`, `components/nalog/korpica-editor.tsx` (prepisan), `lib/api/endpoints.ts`, `types/domain.ts`.

**Produces (lib):** `vrijednostNacrta(stavke)`, `procenat(vrijednost, cilj)`, `povecaj(s)`, `smanji(s) → stavka | null` (null = izbaci), `stanjeBudzeta(p) : "malo" | "ok" | "previse"`.

- [ ] Testovi lib-a (node --test): korak, ispod najmanje → null, procenat i stanje granice 95/100.
- [ ] Editor: lokalni nacrt, −/+/Izbaci, „Dodaj kulturu" (ponuda minus nacrt, minus izbjegnuti), traka „Iskorišteno N %", Sačuvaj (PUT), Popuni (POST popuni → nacrt), Vrati kako je bilo, proizvođač uz stavke, bez cijena.
- [ ] `npx tsc --noEmit`, lint, `npm test`; provjera u pregledniku.

### Task 6: Dokumentacija i završna provjera

- [ ] CLAUDE.md: pravilo najmanje količine, PUT sa količinama, popuni, generator (+10 % težine, osnova puna).
- [ ] Cijeli backend suite, frontend tsc/lint/test, ručno u pregledniku: generiši sadržaj, uredi korpicu.
