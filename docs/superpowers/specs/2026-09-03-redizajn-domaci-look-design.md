# Redizajn web shopa — „domaći" izgled

**Datum:** 2026-09-03
**Grana:** `redizajn-domaci-look`
**Referenca:** https://jungle-sauna-41415150.figma.site (Figma Make predložak „DomaćiTržnica")

## Šta se radi

Javni web shop prelazi na vizuelni jezik referentnog predloška: krem podloge,
duboka zelena i amber, serif display tipografija, naizmjenični ritam
svijetlo → tamno → svijetlo → zeleno. Admin panel se **ne dira**.

Ovo nije dogradnja postojeće palete nego njena zamjena. HEAD u ovom trenutku
nosi narandžasti akcenat (`dc407d3`); on se u cjelini gazi.

## Šta se ne radi

- Admin panel, dispečerski ekran, rute.
- Aplikacija za vozače (zaseban repozitorij).
- Bilo kakva izmjena HTTP ugovora, DTO-ova ili backend logike.
- Ocjene/zvjezdice na karticama proizvoda — `Product` nema polje za ocjenu,
  a izmišljanje broja na kartici nije opcija. Ako se ocjene budu htjele,
  to je zaseban backend zadatak.
- Newsletter forma iz reference — nemamo endpoint za prijavu, a dugme koje
  ništa ne radi je gore nego da ga nema. Zelena traka pred footerom ostaje
  naš CTA „Dostavljamo u tvoj kraj".

## Odluke i njihovi razlozi

### Tokeni se mijenjaju na jednom mjestu, imena ostaju

`globals.css` već nosi bilješku da imena tokena (`brand-*`, `sand`, `ink`,
`honey`) koristi 33 fajla i da se izgled mijenja tu, a ne po komponentama.
Ta odluka se poštuje: mijenjaju se **vrijednosti**, ne imena. Preimenovanje
`brand` u `green` bi značilo 33 fajla izmjena za nula vizuelne razlike.

Posljedica koju treba znati unaprijed: `brand-*` od ovog trenutka znači
zelenu, a ne narandžastu. Ime je ostalo tačno („boja brenda"), ali ko čita
samo ime očekivaće narandžastu.

### Paleta

| Token | Vrijednost | Uloga | Kontrast |
|---|---|---|---|
| `brand-600` | `#2D5016` | ispuna dugmadi, logo krug, zelene sekcije | bijelo na njoj **9.25:1** |
| `brand-500` | `#4A7230` | ikone, tanke linije |  |
| `honey-500` | `#C17F2A` | ukras; tekst **samo na tamnoj** | 4.91:1 na `#232018` |
| `honey-700` *(novi)* | `#8F5D1B` | nadnaslovi i linkovi na kremu | **5.02:1** na kremu |
| `cream` | `#F7F2E9` | pozadina stranice |  |
| `sand` | `#EFE8DA` | naizmjenična sekcija |  |
| `ink` | `#1A1710` | tekst | 16.04:1 na kremu |
| `muted` | `#5A5244` | sekundarni tekst | 6.91:1 na kremu |
| `muted-dark` *(novi)* | `#A79C86` | sekundarni tekst **na tamnoj** | 6.00:1 na `#232018` |
| `line` | `#E2D9C8` | hairline granice |  |
| `night` *(novi)* | `#232018` | tamna sekcija izdvojenih proizvoda |  |
| `night-card` *(novi)* | `#2E2B23` | kartica na tamnoj |  |
| `sage` *(novi)* | `#8DA876` | prigušeni akcent |  |

Pravilo koje se ne krši: **`honey-500` nikad ne nosi sitan tekst na kremu.**
Daje 2.97:1 i pada AA. Na kremu ide `honey-700`, na tamnoj `honey-500`.
Isto vrijedi za `muted` — `#5A5244` na tamnoj daje 4.03:1, zato postoji
`muted-dark`.

### Tipografija

`Playfair Display` (700) kao `--font-display`, `DM Sans` (400/500/700) kao
`--font-sans`. Oba **moraju imati `latin-ext` subset** — bez njega pucaju
č, ć, š, ž, đ, a cijeli sajt je na srpskom.

Tracking na `.font-display` sa `-0.02em` na `-0.01em`. Zbijanje koje je
odgovaralo Inter Tightu na serifu zatvara kontraforme.

Težine naslova sa 800 (`font-extrabold`) na 700 — Playfair na 800 nema
rez, browser bi ga sintetisao.

### Hero fotografija

`frontend/public/hero/hero-domacinstvo.jpg` — 2400×1000, 393 KB,
Pexels 33329205, licenca dozvoljava komercijalnu upotrebu bez potpisa.
Paradajz u drvenom sanduku, vijenci bijelog luka, pletene korpe.

Odbačene su pijačne fotografije sa čitljivim stranim natpisima
(„FENCHEL", „Sweet hungarian", cijene u £, „LES DOMAINES") — na heroju
preko cijelog ekrana se vide i odaju da nije naša pijaca.

Lijeva trećina kadra je **svijetli kameni zid**, pa gradijent slijeva mora
ostati na ≈0.85 nepokrivenosti. Sa slabijim zatamnjenjem krem tekst pada
ispod AA. Ovo je jedini razlog zašto je gradijent toliko jak.

## Sekcije naslovne

Redoslijed sekcija se ne mijenja — već se poklapa sa referencom.

1. **Hero** — full-bleed foto, gradijent slijeva, nadnaslov
   `BANJA LUKA · MANJAČA`, serif naslov u tri reda sa srednjim redom u
   amberu, dvije pilula-akcije. Navbar providan iznad heroja, krem na skrol.
2. **Zelena traka** — imena kategorija u amber verzalu sa ◆ razdjelnikom.
   **Bez emoji-ja** — uz prave fotografije emoji čita kao jeftin.
3. **Kategorije** — mozaik: jedna velika pločica (2 reda) + 4 male.
   Imamo 7 kategorija, mozaik nosi 5; ostale dvije iza `Sve kategorije →`.
4. **Izdvojeni proizvodi** — tamna sekcija `night`, filter-čipovi po
   kategoriji (klijentski, nad već učitanim proizvodima — bez novog
   zahtjeva), kartice `night-card`.
5. **Domaćini** — okrugli portreti, zelena oznaka, serif ime, amber lokacija.
6. **Obećanja** — tri kolone, lucide ikone (ne emoji), hairline sa amber ◆.
7. **Zelena traka pred footerom** — naš CTA sa zonama dostave.
8. **Footer** — tamni `#1A1710`, četiri kolone.

## Ostale stranice

Katalog, detalj proizvoda, korpa, checkout, prijava, registracija, narudžbe,
mobilni tab bar i cart drawer: nova paleta, pilula-dugmad, kartica proizvoda
usklađena sa onom sa naslovne. Bez izmjena rasporeda.

## Kako se provjerava

Nema automatskih testova u repozitoriju. Provjera je:

- `npx tsc --noEmit` i `npm run lint` bez grešaka.
- Svaka stranica otvorena na `localhost:3000` uz pokrenut backend na `:5080`
  i vizuelno upoređena sa referencom.
- Kontrast: svaki par iz tabele iznad provjeren računski, ne na oko.
- Prolaz kroz tok kupovine do potvrde narudžbe — redizajn ne smije razbiti
  korpu ni checkout.
