# Narudžbe bez odobravanja + nabavka po gazdinstvima — plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Narudžba se odmah prihvata, na roku se sama zaključava, a nabavka se radi po gazdinstvu, uz otkupni list u PDF-u.

**Architecture:** `Order.ZakljucanaU` (vremenska oznaka, a ne novi status) postavlja postojeći pozadinski posao. Nabavka koristi postojeći `GetPakerskaListaHandler` (već nosi proizvođača i nabavnu cijenu po redu), samo bez isključivanja `Pending`. Otkupni list je novi entitet sa snimkom stavki, koji se pravi iz te liste po `ProizvodjacId`. PDF se pravi na frontendu sa `pdfmake`.

**Tech Stack:** ASP.NET Core 9, EF Core 9 + Npgsql, MediatR, FluentValidation, xUnit + SQLite · Next.js 15, TanStack Query, pdfmake 0.3.

**Spec:** `docs/superpowers/specs/2026-11-25-narudzbe-bez-odobravanja-nabavka-design.md`

## Global Constraints

- Bez podataka u kodu: nema seed-a, a migracija samo mijenja šemu (CLAUDE.md, pravilo 2026-10-30).
- Novi validator počinje sa `RuleFor(x => x.Input).NotNull()`. Izuzeci nose konkretan `code`.
- Nullable polje u TS tipu je `?:`, nikad `| null` (API izostavlja null).
- JMBG, žiro račun i banka gazdinstva postoje **samo** u admin DTO-u, nikad u javnom `ProducerDto`.
- Korisnički tekst je na srpskom (latinica, ijekavica).
- Frontend mora proći `npx tsc --noEmit`, `npm run lint` i `npm run build`. Backend mora proći `dotnet build` bez upozorenja i `dotnet test`.
- Prije izmjene pravljenja narudžbe pokrenuti `grep -rn "new Order" backend/src --include=*.cs` i odlučiti za svako mjesto.
- Komande za backend se pokreću iz `backend/`, a za frontend iz `frontend/`.

## Review Focus

1. **Posao kasni:** u periodu od 15 minuta između roka i prolaza posla kupac ne smije uspjeti izmijeniti korpicu. Postojeća provjera roka u editoru mora ostati (test u Task 3).
2. **Narudžba napravljena poslije roka** (checkout bira sljedeću sedmicu, pretplata) ne smije biti zaključana na prolazu za staru sedmicu (test u Task 3).
3. **Drugi prolaz posla** ne smije praviti duple otkupne listove ni pomjerati `ZakljucanaU` (test u Task 3 i Task 5).
4. **Otkazana narudžba** se ne zaključava i ne ulazi u nabavku ni u otkupni list (testovi u Task 3 i Task 5).
5. **Gazdinstvo bez ijednog reda** te sedmice ne dobija prazan otkupni list (test u Task 5).

---

### Task 1: Šema — polja i tabele

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/Order.cs` (polje `ZakljucanaU`)
- Modify: `backend/src/EPiljara.Domain/Entities/Producer.cs`
- Create: `backend/src/EPiljara.Domain/Entities/OtkupniList.cs` (`OtkupniList` + `OtkupniListStavka`)
- Modify: `backend/src/EPiljara.Application/Common/Interfaces/IAppDbContext.cs`, `backend/src/EPiljara.Infrastructure/Persistence/AppDbContext.cs`
- Create: migracija `NabavkaIOtkupniListovi` (`dotnet dotnet-ef migrations add NabavkaIOtkupniListovi -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations`)

**Interfaces — Produces:**
- `Order.ZakljucanaU : DateTime?` (UTC, `null` znači otvorena)
- `Producer`: `NosilacIme`, `Bpg`, `Jmbg`, `Adresa`, `ZiroRacun`, `Banka`, sve `string?`
- `OtkupniList : Entity { string Broj; Guid ProducerId; Producer? Producer; int Godina; int Sedmica; DateTime Datum; bool Zastario; string? Napomena; DateTime CreatedAt; ICollection<OtkupniListStavka> Stavke }`
- `OtkupniListStavka : Entity { Guid OtkupniListId; Guid ProductId; string Naziv; Unit Jedinica; decimal Naruceno; decimal? Cijena; decimal? Primljeno; int Redoslijed }`
- `DbSet<OtkupniList> OtkupniListovi`, `DbSet<OtkupniListStavka> OtkupniListStavke`
- Indeksi: unique `Broj`, unique `(ProducerId, Godina, Sedmica)`. FK `ProducerId` je `Restrict`, a stavke se brišu kaskadno. `Jedinica` se čuva kao string (`HasConversion<string>()`, kao `Unit` drugdje).

- [ ] **Step 1:** Dodaj polja i entitete, registruj ih u kontekstu i napravi migraciju.
- [ ] **Step 2:** Provjeri da migracija sadrži samo `AddColumn`/`CreateTable`/`CreateIndex`, bez `InsertData`/`Sql`.
- [ ] **Step 3:** Pokreni `dotnet build` (0 upozorenja) i `dotnet test`. Očekivano: sve prolazi, kao i prije.
- [ ] **Step 4:** Commit: `feat(nabavka): šema — zaključavanje narudžbe, podaci gazdinstva, otkupni list`

### Task 2: Narudžba se pravi kao prihvaćena

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Orders/CreateOrder.cs:305`, `backend/src/EPiljara.Application/Features/Orders/GuestOrderRequests.cs:363`
- Test: `backend/tests/EPiljara.Application.Tests/PrihvatanjeNarudzbeTests.cs` (novi). Po potrebi ažuriraj postojeće testove koji očekuju `Pending`.

- [ ] **Step 1: Testovi koji padaju**
  - `Narudzba_prijavljenog_kupca_je_odmah_prihvacena`: `CreateOrderHandler` → `Assert.Equal(OrderStatus.Confirmed, order.Status)`.
  - `Potvrdjena_gostujuca_narudzba_je_odmah_prihvacena`: `ConfirmGuestOrderHandler` → `Confirmed`.
  - `Prihvatanje_ne_salje_mejl_o_potvrdi`: poslije `CreateOrder` `FakeEmail` nema poziva statusnog mejla za `Confirmed`, samo postojeću potvrdu narudžbe.
  Osnovu (setup) kopiraj iz `CreateOrderSedmicaTests` i `GuestOrderTests`.
- [ ] **Step 2:** `dotnet test --filter PrihvatanjeNarudzbeTests`. Očekivano: FAIL (status je `Pending`).
- [ ] **Step 3:** U oba mjesta postavi `Status = OrderStatus.Confirmed`. Na oba mjesta dodaj komentar sa razlogom (spec §1).
- [ ] **Step 4:** Pokreni `dotnet test`. Testovi koji su pretpostavljali `Pending` poslije kreiranja prilagođavaju se novom pravilu, a ne brišu.
- [ ] **Step 5:** Commit: `feat(narudzbe): nova narudžba je odmah prihvaćena`

### Task 3: Zaključavanje na roku (zamjenjuje odbijanje)

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Orders/ZakljucajNarudzbe.cs`
- Delete: `backend/src/EPiljara.Application/Features/Orders/OdbijNepotvrdjene.cs`, `backend/tests/EPiljara.Application.Tests/OdbijanjeNepotvrdjenihTests.cs`, mejl `ObavjestenjaPravila.OdbijeneAsync` ako nema drugih pozivalaca
- Modify: `backend/src/EPiljara.Infrastructure/BackgroundJobs/ZakljucavanjeSadrzajaService.cs`
- Modify: `backend/src/EPiljara.Application/Features/Korpice/MojaKorpica.cs` (`MojaKorpicaPravila`)
- Modify: `OrderDto` i njegovo mapiranje (`ZakljucanaU`), `frontend/src/types/domain.ts` (`zakljucanaU?: string`)
- Test: `backend/tests/EPiljara.Application.Tests/ZakljucavanjeNarudzbiTests.cs` (osnovu, slot pet. 16:00 sa cutoff 48 i sedmicu 41/2026, kopiraj iz obrisanog `OdbijanjeNepotvrdjenihTests`)

**Interfaces — Produces:**
- `public record SedmicaKljuc(int Godina, int Sedmica);`
- `public record ZakljucajNarudzbeCommand(DateTime? Sada = null) : IRequest<IReadOnlyList<SedmicaKljuc>>;` vraća sedmice u kojima je ovaj prolaz zaključao bar jednu narudžbu.
- `MojaKorpicaPravila.MozeSeMijenjati(Order o) : bool` = `o.Status is Pending or Confirmed && o.ZakljucanaU is null`. Postojeća provjera roka u editoru ostaje.

- [ ] **Step 1: Testovi koji padaju**
  - `Poslije_roka_narudzba_dobija_ZakljucanaU_i_ostaje_Confirmed`
  - `Prije_roka_narudzba_ostaje_otvorena` (`ZakljucanaU == null`, vraća praznu listu)
  - `Otkazana_se_ne_zakljucava`
  - `Drugi_prolaz_ne_mijenja_ZakljucanaU` (prvi prolaz `sada1`, drugi `sada2`. Vrijednost ostaje `sada1`, a drugi vraća praznu listu)
  - `Narudzba_sljedece_sedmice_ostaje_otvorena` (Sedmica 42 pri `sada` poslije roka 41)
  - `Stara_Pending_postaje_Confirmed` (bez obzira na rok)
  - `Vraca_zakljucane_sedmice` (`[new SedmicaKljuc(2026, 41)]`)
  - `Editor_odbija_zakljucanu_korpicu`: u `MojaKorpicaIzmjenaTests` je `ZakljucanaU` postavljen prije roka, pa PUT baca postojeći kod greške za nedozvoljenu izmjenu.
  - `Editor_odbija_poslije_roka_i_bez_zakljucavanja` (Review Focus 1: rok prošao, `ZakljucanaU == null`, odbija)
- [ ] **Step 2:** `dotnet test --filter ZakljucavanjeNarudzbiTests`. Očekivano: FAIL (tip ne postoji).
- [ ] **Step 3:** Implementiraj handler. Rok se računa isto kao u obrisanom `OdbijNepotvrdjene` (`SedmicaHelper.UcitajRokAsync` + `DatumIsporuke(...).AddHours(-cutoff)`), za `Status != Cancelled && ZakljucanaU == null`, uz jedan `SaveChangesAsync`. Uvedi `MozeSeMijenjati` i koristi ga na svim mjestima koja danas čitaju `StatusiKojiDozvoljavaju`.
- [ ] **Step 4:** U servisu `OdbijNepotvrdjeneCommand` zamijeni sa `ZakljucajNarudzbeCommand` i loguj broj sedmica. Task 5 ovdje dodaje pravljenje otkupnih listova.
- [ ] **Step 5:** Pokreni `dotnet build` (0 upozorenja) i `dotnet test`. Očekivano: PASS.
- [ ] **Step 6:** Commit: `feat(narudzbe): zaključavanje na roku umjesto odbijanja nepotvrđenih`

### Task 4: Uklanjanje potvrđivanja i nabavka bez „Na čekanju"

**Files:**
- Delete: `backend/src/EPiljara.Application/Features/Korpice/Potvrdjivanje.cs`, njegov endpoint u `AdminController.cs`, `PotvrdjivanjeSedmiceDto` (`KorpiceDtos.cs`), `adminApi.potvrdjivanje` i tip u frontendu
- Modify: `backend/src/EPiljara.Application/Features/Korpice/AdminKorpice.cs` (`GetPakerskaListaHandler`: uklanja se `!= Pending` na oba mjesta i polje `NaCekanju`. Ostaje samo `!= Cancelled`)
- Modify: `StavkaPakerskeDto` dobija `ProizvodjacId : Guid?` (`null` za obrisan artikal bez proizvoda)
- Modify: `backend/src/EPiljara.Application/Features/Admin/AdminZadaci.cs` (uklanjaju se zadaci `naCekanju` i „potvrđene" ako služe samo potvrđivanju) i `AdminDashboard.cs` (`PendingOrders` se uklanja)
- Test: `PakerskaListaTests` (test `Narudzba_na_cekanju_ulazi_u_nabavku_tek_kad_se_potvrdi` zamjenjuje se sa `Prihvacena_narudzba_odmah_ulazi_u_nabavku`), `AdminZadaciTests` (prilagoditi)

- [ ] **Step 1:** Napiši `Prihvacena_narudzba_odmah_ulazi_u_nabavku` (narudžba `Confirmed`, `ZakljucanaU == null` → artikal je u `Stavke`) i `Stavka_nosi_ProizvodjacId` (`Assert.Equal(proizvodjac.Id, stavka.ProizvodjacId)`).
- [ ] **Step 2:** Pokreni testove. Očekivano: FAIL (polje ne postoji).
- [ ] **Step 3:** Ukloni potvrđivanje, `NaCekanju` i `PendingOrders`. Dodaj `ProizvodjacId`.
- [ ] **Step 4:** Pokreni `dotnet build` i `dotnet test`. Očekivano: PASS.
- [ ] **Step 5:** Commit: `refactor(narudzbe): bez ekrana potvrđivanja; nabavka broji sve neotkazane`

### Task 5: Otkupni listovi — backend

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Nabavka/OtkupniListovi.cs` (komande, upiti, validatori)
- Create: `backend/src/EPiljara.Application/DTOs/NabavkaDtos.cs`
- Create: `backend/src/EPiljara.API/Controllers/NabavkaController.cs` (`[Authorize(Roles="Admin")]`)
- Modify: `ZakljucavanjeSadrzajaService.cs` (poslije zaključavanja se za svaku vraćenu sedmicu šalje `NapraviOtkupneListoveCommand`)
- Modify: `UpdateOrderStatusHandler` (`AdminOrders.cs`) i `IzmijeniKorpicuNarudzbeHandler` (`AdminKorpicaNarudzbe.cs`): kad je narudžba zaključana, poziva se `OtkupniListPravila.OznaciZastaroAsync`
- Test: `backend/tests/EPiljara.Application.Tests/OtkupniListoviTests.cs`

**Interfaces — Produces:**
- `NapraviOtkupneListoveCommand(int Godina, int Sedmica) : IRequest<int>` pravi listove koji nedostaju, po jedan za svaki `ProizvodjacId` iz `GetPakerskaListaHandler` te sedmice (preskače `null` i gazdinstvo bez redova). Stavke se pune ovako: `Naruceno = Kolicina`, `Cijena = NabavnaCijena`, naziv/jedinica, a `Redoslijed` ide po nazivu. `Datum = DateTime.UtcNow`. Ako sedmica nema nijednu zaključanu narudžbu, baca `ConflictException("…", "sedmica_nije_zakljucana")`. Gazdinstvo koje već ima list preskače se (idempotentno).
- `OtkupniListPravila.SljedeciBrojAsync(IAppDbContext db, int godina, CancellationToken ct) : Task<string>` vraća `OL-{godina}-{n:D4}`, gdje je `n` broj listova te godine + 1.
- `OtkupniListPravila.OznaciZastaroAsync(IAppDbContext db, int godina, int sedmica, CancellationToken ct) : Task` postavlja `Zastario = true` svim listovima sedmice (bez `SaveChanges`, to radi pozivalac).
- `GetOtkupniListoviQuery(int Godina, int Sedmica) : IRequest<IReadOnlyList<OtkupniListDto>>`
- `OsvjeziOtkupniListCommand(Guid Id) : IRequest<OtkupniListDto>` ponovo puni stavke iz pakerske liste i postavlja `Zastario = false`. Ako bilo koja stavka ima `Primljeno`, baca `ConflictException(..., "otkupni_list_primljen")`.
- `UnesiPrimljenoCommand(Guid Id, UnesiPrimljenoInput Input) : IRequest<OtkupniListDto>`, gdje je `UnesiPrimljenoInput(IReadOnlyList<PrimljenoStavkaInput> Stavke)` i `PrimljenoStavkaInput(Guid StavkaId, decimal? Primljeno)`. Validator: `Input` NotNull, `Primljeno >= 0`. Nepoznat `StavkaId` baca `DomainException(..., "stavka_nepoznata")`.
- `OtkupniListDto { Guid Id; string Broj; int Godina; int Sedmica; DateTime Datum; bool Zastario; OtkupniListProdavacDto Prodavac; IReadOnlyList<OtkupniListStavkaDto> Stavke; decimal? Iznos; decimal? IznosPrimljeno }`, gdje `Prodavac` nosi ime gazdinstva, selo, telefon i svih šest novih polja. `Iznos` se sabira samo preko stavki sa cijenom.
- Rute: `GET /admin/otkupni-listovi?godina=&sedmica=`, `POST /admin/otkupni-listovi` (body `{godina, sedmica}`), `POST /admin/otkupni-listovi/{id}/osvjezi`, `PUT /admin/otkupni-listovi/{id}/primljeno`.

- [ ] **Step 1: Testovi koji padaju** (osnova: `Scenario` iz `PakerskaListaTests`, sa dva gazdinstva):
  - `Jedan_list_po_gazdinstvu_sa_kolicinama_iz_nabavke`
  - `Gazdinstvo_bez_redova_nema_list`
  - `Otkazana_narudzba_ne_ulazi_u_list`
  - `Drugi_poziv_ne_pravi_duplikate` (vraća 0, broj listova je isti)
  - `Brojevi_su_redom_po_godini` (`OL-2026-0001`, `OL-2026-0002`)
  - `Nezakljucana_sedmica_vraca_sedmica_nije_zakljucana`
  - `Admin_otkaz_zakljucane_narudzbe_oznacava_list_zastarjelim`
  - `Osvjezi_puni_nove_kolicine_i_skida_Zastario`
  - `Osvjezi_poslije_unosa_primljenog_vraca_otkupni_list_primljen`
  - `Unos_primljenog_racuna_IznosPrimljeno`
  - `Artikal_bez_nabavne_cijene_ima_praznu_cijenu_i_ne_ulazi_u_Iznos`
- [ ] **Step 2:** `dotnet test --filter OtkupniListoviTests`. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj. Stavke iz pakerske liste uzimaj pozivom `new GetPakerskaListaHandler(db, opcije).Handle(...)` ili preko `ISender`, a ne kopiranjem upita.
- [ ] **Step 4:** Uveži u servis i kontrolere. `dotnet build` (0 upozorenja), `dotnet test` → PASS.
- [ ] **Step 5:** Commit: `feat(nabavka): otkupni listovi po gazdinstvu`

### Task 6: Gazdinstvo — podaci za otkupni list (admin)

**Files:**
- Modify: `backend/src/EPiljara.Application/Features/Admin/AdminProducers.cs` (`ProducerInput` + 6 polja, mapiranje u Create/Update, validator)
- Create: `AdminProducerDto` (u `CatalogDtos.cs` ili uz `AdminProducers.cs`) + `GetAdminProducersQuery`, ruta `GET /admin/producers`. Create/Update vraćaju `AdminProducerDto`.
- Modify: `frontend/src/components/admin/producer-form.tsx`, `frontend/src/app/admin/producers/page.tsx` (lista ide preko `adminApi.producers()`), `frontend/src/lib/api/endpoints.ts`, `frontend/src/types/domain.ts` (`AdminProducer`)
- Test: `backend/tests/EPiljara.Application.Tests/GazdinstvoPodaciTests.cs`

- [ ] **Step 1: Testovi koji padaju:** `Cuva_podatke_za_otkupni_list` (create, pa svih 6 polja dolazi u `AdminProducerDto`), `Javni_ProducerDto_nema_jmbg_ni_racun` (refleksijom: `typeof(ProducerDto)` nema svojstva `Jmbg`/`ZiroRacun`/`Banka`/`Bpg`), `Jmbg_mora_imati_13_cifara` (validator, ključ `input.jmbg`), `Prazna_polja_su_dozvoljena`.
- [ ] **Step 2:** Pokreni testove. Očekivano: FAIL.
- [ ] **Step 3:** Implementiraj. Validator: `Jmbg` je, kad je dat, `^\d{13}$`. Za `Bpg`, kad je dat, dozvoljene su samo cifre i najviše 20 znakova. Ostala polja imaju najviše 200 znakova. Prazan string se čuva kao `null`. U formi dodaj sekciju „Podaci za otkupni list" sa napomenom da su opcioni i da se štampaju na otkupnom listu.
- [ ] **Step 4:** `dotnet test` → PASS. U `frontend/` pokreni `npx tsc --noEmit` i `npm run lint`.
- [ ] **Step 5:** Commit: `feat(gazdinstva): podaci za otkupni list`

### Task 7: Spisak narudžbi bez potvrđivanja (frontend)

**Files:**
- Modify: `frontend/src/lib/orders.ts` (`STATUS_META.Pending` ostaje radi starih redova, ali tranzicija `Pending` se ne nudi kao radnja), `frontend/src/lib/admin/kanban.ts` (`KOLONE` bez `Pending`) + `kanban.test.ts`, `frontend/src/components/admin/narudzbe-tabla.tsx` (bez `zaPotvrdu`), `frontend/src/app/admin/orders/page.tsx`, `frontend/src/app/admin/pakovanje/page.tsx` (bez `Pending → Confirmed` koraka), `frontend/src/app/admin/dashboard/page.tsx` (bez kartice „Na čekanju"), `frontend/src/components/admin/spisak-nabavke.tsx` (briše se `NaCekanjuNapomena`)
- Create: `frontend/src/components/admin/traka-roka.tsx`

**Interfaces — Consumes:** `OrderDto.zakljucanaU?: string` (Task 3) i `PakerskaLista.rok`/`zakljucena` (postojeće) za izabranu sedmicu.

- [ ] **Step 1:** Ažuriraj `kanban.test.ts` tako da `KOLONE` ne sadrži `"Pending"`. `npm test` → FAIL.
- [ ] **Step 2:** Implementiraj. `TrakaRoka({ godina, sedmica })` čita pakersku listu za sedmicu i prikazuje „Sedmica N — otvoreno do {dan} u {HH:mm} · X narudžbi" ili „Zaključeno {datum} · X narudžbi · Nabavka →" (link `/admin/nabavka?datum=`). Dan i vrijeme se formatiraju preko `lib/sedmica.ts`. Na listi narudžbi postoji filter „Otvorene / Zaključene" po `zakljucanaU`, a kartica narudžbe ima bedž „Zaključena" kad je polje prisutno.
- [ ] **Step 3:** Pokreni `npm test`, `npx tsc --noEmit`, `npm run lint` i `npm run build`. Očekivano: sve prolazi.
- [ ] **Step 4:** Commit: `feat(admin): spisak narudžbi — otvorene i zaključene, bez potvrđivanja`

### Task 8: Nabavka po gazdinstvima i otkupni list u PDF-u (frontend)

**Files:**
- Modify: `frontend/src/app/admin/nabavka/page.tsx` (dvije kartice: „Po gazdinstvu" (podrazumijevano) i „Po vrsti robe" (postojeći prikaz))
- Modify: `frontend/src/lib/admin/nabavka.ts`: `poGazdinstvu(stavke: StavkaPakerske[]): GrupaGazdinstva[]`
- Create: `frontend/src/components/admin/nabavka-gazdinstva.tsx`, `frontend/src/components/admin/otkupni-list.tsx` (pregled i unos primljenog), `frontend/src/lib/admin/otkupni-list-pdf.ts`
- Modify: `frontend/src/lib/api/endpoints.ts` (`adminApi.otkupniListovi`, `napraviOtkupneListove`, `osvjeziOtkupniList`, `unesiPrimljeno`), `frontend/src/lib/api/contracts.ts` (`OtkupniList`, `OtkupniListStavka`, `UnesiPrimljenoRequest`; `StavkaPakerske.proizvodjacId?: string`)
- Test: `frontend/src/lib/admin/nabavka.test.ts`

**Interfaces — Produces:**
- `interface GrupaGazdinstva { proizvodjacId: string; naziv: string; selo: string; telefon?: string; redovi: RedNabavke[]; trosak: number | null }`. Gazdinstva su poredana po nazivu, a stavke bez `proizvodjacId` idu u grupu „Bez gazdinstva" na kraju.
- `dokumentOtkupnogLista(listovi: OtkupniList[], otkupljivac: { naziv?: string; adresa?: string; telefon?: string }): TDocumentDefinitions`. Za svaki list ide nova strana. Zaglavlje je `OTKUPNI LIST br. {broj}`, pa idu podaci otkupljivača i prodavca (prazno polje štampa liniju `________`), tabela (R. br., Artikal, Jed., Naručeno, Primljeno (prazno ako nije uneseno), Cijena, Iznos), zbir i dva potpisa „Prodavac" / „Otkupljivač" sa „Datum: ____". Otkupljivač dolazi iz `useSadrzajSajta`, ako tamo ima naziv/adresu/telefon. Inače su polja prazne linije.

- [ ] **Step 1: Test koji pada:** `nabavka.test.ts`: `poGazdinstvu` grupiše dva artikla istog gazdinstva u jednu grupu sa zbirom `trosak`, a stavka bez `proizvodjacId` ide u „Bez gazdinstva" posljednja. `npm test` → FAIL.
- [ ] **Step 2:** Implementiraj `poGazdinstvu` → `npm test` PASS.
- [ ] **Step 3:** Ekran. Kartica po gazdinstvu prikazuje ime, selo, telefon, tabelu i zbir. Kad je sedmica zaključana, nudi „Otkupni list (PDF)", a gore stoje „Svi otkupni listovi (PDF)" i bedž „Zastario — Osvježi količine" kad treba. Prije roka je žuta napomena „Predračun — narudžbe su otvorene do …", a otkupnih listova još nema. Ako je sedmica zaključana a listova nema, dugme „Napravi otkupne listove" (POST). Unos primljenog: polje po stavci i „Sačuvaj primljeno". Mutacije prate obrazac `useMutation` → invalidate → toast. Ako se koristi `useSearchParams`, ekran ide u `<Suspense>`.
- [ ] **Step 4:** Pokreni `npx tsc --noEmit`, `npm run lint` i `npm run build`. Očekivano: sve prolazi.
- [ ] **Step 5:** Commit: `feat(nabavka): spisak po gazdinstvu i otkupni list u PDF-u`

### Task 9: Dokumentacija

**Files:** `CLAUDE.md` (nova sekcija „Prihvatanje, zaključavanje i nabavka (2026-11-25)": `ZakljucanaU` umjesto statusa, `MozeSeMijenjati`, otkupni list kao snimak, `Zastario`, JMBG samo u adminu. Ispravljaju se rečenice koje tvrde da je narudžba `Pending` i da nabavka broji samo potvrđene, npr. „The unassigned set deliberately includes `Pending`"), `PRODUCTION.md` (JMBG/žiro račun su lični podaci gazdinstva i vide se samo u adminu).

- [ ] **Step 1:** Ažuriraj oba fajla. `grep -n "Pending\|potvrd" CLAUDE.md` ne smije ostaviti tvrdnju koja više ne važi.
- [ ] **Step 2:** Commit: `docs: prihvatanje, zaključavanje i nabavka po gazdinstvima`

### Task 10: Testni podaci u lokalnoj bazi (SQL, nije kod)

Radi se samo nad lokalnom dev bazom (`docker exec epiljara-postgres psql -U epiljara -d epiljara`). Skripta ide u scratchpad, **ne u repo**.

- [ ] **Step 1:** Napravi backup: `docker exec epiljara-postgres pg_dump -U epiljara -Fc epiljara > <scratchpad>/epiljara-prije-testnih.dump`.
- [ ] **Step 2:** Za svih 6 gazdinstava upiši izmišljene, očito testne podatke za otkupni list: `NosilacIme` (ime iz naziva porodice), `Bpg` (`TEST` nije dozvoljen validatorom, pa `9000000001`…), `Jmbg` `0101990100001`…, `Adresa` „{Selo} bb", `ZiroRacun` `5620000000000001`…, `Banka` „Testna banka".
- [ ] **Step 3:** Pobrini se da postoje narudžbe za **zaključanu sedmicu 48/2026** (rok uto 24. 11. 16:00 je prošao) i za **otvorenu sedmicu 49/2026**, sa korpicama i artiklima iz Špajza od više gazdinstava. Ako ih nema, kopiraj postojeće isporučene narudžbe (sa `OrderItems` i `StavkeKorpiceNarudzbe`) u te sedmice, sa novim `Id`/`Code`/`PublicToken`, `Status = 'Confirmed'` i `ZakljucanaU = null`. Posao će sam zaključati sedmicu 48.
- [ ] **Step 4:** Restartuj API. U roku od jednog prolaza posla (ili odmah, preko `POST /admin/otkupni-listovi {2026, 48}`) provjeri da narudžbe sedmice 48 imaju `ZakljucanaU`, a sedmice 49 nemaju, i da postoji po jedan otkupni list za svako gazdinstvo sa robom u sedmici 48.
- [ ] **Step 5:** Prođi kroz tok u browseru: spisak narudžbi (otvorene/zaključene), nabavka po gazdinstvu, PDF otkupnog lista, unos primljenog, uređivanje gazdinstva.
