# E piljara — šta nije urađeno ili nije dobro urađeno

Stanje na dan **2026-10-15**, poslije spajanja P7 (`main` = `origin/main`). Plan P1–P7 je
završen; ovo je sve što je usput svjesno odloženo, nađeno u recenzijama ili fali do produkcije.
Detalji i istorija su u `STANJE.md`; ovaj fajl je radna lista — **kad se stavka uradi, briše se
odavde** (i upiše u `STANJE.md`).

Veličina: **S** = do sat-dva, **M** = pola dana, **L** = dan i više / treba spec.

---

## A. Prioritet — kupac ili admin to primijeti

| # | Šta | Gdje | Prijedlog | Vel. |
|---|---|---|---|---|
| A1 | **Admin nema ekran za pretplate.** Podatak (aktivne, pauzirane, koliko korpica je obećano za narednu sedmicu, `cekajuSadrzaj`) postoji samo na `GET /admin/pretplate?status=`. Nabavka ga čita kroz API. | `frontend/src/app/admin/` (novi `pretplate/`) | Lista + brojač „za narednu sedmicu" po korpici i veličini; isti obrazac kao `/admin/ocjene`. Backend gotov. | M |
| A2 | **Pretplatnik ne dobija obavijest kad mu sedmica bude preskočena** (sadržaj korpice nije objavljen do roka) — vidi tek po izostanku isporuke. Admin isto nema alarm. | `PretplatneNarudzbeService`, `IEmailService` | Mejl kupcu („ove sedmice nema isporuke, ne naplaćujemo") + mejl adminu kad `cekajuSadrzaj > 0` 24 h prije roka. Rizik R2 iz P3 speca. | M |
| A3 | **`/korpice` poslije roka govori o tekućoj sedmici** („rok istekao"), a checkout već prodaje za sljedeću. Početna i footer već prikazuju sljedeću (`aktivniKrug`). | `GetTekucaSedmicaHandler`, `rok-traka.tsx` | Izlog prebaciti na `aktivniKrug()`. Pažnja: mijenja i kad se gasi „Naruči korpicu" (spec P4, O7). | M |
| A4 | **Vremenska zona roka.** `DeliverySlot.TimeFrom` „16:00" se čita kao UTC → rok se zatvara u 18:00 po BiH, i kupcu se tako prikazuje. | `SedmicaHelper`, `lib/sedmica.ts`, `DeliverySlot` | Eksplicitna zona (`Europe/Sarajevo`) pri gradnji roka na serveru; prikaz već prati server. Dira sve rokove — testovi `SedmicaHelper`. | M |
| A5 | **Sedmica narudžbe/pretplate ne gleda izabrani `DeliverySlot`** — računa se iz slota sa najnižim `DayOfWeek`. Bezopasno dok su svi slotovi u istoj sedmici (čet, pet). | `CreateOrder`, `PretplatneNarudzbe`, `SedmicaHelper` | Riješiti zajedno sa A4, na oba mjesta odjednom. | S |
| A6 | **Prijava na novom uređaju ne preuzima pismo naloga** (ćirilica). Server šalje `AuthUserDto.pismo`, klijent ga ne čita. | `lib/auth/auth-context.tsx`, `lib/pismo/pismo.ts` | Poslije prijave: ako kolačić `pismo` ne postoji a nalog ima `Cirilica` → `postaviPismo("cir")` + reload. | S |
| A7 | **Nema sopstvene 404 stranice.** Na ćirilici je nepostojeća ruta 1,5 s prazna pa latinicom. | `app/(shop)/not-found.tsx` (novi) | Stranica u stilu sajta pod `(shop)` → dobija i provajder ćirilice. | S |
| A8 | **Stranice bez naslova** — nalog, checkout i još neke imaju podrazumijevani `<title>`. | `app/(shop)/**/page.tsx` (klijentske stranice) | `layout.tsx` sa `metadata.title` po sekciji (klijentska stranica ne može izvesti `metadata`). | S |
| A9 | **Obraćanje „ti/vi" je nedosljedno** (npr. praćenje narudžbe vs. „Moje narudžbe"). | cijeli kupčev dio | Jedan prolaz kroz sve tekstove; dogovor: „ti" (većina sajta već tako). | M |
| A10 | **Zahvala „Hvala na ocjeni!" se ne vidi** — refetch odmah prikaže „Tvoja ocjena". | ocjena isporuke (P6a), `narudzba/[token]`, „Moje narudžbe" | Kratka poruka/toast prije prikaza ocjene. | S |

## B. Novac i istovremenost (rijetko, ali dira iznos)

| # | Šta | Gdje | Prijedlog | Vel. |
|---|---|---|---|---|
| B1 | Dvije istovremene narudžbe istog kupca mogu potrošiti **iste poene** (O8). | `CreateOrderHandler`, `PoeniPravila` | Zaključavanje po kupcu (`SELECT … FOR UPDATE` nad korisnikom) ili serializable transakcija oko trošenja. | M |
| B2 | Limit promo koda se pri istovremenim narudžbama može **preći za jedan** (O10). | `PromoKodPravila`, `CreateOrder`, potvrda gosta | Isto kao B1 — zaključavanje reda koda pri upisu korišćenja. | M |
| B3 | Istovremena dvostruka promjena statusa iste narudžbe → drugom zahtjevu **500** (jedinstveni indeks poena). | `AdminOrders`, `DriverFeature` | Uhvatiti `DbUpdateException` jedinstvenog indeksa → 409 „narudžba je već promijenjena". | S |
| B4 | **Admin nema ekran za korekciju poena**, a poeni **nemaju rok trajanja**. `VrstaPoena.Korekcija` postoji, ekran ne. | admin, `PoeniStavka` | Ekran „Poeni kupca" (stanje, istorija, ručna korekcija sa razlogom). Istek: poslovna odluka. | M |
| B5 | **Preporuka ne radi kroz Google/Facebook registraciju** (kod iz linka se gubi). | `SocialAuthCommands`, `register/page.tsx` | Kod preporuke proslijediti kroz social login komandu. | S |
| B6 | Javna provjera promo koda otkriva da li je e-mail ranije naručivao („samo prva narudžba"). Pod rate limitom. | `POST /promo-kodovi/provjera` | Prihvaćeno; ako smeta — generička poruka „kod ne važi za ovu narudžbu". | S |
| B7 | Pregled na checkoutu i narudžba računaju subtotal **svaki u svojoj petlji** (klijent i server). Server mjerodavan. | `checkout/page.tsx`, `CreateOrder` | Pregled iz `POST /promo-kodovi/provjera` već nosi subtotal — koristiti njega. | S |
| B8 | `BrojIsporuka` pretplate je izveden podatak — ručna promjena statusa ga može razići sa stvarnim brojem isporuka. | `Pretplata`, `BrojacIsporuka` | Admin dugme „preračunaj" ili računanje upitom pri čitanju. | S |

## C. Adrese i mapa

| # | Šta | Gdje | Prijedlog | Vel. |
|---|---|---|---|---|
| C1 | **Adrese sačuvane prije P2c nemaju pin** (i one prenesene iz starog `User.Address`). | tabela `Addresses` | Batch geokodiranje kao `admin/orders/geocode-missing`. Treba radno geokodiranje (C3/D2). | S |
| C2 | Niko ne provjerava da **pin i tekst adrese** govore o istom mjestu (kad reverzno geokodiranje ne uspije). | `address-picker.tsx`, `AddressRules` | Upozorenje već postoji; opcija: server odbije tačku dalje od X km od geokodiranog teksta. | M |
| C3 | **Lokalno geokodiranje ne radi** — `dotnet` procesi na ovom računaru ne mogu van (socket 10013), `/geocode` vraća `{}`. U ručnim provjerama pin se postavlja klikom. | okruženje | Vlastiti Nominatim kontejner (`backend/nominatim/README.md`) — riješava i lokalno i produkciju (D2). | M |

## D. Do produkcije (`PRODUCTION.md`)

| # | Šta | Prijedlog | Vel. |
|---|---|---|---|
| D1 | **Viber je stub** (`LoggingViberService` samo loguje). Kupac bez e-maila ne dobija potvrdu. | Implementirati `IViberService` za BSP (Infobip/Twilio), `Viber__ApiKey`; poslovna odluka + ugovor sa provajderom. | M |
| D2 | **Geokodiranje ide na javni Nominatim** (adrese kupaca idu trećoj strani, 1 zahtjev/s). | Self-host (vidi C3), `Nominatim__BaseUrl`, `ThrottleMs=0`. | M |
| D3 | JWT bez opoziva — nema prisilne odjave. | Po potrebi: lista opozvanih ili kratki token + refresh. | M |
| D4 | Migracije se primjenjuju pri startu API-ja — za više instanci zasebni korak. | Kad bude više instanci. | S |
| D5 | Prvi pravi admin nalog, lokacija magacina, env varijable — checklista u `PRODUCTION.md`. | Proći checklistu pri deployu. | S |
| D6 | Pretraživači indeksiraju samo latinicu (ćirilica je samo preslovljavanje u pregledaču). | Svjesno (P7 spec). Ako zatreba: `/cir/...` rute sa serverskim preslovljavanjem. | L |

## E. Kvalitet koda i testova

| # | Šta | Gdje |
|---|---|---|
| E1 | Admin forme bez `label`/`htmlFor`, `role="dialog"`, zatvaranja na Escape — popraviti **sve odjednom**. | `utisak-form.tsx`, `producer-form.tsx`, ostale admin forme |
| E2 | Hidratacijska greška u traci kategorija (Suspense fallback `<div>`, komponenta `<nav>`). | `category-rail.tsx` |
| E3 | Četiri slaba testa (jedan samo provjerava da ne puca, jedan djelimično cirkularan, jedan suvišan assert, jedan tautološki). | `SedmicaZaNarudzbuTests`, `CreateOrderSedmicaTests`, `AdminKorpiceTests` |
| E4 | Razlika „broj narudžbi vs. broj redova" na pakerskoj listi nije testirana; editor ne provjerava da je red korpica. | `AdminKorpice.cs`, `BrojNarudzbi` |
| E5 | Klijent predviđa količinu 1 za ponovo dodat šablonski artikal (traka budžeta pokaže manje od servera). | `korpica-editor.tsx`; treba `PonudaArtiklaDto` sa šablonskom količinom |
| E6 | Pola feninga razlike JS/.NET zaokruživanja u editoru korpice (server mjerodavan). | `korpica-editor.tsx` |
| E7 | Sitnice P4: test granice godine ide na `SedmicaHelper` a ne kroz handler; update test utiska ne provjerava `Mjesto`/`Tekst`; nema testa za obrisan artikal u `Artikli`; mock vraća prazne `artikli`; red u footeru se pojavi tek kad stigne podatak. | razno |
| E8 | Sitnice P2b: mrtva `undefined` grana u praznom stanju admina; zastarjeli komentari. | razno |
| E9 | Admin putanje `Order.ToDto` ne učitavaju `Ocjena` (polje tamo nije u upotrebi). | `AdminOrders` |
| E10 | Utisak (P4) se ne može dokazati kao utisak stvarnog kupca kad ga admin unese ručno; samo oni iz ocjene nose `OcjenaId`. | `Utisak` |
| E11 | Ćirilica: tekst koji je kupac unio prikazuje se preslovljen („Kralja Petra I" → „Краља Петра И"). Ako smeta — `translate="no"` na prikazima imena/adrese. | `order-receipt.tsx`, nalog |

## F. Okruženje i repozitorijumi

| # | Šta |
|---|---|
| F1 | `Desktop\Kumov Web Shop` je detached HEAD daleko iza `main` — stara kopija. Ili obrisati ili `git switch main` pa pull, da ne zbunjuje. Radi se u `Desktop\epiljara-backend-wt`. |
| F2 | Vozačka aplikacija (`Desktop\EPiljaraVozac`) je na grani `dizajn-po-mockupu`; u testu `ApiClientTests.cs` ostalo je ime „Haso Begić" (demo imena treba da budu srpska). Promijeniti kad se ta grana završi. |
| F3 | Računar ima malo slobodne memorije (Visual Studio + tsserver zauzimaju ~4 GB) — API i Next dev server su gašeni zbog toga. Za ručne provjere zatvoriti nepotrebno. |
| F4 | Snimci ekrana preko Chrome ekstenzije povremeno padaju („renderer ne odgovara") iako stranica radi — provjere raditi skriptama (`javascript_tool`, `get_page_text`). |

---

## Prijedlog redoslijeda

1. **A1 + A2 + A3** — sedmični posao sa pretplatama (admin ekran, obavijest o preskočenoj sedmici, izlog korpica).
2. **A4 + A5** — vremenska zona i slot, zajedno (jedan spec, dira sve rokove).
3. **A6, A7, A8, A10, B3, B5** — male stvari, jedna grana.
4. **B1 + B2** — zaključavanje za poene i promo kodove.
5. **C3/D2** — vlastiti Nominatim, pa **C1**.
6. **D1** kad postoji ugovor sa Viber provajderom.
7. **A9, E1** — prolazi kroz cijeli sajt (tekst, pristupačnost).
