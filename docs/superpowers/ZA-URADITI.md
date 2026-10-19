# E piljara — šta nije urađeno ili nije dobro urađeno

Ažurirano **2026-10-19** (grana `zavrsetak`). Plan P1–P7 i redizajn R1–R4 su
završeni; ovo je sve što je usput svjesno odloženo, nađeno u recenzijama ili fali do produkcije.
Detalji i istorija su u `STANJE.md`; ovaj fajl je radna lista — **kad se stavka uradi, briše se
odavde** (i upiše u `STANJE.md`).

Veličina: **S** = do sat-dva, **M** = pola dana, **L** = dan i više / treba spec.

---

## A. Prioritet — kupac ili admin to primijeti

| # | Šta | Gdje | Prijedlog | Vel. |
|---|---|---|---|---|
| A2b | **Admin nema alarm za neobjavljen sadržaj** — kupcu se javlja (A2, urađeno), adminu ne. | `PretplatneNarudzbe`, `IEmailService` | Mejl adminu kad `cekajuSadrzaj > 0` 24 h prije roka, jednom po sedmici (isti obrazac kao `ObavijestPreskocena*`). | S |
| A9 | **Obraćanje „ti/vi" je nedosljedno** (npr. praćenje narudžbe vs. „Moje narudžbe"). | cijeli kupčev dio | Jedan prolaz kroz sve tekstove; dogovor: „ti" (većina sajta već tako). | M |

## B. Novac i istovremenost (rijetko, ali dira iznos)

| # | Šta | Gdje | Prijedlog | Vel. |
|---|---|---|---|---|
| B4 | **Admin nema ekran za korekciju poena**, a poeni **nemaju rok trajanja**. `VrstaPoena.Korekcija` postoji, ekran ne. | admin, `PoeniStavka` | Ekran „Poeni kupca" (stanje, istorija, ručna korekcija sa razlogom). Istek: poslovna odluka. | M |
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

Urađeno 2026-10-19 na grani `zavrsetak` (vidi `STANJE.md`): A1–A8, A10, B1–B3, B5 (A1, A3 i A7 su
bili zatvoreni već u redizajnu R2/R4; A5 je zatvoren kao odluka).

1. **A2b, B8, B7** — male stvari oko pretplata i checkouta. ← *sljedeće*
2. **A9, E1, E2** — prolazi kroz cijeli sajt (obraćanje „ti", pristupačnost admin formi, hidratacija trake kategorija).
3. **B4** — admin ekran poena kupca.
4. **C3/D2** — vlastiti Nominatim, pa **C1** (traži memoriju i desetine minuta uvoza; vidi F3).
5. **D1** kad postoji ugovor sa Viber provajderom.
