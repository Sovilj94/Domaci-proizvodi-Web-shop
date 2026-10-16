# Redizajn po farmit.rs — izgled i model rada

**Datum:** 2026-10-16
**Uzor:** farmit.rs, snimak u `docs/superpowers/farmit-snimak-2026-10-16.md` (tokeni, sekcije, mobilni prikaz)
**Grana:** jedna kratka grana po potprojektu (R1…R4), iz `main`

## Šta se radi

Kupčev dio E piljare dobija **izgled i način rada farmit.rs-a**: iste boje, font, oblik dugmadi i
kartica, isti raspored stranica i isti mobilni prikaz. Sve funkcije koje E piljara ima a farmit nema
(ćirilica, poeni i preporuke, promo kodovi, recepti, ocjena isporuke, izmjena sadržaja korpice, gost
checkout…) **ostaju** i dobijaju isti izgled.

**Ne kopira se** sadržaj farmit-a: njihovi tekstovi, fotografije, logo i naziv. Na tim mjestima idu
E piljara tekstovi (u istom tonu i dužini), E piljara fotografije i E piljara logo.

## Odluke korisnika (2026-10-16)

| Pitanje | Odluka |
|---|---|
| Izgled | „Identičan sajt", „iste stilove, boje, sve isto", „isti prikaz na mobilnim uređajima". |
| Koje korpice | **Samo povrće i voće**, tri veličine. Sir, suho meso, ajvar i zimnica se dodaju iz **Špajza** u istu narudžbu. Domaćinska, mliječna i suhomesnata korpica se **sakrivaju** (ostaju u kodu i bazi). |
| Planovi | **NEDJELJNA** = pretplata bez obaveze · **MJESEČNA** = obaveza na 4 isporuke · **3 MJESECA** = obaveza na 13 isporuka. Plaća se pouzećem pri svakoj isporuci. |
| Popust | **Odmah niža cijena** od prve isporuke (precrtana puna cijena), mjesečna i 3 mjeseca još niže. Zamjenjuje P3 pravilo „od šeste isporuke". |
| Ostalo | Što E piljara ima više — ostaje. |

## Dizajn sistem (svi potprojekti ga koriste)

Vrijednosti su izvučene iz farmit-ovog CSS-a.

| Token (Tailwind, `globals.css`) | Vrijednost | Mijenja |
|---|---|---|
| `primary` (+ `-200…-700`) | `#228B22` · `#E6F2E6` `#CEE5CE` `#B5D8B5` `#9DCB9D` `#84BF84` `#6CB26C` | današnji `brand-*` |
| `secondary` (+ `-200…-700`) | `#FE7E0F` · `#FFF1E4` `#FFD4AF` `#FFC695` `#FEB87A` `#FEAA60` | današnji `honey-*` |
| `beige` | `#FFF1E4` | današnji `cream` (pojasevi sekcija) |
| `ink` | `#394236` | naslovi i tekst |
| `muted` | `#6C757D` | sekundarni tekst |
| `line` | `#D9DBD8` | okviri |
| font | **Poppins** 400/500/600/700 (latin, latin-ext) za sve; na ćirilici **Montserrat** (Poppins nema ćirilicu) | DM Sans + Playfair + Onest |

Komponente:

- **Dugme glavno:** narandžasto, bijeli tekst 16px/600, `px-6 py-3`, **pill**. **Istaknuto:** isto, zeleno.
  **Okvir:** bijelo, okvir 0.8px narandžast, narandžast tekst. **Bijelo na fotografiji:** bijelo pill sa emoji.
- **Kartica:** bijela, `rounded-[26px]`, sjenka `0 10px 20px rgba(64,74,61,.3)`, hover `scale(1.02)`.
- **Naslov stranice** 60px/700 (mobilni 32px), **naslov sekcije** 48px/700 (mobilni 28px), centrirano, `ink`.
- **Pojasevi:** bijelo i `beige` naizmjenično; neki krem pojasevi imaju zaobljenu gornju ivicu (luk).
- **Obojene pločice namirnica:** svaka namirnica ima svoju boju (`Product.Boja`, vidi R1).

Admin panel nema sopstvene tokene, pa dobija nove boje i font automatski; raspored admina se ne mijenja.

## Dekompozicija

Svaki dio je zasebna grana, plan i provjera. Redoslijed je obavezan: R1 postavlja sistem na kom ostali grade.

### R1 — Osnova i početna

- **Tokeni, fontovi, dugmad, kartice** (gore). Zamjena vrijednosti postojećih tokena, pa se većina
  ekrana preboji odjednom; ostatak ide kroz R2–R4.
- **Zaglavlje:** logo · Početna · Korpice · Špajz · Analize · Recepti · „Prijavi se" (puno) i
  „Registruj se" (okvir) ili ime kupca · prekidač pisma (umjesto izbora jezika) · korpa. Na početnoj
  prozirno preko hero fotografije (bijeli tekst), drugdje bijelo sa zelenim linkovima i narandžastom
  podvlakom aktivnog.
- **Krem traka roka** ispod zaglavlja (osim početne), tekst iz servera („Poruči do utorka u 16h i stiže
  ti u četvrtak ili petak"); na mobilnom pokretna.
- **Mobilni meni** preko cijelog ekrana (X, pismo, stavke sa ikonom i strelicom, dole „Prijavi se" /
  „Registruj se" ili „Moj nalog" / „Odjava"). Zamjenjuje današnju donju traku (`MobileTabBar`).
- **Footer** po uzoru: kartica „Prijavi se i budi u toku" sa poljem e-maila, linkovi, kontakt.
  Prijava na novosti je **nova funkcija**: tabela `PrijavaNovosti` (e-mail, vrijeme, potvrđena?),
  `POST /novosti/prijava` (politika `auth`, honeypot), admin lista `/admin/novosti` sa izvozom CSV.
- **Početna**, deset sekcija istim redom kao uzor (snimak, „Početna"): hero karusel · Ova sedmica
  (odbrojavanje + obojene pločice sadržaja korpice povrća, „+N", „Naruči korpicu") · Utisci (karusel) ·
  „Sa njive do tvog stola" (pločice Povrće / Voće / Špajz) · Analize (karusel) · Kako funkcioniše ·
  fotografija sa porukom · Naši domaćini (karusel) · „Hoću moju korpicu!" · Česta pitanja.
  Današnje sekcije početne (P4) prelaze u ove; ništa od podataka se ne gubi.
- **`Product.Boja`** (hex, nullable) + polje u admin formi proizvoda; bez boje se uzima boja kategorije
  (mapa u kodu). Seed dodjeljuje boje postojećim proizvodima.
- **Fotografije:** do pravih, iz `public/` se koriste postojeće slike proizvoda; hero i korpice
  dobijaju fajlove `public/izgled/hero-1.jpg`… koje vlasnik zamijeni (popis u `PRODUCTION.md`).

### R2 — Korpice, planovi, stranica korpice

- **`/korpice/{vrsta}?plan=`** (vrsta `povrce`|`voce`): H1 „Izaberi korpicu", segment Povrće/Voće,
  segment planova NEDJELJNA · MJESEČNA · 3 MJESECA, tri kartice (Mala/Srednja/Velika: fotografija,
  „Korpica za 1 do 2 člana", četiri reda sa emoji, cijena plana „/korpica", „Odaberi korpicu", srednja
  zeleno + „Popularno"), link „Pogledaj sadržaj korpice" → modal sa obojenim redovima (namirnica,
  količina, mjesto, domaćin). Mobilni: horizontalne kartice sa zelenim okruglim dugmetom.
- **`/korpice/{vrsta}/{velicina}?plan=`**: galerija lijevo; desno veličina (segment), četiri reda, zelena
  info kutija (dan dostave, besplatna dostava ako važi), izbor **pretplata (plan) / jednokratna
  kupovina** sa precrtanom punom cijenom i uštedom, „Dodaj u korpu"; ispod „Ove sedmice u srednjoj
  korpici…" — obojene kartice. Pretplata se i dalje zaključuje kroz postojeći tok (adresa, slot).
- **Sakrivanje tri vrste:** `Product.IsAvailable = false` za domaćinsku, mliječnu i suhomesnatu (seed i
  migracija podataka); enum i kod ostaju. Stari linkovi `/korpice/{slug}` vode na novu stranicu.
- **Planovi na pretplati:** `Pretplata.Plan` (`Nedjeljna`|`Mjesecna`|`TriMjeseca`), `ObavezaIsporuka`
  (0 / 4 / 13). Cijena: `PretplatnaCijena.Izracunaj(puna, plan)` = puna × (1 − popust plana); popusti u
  `PretplateOpcije` (podrazumijevano 10 % / 15 % / 20 %). Postojeće pretplate dobijaju `Nedjeljna`
  (migracija). Pravilo „od šeste isporuke" se uklanja; `BrojIsporuka` ostaje (brojač obaveze).
- **Obaveza:** dok `BrojIsporuka < ObavezaIsporuka`, otkaz sa `/moj-nalog/pretplate` nije ponuđen
  („obaveza još N isporuka — javi nam se za otkaz"); pauza jeste. Poslije ispunjene obaveze plan se
  nastavlja po istoj cijeni dok se ne otkaže. **Rizik:** pouzećem se obaveza ne može naplatiti — kupac
  koji prestane da prima i dalje plaća samo primljeno. Prihvaćeno uz ovu UI prepreku.
- `KorpicaDto` nosi cijenu po planu (`cijene: { jednokratno, nedjeljna, mjesecna, triMjeseca }`); klijent
  ne računa popust.

### R3 — Špajz

- **`/spajz`** (današnji `/products` preusmjeren): H1, kategorije u krugovima (bočni skrol na
  mobilnom), „Zavolećeš ove proizvode…" + „Svi proizvodi", kartice proizvoda (siva pozadina slike,
  naziv, cijena lijevo, pakovanje desno, „Dodaj u korpu"; mobilni 2 kolone), pojas „Zato što znaš šta
  vrijedi" (tri ilustracije), „Zašto kod nas?". Filter po kategoriji i pretraga ostaju.
- Kategorije za dodatke (podaci, ne kod): Sir i mliječno · Suho meso · Ajvar · Zimnica i džemovi · Med ·
  Jaja. Seed ih postavlja; admin ih uređuje kao i danas.
- **Ubacivanje u korpicu:** na stranici korpice i u korpi blok „Dodaj iz Špajza" (4–6 proizvoda) — isti
  `Dodaj u korpu`, roba ide u istu narudžbu i istu dostavu kao korpica.
- Stranica proizvoda dobija isti izgled.

### R4 — Ostale kupčeve stranice

Isti izgled (tokeni, kartice, dugmad, naslovi, pojasevi, mobilni) za: korpa, checkout (svi koraci),
potvrda i praćenje narudžbe, prijava i registracija, moj nalog (svih 7 sekcija), analize, recepti,
domaćini, o nama, postani partner, 404 (nova, pod `(shop)`). Bez promjene ponašanja.

## Šta se ne dira

Backend tokovi narudžbe, poena, promo kodova, ćirilice, ruta i vozačke aplikacije; admin raspored;
plaćanje ostaje pouzećem.

## Testiranje

- Backend: planovi (cijena po planu, obaveza blokira otkaz, migracija postojećih pretplata na
  `Nedjeljna`), `PrijavaNovosti` (honeypot, duplikat e-maila je uspjeh bez novog reda), `Product.Boja` validacija hex.
- Frontend: `tsc`, `lint`, `npm test`, `npm run build`.
- Uživo, za svaku stranicu: desktop 1440 px i mobilni 390 px uporedo sa snimkom farmit-a; bez
  horizontalnog skrola; ćirilica (Montserrat) bez zaostale latinice; checkout korpice sa planom i
  dodatkom iz Špajza do kraja.

## Van opsega

Plaćanje karticom; „poklon uz prvu dostavu" (može kasnije kroz promo kodove); chat (Crisp) — umjesto
njega plutajuće dugme Viber/telefon nije u opsegu; engleski jezik.
