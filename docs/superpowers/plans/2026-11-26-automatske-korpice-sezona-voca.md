# Automatske korpice i sezona voća: plan implementacije

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sedmični sadržaj korpica se sam sastavlja i objavljuje čim prođe rok. Voće postoji samo u sezoni
iz podešavanja, van nje ima poruku, prijavu na obavještenje i sezonsku pauzu pretplata.

**Architecture:** Nova MediatR komanda `AutoObjaviSadrzajeCommand` slaže postojeće handlere (generator i
objava) i zove se iz `ZakljucavanjeSadrzajaService` poslije zaključavanja. Pravilo sezone je jedna statička
klasa `SezonaVoca` nad `KorpiceOpcije.SezonaVocaOd/Do`, koje se pune iz `Podesavanja`. Koriste je automatika,
`KorpicaDto`, checkout i pretplate. Prijave „Javi mi" su nova tabela, a mejlovi idu kroz postojeći red
mejlova (`RedEmailService`).

**Tech Stack:** ASP.NET Core 9, EF Core 9 + Npgsql, MediatR, FluentValidation, xUnit + SQLite (`TestDb`);
Next.js 15, TanStack Query, Tailwind v4.

**Spec:** `docs/superpowers/specs/2026-11-26-automatske-korpice-sezona-voca-design.md`

## Global Constraints

- Nema podataka u kodu: mjeseci sezone i tekstovi poruke se ne upisuju u kod ni u migraciju (CLAUDE.md, pravilo vlasnika 2026-10-30).
- Farmit tekstove ne kopirati: „Never copy farmit texts, photos or logo."
- Novi tekstovi za kupce su na srpskom (latinica u izvoru; ćirilicu daje `PismoPoruke`/`preslovi`).
- Opcije nemaju podrazumijevane vrijednosti; testovi ih pune kroz `TestOpcije`.
- Nullable polja API ne šalje (`WhenWritingNull`), pa su u TS-u `?:`, nikad `| null`.
- Svaki novi validator komande počinje sa `RuleFor(x => x.Input).NotNull()`.
- Svaka `DomainException` nosi specifičan `code` kao drugi argument.
- `SedmicniSadrzaj.Status` se mijenja samo kroz postojeće handlere (`Predji`), nikad direktno.
- Oba mjesta koja prave `Order` (`CreateOrder.cs`, `GuestOrderRequests.cs`) dobijaju istu provjeru sezone.
- Migracije: `dotnet dotnet-ef migrations add <Ime> -p src/EPiljara.Infrastructure -s src/EPiljara.Infrastructure -o Persistence/Migrations` iz `backend/`; nikakav `InsertData`.
- Frontend je gotov tek kad prođu `npx tsc --noEmit`, `npm run lint` **i** `npm run build`.
- Backend build bez upozorenja (MSB3277 signal, CLAUDE.md).

## Review Focus

1. **Sezona preko nove godine** (`Od=11, Do=3`): u decembru je u sezoni, a u aprilu nije. Test u Task 1.
2. **Mjesec isporuke, ne današnji dan**: narudžba 30. aprila za isporuku u maju je u sezoni (maj–oktobar). Test u Task 1 (`MjesecIsporuke`) i Task 3.
3. **Dva prolaza posla istovremeno ili jedan za drugim** ne smiju napraviti dva sadržaja ni poslati dva mejla prijavama. Testovi „drugi prolaz → 0" u Task 2 i Task 5.
4. **Jedna korpica čija objava pukne** (`DomainException`, npr. obrisan artikal u nacrtu) ne smije zaustaviti ostale. Test u Task 2.
5. **Ista adresa prijavljena dvaput, ili sa razmacima i velikim slovima** daje jedan red i jedan mejl. Test u Task 5.

---

### Task 1: Sezona voća u podešavanjima i pravilo `SezonaVoca`

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/Podesavanja.cs` (sekcija Korpice: `int? SezonaVocaOd`, `int? SezonaVocaDo`)
- Modify: `backend/src/EPiljara.Application/Features/Korpice/KorpiceOpcije.cs` (ista dva polja, `int?`)
- Modify: `backend/src/EPiljara.Application/Features/Admin/Podesavanja.cs` (DTO, `PodesavanjaMapa.Korpice`, validator, upis)
- Create: `backend/src/EPiljara.Application/Features/Korpice/SezonaVoca.cs`
- Create: migracija `SezonaVoca`
- Modify: `backend/tests/EPiljara.Application.Tests/TestOpcije.cs` (bez sezone: oba `null`)
- Test: `backend/tests/EPiljara.Application.Tests/SezonaVocaTests.cs`, `PodesavanjaTests.cs`
- Modify: `frontend/src/types/domain.ts` (`Podesavanja`: `sezonaVocaOd?: number; sezonaVocaDo?: number`)
- Modify: `frontend/src/components/admin/poslovna-pravila.tsx` (dva select-a „Sezona voća od/do", opcija „—" = cijele godine)

**Interfaces:**
- Produces:
  - `public static class SezonaVoca` sa:
    - `bool UTijeku(KorpiceOpcije o, int mjesec)`: `true` kad su oba `null`; inače `Od <= Do ? Od <= m <= Do : m >= Od || m <= Do`.
    - `int MjesecIsporuke(int godina, int sedmica, DayOfWeek dan, string vrijemeOd)`: mjesec `SedmicaHelper.DatumIsporuke(...)` po `SedmicaHelper.Zona`.
    - `bool VanSezone(KorpiceOpcije o, TipKorpice? tip, int mjesec)`: `tip == Voce && !UTijeku(o, mjesec)`.
    - `int GodinaPovratka(KorpiceOpcije o, DateTime isporukaUtc)`: godina prvog `Od` mjeseca na ili poslije isporuke (decembar 2026 → 2027; mart 2027 → 2027).
  - `PodesavanjaDto.SezonaVocaOd/Do` (`int?`), na žici `sezonaVocaOd/sezonaVocaDo`.

- [ ] **Step 1: Failing testovi u `SezonaVocaTests`**
  - `Bez_podesene_sezone_voce_je_cijele_godine`: `Od=null, Do=null` → `UTijeku(o, m)` je `true` za m 1..12.
  - `Maj_oktobar`: `Od=5, Do=10` → `UTijeku` je `true` za 5 i 10, `false` za 4 i 11.
  - `Sezona_preko_nove_godine`: `Od=11, Do=3` → `true` za 12 i 1, `false` za 4.
  - `Povrce_nikad_nije_van_sezone`: `VanSezone(o{5,10}, TipKorpice.Povrce, 1)` je `false`.
  - `Mjesec_isporuke_ne_danasnji`: `MjesecIsporuke(2026, 18, DayOfWeek.Friday, "16:00")` je `5` (petak 1. 5. 2026).
  - `Godina_povratka`: `Od=5`, isporuka 2026-12-04 → `2027`; isporuka 2027-03-05 → `2027`.
- [ ] **Step 2: Failing test u `PodesavanjaTests`**: validator odbija samo jedno popunjeno polje (`Od=5, Do=null`), odbija `13` i prihvata oba `null`. Ključ greške je `podesavanja.sezonaVocaOd`.
- [ ] **Step 3: Pokreni** `dotnet test --filter "SezonaVoca|Podesavanja"` iz `backend/`. Očekivano: FAIL (tipovi ne postoje).
- [ ] **Step 4: Implementiraj** polja, mapu, validator (`Must(oba null ili oba 1–12)`, poruka „Unesi oba mjeseca ili nijedan.") i `SezonaVoca`. Zatim napravi migraciju `SezonaVoca` (dvije nullable `integer` kolone, bez `InsertData`).
- [ ] **Step 5: Pokreni** iste testove i cijeli `dotnet test`. Očekivano: PASS i build bez upozorenja.
- [ ] **Step 6: Frontend**: tip i dva polja u `poslovna-pravila.tsx` (labela „Sezona voća", pomoćni tekst „Prazno = voće cijele godine"). Pokreni `npx tsc --noEmit` i `npm run lint` u `frontend/`. Očekivano: čisto.
- [ ] **Step 7: Commit** `feat(korpice): sezona voća u podešavanjima`

---

### Task 2: `AutoObjaviSadrzajeCommand` i poziv iz posla

**Files:**
- Create: `backend/src/EPiljara.Application/Features/Korpice/AutoObjaviSadrzaje.cs`
- Modify: `backend/src/EPiljara.Infrastructure/BackgroundJobs/ZakljucavanjeSadrzajaService.cs`
- Test: `backend/tests/EPiljara.Application.Tests/AutoObjavaSadrzajaTests.cs`

**Interfaces:**
- Consumes: `SezonaVoca` (Task 1), `GenerisiSedmicniSadrzajHandler(db, opcije)`, `ObjaviSadrzajHandler(db, opcije)`, `SedmicaHelper.UcitajRokAsync/SedmicaZaNarudzbu`.
- Produces:
  - `public record AutoObjaviSadrzajeCommand(DateTime? Sada = null) : IRequest<AutoObjavaRezultat>;`
  - `public record AutoObjavaRezultat(int Godina, int Sedmica, int Objavljeno, bool PrvaVocnaUSezoni);`, gdje je `PrvaVocnaUSezoni = true` kad je u ovom prolazu objavljena bar jedna voćna korpica, a za prethodnu sedmicu nije bilo objavljenog/zaključanog voćnog sadržaja. Task 5 ga koristi.
  - Handler: `AutoObjaviSadrzajeHandler(IAppDbContext db, IOptions<KorpiceOpcije> opcije, ILogger<AutoObjaviSadrzajeHandler> log)`. Handlere generatora i objave pravi sam (`new ...Handler(db, opcije)`), bez `IMediator`, da test ne treba lažni medijator.

- [ ] **Step 1: Failing testovi** (`TestDb`, korpice povrće i voće kao u `ObjavaSadrzajaTests.Postavi`, bazen od ≥ minimum artikala sa `Mjeseci` koji pokrivaju ciljni mjesec, `Opcije(minimum: 1)` kao u tom testu, `Sada` fiksno, npr. 2026-12-02 10:00 UTC, bez `DeliverySlot` pa važi rezervni rok iz `TestOpcije`):
  - `Nova_sedmica_bez_sadrzaja_se_sastavi_i_objavi`: poslije poziva postoji `SedmicniSadrzaj` za `SedmicaZaNarudzbu(Sada)` sa `Status == Objavljen` i `Stavke.Count > 0`; `Objavljeno == 2` kad je sezona `null`.
  - `Objavljen_sadrzaj_se_ne_dira`: prethodno objavljen sadržaj ima iste stavke i `Id` poslije poziva; `Objavljeno == 0`.
  - `Nacrt_sa_stavkama_se_objavi_kakav_jeste`: nacrt sa jednom ručno unesenom stavkom (količina 3) je objavljen sa tom jednom stavkom i količinom 3.
  - `Prazan_nacrt_se_sastavi_iznova`: nacrt bez stavki → objavljen sa stavkama.
  - `Voce_van_sezone_se_preskace`: `SezonaVocaOd=5, Do=10`, decembar → voćna korpica nema sadržaj, a povrćna je objavljena; `Objavljeno == 1`.
  - `Drugi_prolaz_ne_radi_nista`: drugi poziv vraća `Objavljeno == 0`, a broj redova u `SedmicniSadrzaji` je isti.
  - `Pad_jedne_korpice_ne_zaustavlja_ostale`: nacrt voćne korpice sa stavkom na soft-obrisanom artiklu (`IsDeleted = true`) ostaje `Nacrt`, a povrćna je `Objavljen`.
  - `Neaktivna_korpica_se_preskace`: `IsAvailable = false` → nema sadržaja.
  - `Prva_vocna_u_sezoni`: maj, prethodna sedmica bez voćnog sadržaja → `PrvaVocnaUSezoni == true`; drugi poziv → `false`.
  - Prije assert-a nad `Stavke` pozovi `t.Db.ChangeTracker.Clear()` (CLAUDE.md, `TestDb` zamka).
- [ ] **Step 2: Pokreni** `dotnet test --filter AutoObjavaSadrzaja`. Očekivano: FAIL.
- [ ] **Step 3: Implementiraj handler.** Redoslijed po korpici je iz speca (odluka 1). Svaka korpica ide u svoj `try`: `DomainException` i `DbUpdateException` → `log.LogWarning` + `ChangeTracker.Clear()` → sljedeća korpica. Upozorenja generatora idu u `log.LogInformation`. Ništa ne baca prema pozivaocu osim `OperationCanceledException`.
- [ ] **Step 4: Pokreni** testove. Očekivano: PASS.
- [ ] **Step 5: Poziv iz posla.** U `ZakljucavanjeSadrzajaService`, odmah poslije `ZakljucajSadrzajeCommand`, ide `mediator.Send(new AutoObjaviSadrzajeCommand())` u svom `try/catch` (`log.LogError(ex, "Automatska objava sadržaja korpica nije uspjela.")`). Kad je `Objavljeno > 0`: `log.LogInformation("Automatski objavljeno sadržaja korpica za {Sedmica}/{Godina}: {Broj}", …)`. Ažuriraj XML komentar klase.
- [ ] **Step 6: Pokreni** cijeli `dotnet test`. Očekivano: PASS, build bez upozorenja.
- [ ] **Step 7: Commit** `feat(korpice): automatsko sastavljanje i objava sadržaja poslije roka`

---

### Task 3: Sezona u `KorpicaDto` i checkoutu

**Files:**
- Modify: `backend/src/EPiljara.Application/DTOs/KorpiceDtos.cs` (`KorpicaDto`: `bool VanSezone`, `int? SezonaOd`, `int? SezonaDo`)
- Modify: `backend/src/EPiljara.Application/Features/Korpice/KorpiceQueries.cs` (`ToKorpicaDto` dobija parametar `KorpiceOpcije? korpiceOpcije = null, int? mjesecIsporuke = null`; `GetKorpiceHandler`, `GetKorpicaBySlugHandler` računaju mjesec ciljne sedmice jednom)
- Modify: `backend/src/EPiljara.Application/Features/Korpice/SnapshotKorpice.cs` (`PricvrstiAsync`: provjera sezone ispred upita za sadržaj)
- Test: `backend/tests/EPiljara.Application.Tests/KorpiceQueriesTests.cs`, `CreateOrderKorpicaTests.cs`, `GuestOrderTests.cs`

**Interfaces:**
- Consumes: `SezonaVoca.VanSezone`, `SezonaVoca.MjesecIsporuke` (Task 1).
- Produces: na žici `vanSezone`, `sezonaOd?`, `sezonaDo?` (sezona se šalje samo za voćne korpice). `Dostupna` je `false` kad je `VanSezone`. Greška checkouta: `DomainException("Voćne korpice slažemo od {genitiv(Od)} do {genitiv(Do)}.", "voce_van_sezone")`.

- [ ] **Step 1: Failing testovi**
  - `KorpiceQueriesTests.Vocna_korpica_van_sezone_nije_dostupna`: sezona 5–10, `Sada` u decembru → voćni DTO ima `VanSezone == true`, `SezonaOd == 5`, `SezonaDo == 10`, `Dostupna == false`; povrćni ima `VanSezone == false`, a `SezonaOd` je `null`.
  - `CreateOrderKorpicaTests.Vocna_korpica_van_sezone_se_ne_moze_naruciti`: i sa objavljenim sadržajem (admin ručno) `CreateOrder` baca `DomainException` sa `Code == "voce_van_sezone"`.
  - `GuestOrderTests.Potvrda_gosta_odbija_vocnu_korpicu_van_sezone`: isti kod na potvrdi.
- [ ] **Step 2: Pokreni** testove. Očekivano: FAIL.
- [ ] **Step 3: Implementiraj.** `PricvrstiAsync` dobija `mjesec` iz `SezonaVoca.MjesecIsporuke` za `godina/sedmica` i dan isporuke iz `UcitajRokAsync`. Nazivi mjeseci u genitivu za poruku žive u `SezonaVoca` (`Genitiv(int)`: „januara" … „decembra"), jer su dio poruke interfejsa, a ne podatak.
- [ ] **Step 4: Pokreni** cijeli `dotnet test`. Očekivano: PASS.
- [ ] **Step 5: Commit** `feat(korpice): voćna korpica van sezone nije u prodaji`

---

### Task 4: Pretplate na voće van sezone: sezonska pauza

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/Pretplata.cs` (`int? ObavijestSezoneGodina`)
- Create: migracija `PretplataObavijestSezone`
- Modify: `backend/src/EPiljara.Application/Features/Pretplate/PretplatneNarudzbe.cs`
- Modify: `backend/src/EPiljara.Application/Features/Pretplate/AdminPretplate.cs` (`CekajuSadrzaj` ne broji voće van sezone)
- Modify: `backend/src/EPiljara.Application/Common/Interfaces/Services.cs` (`IEmailService.SendSezonskaPauzaAsync`)
- Create: `backend/src/EPiljara.Infrastructure/Services/SezonskaPauzaTemplate.cs` (po uzoru na `PreskocenaIsporukaTemplate`)
- Modify: `backend/src/EPiljara.Infrastructure/Services/Mejlovi/RedEmailService.cs`, `backend/tests/EPiljara.Application.Tests/FakeNotifications.cs`
- Test: `backend/tests/EPiljara.Application.Tests/PretplatneNarudzbeTests.cs`

**Interfaces:**
- Consumes: `SezonaVoca.VanSezone/MjesecIsporuke/GodinaPovratka` (Task 1).
- Produces: `Task SendSezonskaPauzaAsync(User kupac, string korpica, int mjesecPovratka, CancellationToken ct = default)`. Vrsta poruke u redu je `"sezonska_pauza"`, naslov „Voće se vraća u {lokativ(mjesec)}". Tijelo: „tvoja {korpica} pravi pauzu dok voćnjaci ne rode. Pretplata se nastavlja sama u {lokativ}, ništa ne moraš raditi. Do tada ništa ne plaćaš." Dugme „Moje pretplate". Ide kroz `PismoPoruke.Primijeni`.

- [ ] **Step 1: Failing testovi** (sezona 5–10, ciljna sedmica u decembru, aktivna pretplata na voćnu korpicu, objavljen povrćni sadržaj):
  - `Vocna_pretplata_van_sezone_ne_pravi_narudzbu_i_salje_jedan_mejl`: prolaz → 0 narudžbi za voće, `FakeEmail` ima tačno jedan `sezonska_pauza`, `ObavijestSezoneGodina == 2027`, `Status == Aktivna`, `PauzaDo*` su `null`.
  - `Drugi_prolaz_ne_salje_ponovo`: drugi prolaz (i sedmicu kasnije) → i dalje jedan mejl.
  - `Van_sezone_nema_mejla_o_preskocenoj_isporuci`: prolaz u sedmici poslije sedmice van sezone ne šalje `preskocena_isporuka` voćnoj pretplati.
  - `U_sezoni_narudzba_se_pravi_kao_prije`: maj, objavljen voćni sadržaj → narudžba napravljena.
  - `Alarm_adminu_ne_broji_voce_van_sezone`: u prozoru alarma, bez voćnog sadržaja → `SendAlarmSadrzajaAsync` se ne poziva za voćnu korpicu.
  - `AdminPretplate_cekaju_sadrzaj_bez_voca_van_sezone`: `CekajuSadrzaj == 0`.
- [ ] **Step 2: Pokreni** `dotnet test --filter PretplatneNarudzbe`. Očekivano: FAIL.
- [ ] **Step 3: Implementiraj.** `ObradiAsync`: provjera sezone prije provjere sadržaja → `ObavijestiOSezoniAsync(p, …)` (mejl jednom po `GodinaPovratka`) → `return false`. `ObavijestiOPreskocenimAsync` isključuje voćne pretplate kad je PRETHODNA sedmica (`pg/ps`) van sezone. `UpozoriAdminaAsync` i `AdminPretplate` isključuju voćne korpice van sezone. Migracija dodaje jednu nullable kolonu.
- [ ] **Step 4: Pokreni** cijeli `dotnet test`. Očekivano: PASS.
- [ ] **Step 5: Commit** `feat(pretplate): sezonska pauza voćnih pretplata`

---

### Task 5: „Javi mi kad stigne voće"

**Files:**
- Create: `backend/src/EPiljara.Domain/Entities/PrijavaSezone.cs` (`Email`, `TipKorpice`, `CreatedAt`, `DateTime? ObavijestenAt`)
- Modify: `IAppDbContext`, `AppDbContext` (`DbSet<PrijavaSezone> PrijaveSezone`; jedinstven filtrirani indeks `(Email, TipKorpice)` uz `HasFilter("\"ObavijestenAt\" IS NULL")`)
- Create: migracija `PrijaveSezone`
- Create: `backend/src/EPiljara.Application/Features/Korpice/PrijaveSezone.cs` (komanda prijave + validator; komanda `ObavijestiPrijaveSezoneCommand`)
- Modify: `backend/src/EPiljara.API/Controllers/KorpiceController.cs`, `Requests.Korpice.cs`
- Modify: `ZakljucavanjeSadrzajaService.cs` (poslije auto objave sa `PrvaVocnaUSezoni` → `ObavijestiPrijaveSezoneCommand`)
- Modify: `IEmailService`, `RedEmailService`, `FakeNotifications.cs`; Create: `SezonaKrenulaTemplate.cs`
- Test: `backend/tests/EPiljara.Application.Tests/PrijaveSezoneTests.cs`

**Interfaces:**
- Consumes: `AutoObjavaRezultat.PrvaVocnaUSezoni` (Task 2), `SezonaVoca` (Task 1).
- Produces:
  - `POST /korpice/{tip}/javi-mi` sa tijelom `{ email, web }` (`web` je honeypot, kao `POST /partneri/prijave`), `[EnableRateLimiting("auth")]`, odgovor `204` (`NoContent`; frontend ga zna iz `DELETE /account/addresses`).
  - `public record PrijaviNaSezonuCommand(string Tip, PrijavaSezoneInput Input) : IRequest;`, `PrijavaSezoneInput(string Email, string? Web)`.
  - `public record ObavijestiPrijaveSezoneCommand(TipKorpice Tip) : IRequest<int>;`
  - `Task SendSezonaKrenulaAsync(string email, CancellationToken ct = default)`, vrsta `"sezona_krenula"`, naslov „Stiglo je voće", dugme „Izaberi korpicu voća" → `{BaseUrl}/korpice/voce`.
- Greške: tip koji nije `voce` → `DomainException("Prijava je moguća samo za voće.", "prijava_nije_moguca")`; voće u sezoni → isti kod sa porukom „Voće je već u ponudi."

- [ ] **Step 1: Failing testovi**
  - `Prijava_cuva_normalizovan_email`: `"  Ana@Primjer.BA "` → red sa `ana@primjer.ba`.
  - `Dvostruka_prijava_daje_jedan_red`: dvaput ista adresa (druga sa velikim slovima) → 1 red, bez greške.
  - `Honeypot_ne_upisuje_nista`: `Web = "x"` → 0 redova, bez greške.
  - `Neispravan_email_je_greska_validacije`: ključ greške `input.email`.
  - `Prijava_u_sezoni_ili_za_povrce_odbijena`: kod `prijava_nije_moguca`.
  - `Obavijest_salje_svima_jednom`: 2 prijave → `ObavijestiPrijaveSezoneCommand` vraća 2, `FakeEmail` ima 2 `sezona_krenula`, oba reda imaju `ObavijestenAt`; drugi poziv vraća 0.
  - `Ponovna_prijava_poslije_obavjestenja`: obaviješten red + nova prijava iste adrese → novi red (filtrirani indeks to dozvoljava).
- [ ] **Step 2: Pokreni** `dotnet test --filter PrijaveSezone`. Očekivano: FAIL.
- [ ] **Step 3: Implementiraj** entitet, indeks, migraciju, komande, endpoint i template. Dupla prijava: prvo `AnyAsync` po normalizovanoj adresi bez `ObavijestenAt`. `DbUpdateException` iz trke se tretira kao uspjeh tek kad ponovna provjera pokaže da red postoji, inače `throw;`. Slanje ide po prijavi u svom `try`, a `ObavijestenAt` se upisuje i čuva po prijavi.
- [ ] **Step 4: Pokreni** cijeli `dotnet test`. Očekivano: PASS, build bez upozorenja.
- [ ] **Step 5: Commit** `feat(korpice): prijava na obavještenje kad stigne voće`

---

### Task 6: Frontend: `/korpice/voce` van sezone

**Files:**
- Modify: `backend/src/EPiljara.Domain/Entities/BlokSadrzaja.cs` (`SekcijeSadrzaja.KorpiceVoceVanSezone = "korpice-voce-van-sezone"`, dodato u `Sve`)
- Modify: `frontend/src/types/domain.ts` (`SEKCIJE_SADRZAJA` + `"korpice-voce-van-sezone"`; `Korpica`: `vanSezone: boolean; sezonaOd?: number; sezonaDo?: number`)
- Modify: `frontend/src/lib/api/endpoints.ts` (`korpiceApi.javiMi(tip: TipKorpice, email: string, web?: string): Promise<void>`)
- Create: `frontend/src/lib/mjeseci.ts` (`MJESECI_GENITIV`, `MJESECI_LOKATIV`, indeks 1–12; `popuniSezonu(tekst, {od, do, godina})` zamjenjuje `{od}` genitivom, `{do}` genitivom, `{u}` lokativom od `od` i `{godina}`)
- Create: `frontend/src/components/korpice/voce-van-sezone.tsx`
- Modify: `frontend/src/components/korpice/izbor-korpice.tsx`, `frontend/src/components/korpice/segmenti.tsx`
- Modify: admin ekran `/admin/sadrzaj` (nova sekcija u listi, sa nazivom „Korpice: voće van sezone" i napomenom o oznakama `{od}`, `{do}`, `{u}`, `{godina}`), pa u `frontend/src/components/admin/` nađi gdje se sekcije imenuju (`grep -rn "korpice-mogucnosti" frontend/src`)

**Interfaces:**
- Consumes: `Korpica.vanSezone/sezonaOd/sezonaDo` (Task 3), `POST /korpice/{tip}/javi-mi` (Task 5).
- Produces: `VoceVanSezone({ sezonaOd, sezonaDo }: { sezonaOd: number; sezonaDo: number })`.

- [ ] **Step 1: `IzborKorpice`**: za `vrsta === "voce"`, kad neka korpica te vrste ima `vanSezone`, umjesto kartica (i `LjepljiveKartice`) se renderuje `<VoceVanSezone …/>`. Ostale sekcije stranice ostaju.
- [ ] **Step 2: `VoceVanSezone`**: blokovi iz `useBlokovi("korpice-voce-van-sezone")` (naslov i tekst prvog bloka kroz `popuniSezonu`; `godina` = godina zadnjeg završenog `sezonaDo` u odnosu na danas, računato u efektu, ne u renderu). Zatim forma: polje „E-mail", skriveno polje `web`, dugme „Javi mi", poslije uspjeha „Javićemo ti čim voće stigne.", greška validacije ispod polja iz `err.fieldErrors["input.email"]`. Na kraju linkovi „Pogledaj korpice povrća" (`/korpice/povrce`) i „Špajz" (`/spajz`). Dugmad su `buttonVariants` (`primary`, `secondary`).
- [ ] **Step 3: `VrstaSegment`**: kad je voće van sezone, ispod naziva „Voće" ide sitno „od {genitiv(sezonaOd)}". Podatak dolazi iz `useKorpiceList()`, bez novog poziva.
- [ ] **Step 4: Provjera**: `npx tsc --noEmit`, `npm run lint`, `npm run build` u `frontend/`. Očekivano: sve čisto.
- [ ] **Step 5: Commit** `feat(shop): stranica voća van sezone sa prijavom na obavještenje`

---

### Task 7: Lokalni podaci, dokumentacija i prolaz kroz tok

**Files:**
- Modify: `CLAUDE.md` (novi odjeljak „Automatske korpice i sezona voća (2026-11-26)": automatika u poslu, `SezonaVoca` kao jedino pravilo, `voce_van_sezone`, sezonska pauza ≠ pauza, `PrijaveSezone`, sekcija `korpice-voce-van-sezone`)
- Modify: `PRODUCTION.md` (posle deploya: unijeti sezonu voća u `/admin/settings` i blok u `/admin/sadrzaj`; nova tabela sa e-mail adresama)

- [ ] **Step 1: Migracije lokalno**: restart API-ja (`dotnet run --project src/EPiljara.API --urls http://0.0.0.0:5080`) primjenjuje migracije u Developmentu.
- [ ] **Step 2: Podaci kroz admin** (kao `admin@epiljara.ba`, ne SQL-om): u `/admin/settings` unesi sezonu voća maj–oktobar. U `/admin/sadrzaj`, sekcija „Korpice: voće van sezone", unesi blok: naslov „Voće se vraća u {u}", tekst „Voće beremo samo kad ga voćnjaci daju, pa voćne korpice slažemo od {od} do {do}. Do tada je tu korpica povrća."
- [ ] **Step 3: Tok uživo**: u roku od 15 min poslije starta (ili odmah, jer posao radi prvi prolaz na startu) sedmica 49 ima objavljene povrćne korpice, a voćne nema. `/korpice/povrce` prikazuje tri kartice. `/korpice/voce` prikazuje blok, formu i linkove. Prijava adrese daje `204`, a red u `PrijaveSezone` postoji. Kupovina voćne korpice je nemoguća (`voce_van_sezone`). Mailpit (http://localhost:8025) ima mejl `sezonska_pauza` za aktivnu voćnu pretplatu.
- [ ] **Step 4: Commit** `docs: automatske korpice i sezona voća`
