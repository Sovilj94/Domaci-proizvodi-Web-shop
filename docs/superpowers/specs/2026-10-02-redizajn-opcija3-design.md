# Redizajn web shopa — „Opcija 3"

**Datum:** 2026-10-02
**Grana:** `redizajn-opcija3`
**Vizuelni izvor istine:** `C:\Users\soviljn\Desktop\E-piljara Grocery UI Showcase complete.png`

## Šta se radi

Web shop E piljare dobija nov izgled po priloženom mockupu: svijetla krem pozadina, tamno
zelena kao jedina jaka boja, bijele kartice, krupne fotografije, dosta praznog prostora.
Mijenja se izgled svih osam kupčevih ekrana i dodaju se četiri koje mockup traži a nemamo.

Ovo je prvenstveno UI posao. Backend se dira na tačno dva mjesta, oba zato što mockup prikazuje
funkciju koju nemamo: **omiljeni proizvodi** i **izmjena ličnih podataka**.

## Zašto

Današnji izgled je „domaći/rustikalni": topla krem podloga, amber akcenti, dvije **tamne**
sekcije (hero i „Izdvojeni proizvodi") i tamni footer. Mockup je suprotan — svijetao skroz,
sa zelenom kao jedinim akcentom i fotografijom kao nosiocem.

Razlika nije samo u paleti. Tamne sekcije su **raspored**, ne boja: promjena vrijednosti tokena
ih neće ukloniti. Zato redizajn nije „promijeni boje" nego prepravka strukture početne,
proizvoda i detalja, uz pretakanje tokena ispod svega.

## Šta se NE radi

Mockup prikazuje podatke koje backend nema. Ne izmišljaju se:

| Sa mockupa | Zašto otpada |
|---|---|
| Ocjene („4.8 (1.120 ocjena)"), tab Recenzije | `ProductDto` nema nikakvo polje za ocjenu |
| Tab „Nutritivne vrijednosti" (89 kcal, 1.1g proteina…) | nema podataka ni u entitetu ni u seedu |
| Badge „Akcija" | nema popusta ni stare cijene — koristi se `isFeatured` („Izdvojeno") i naziv kategorije |
| „Imaš kod za popust?" | nema kupona u domenu |
| Filter „Brend" (Domaće / Bio / Organsko / Uvoz) | proizvod nema takvo polje |
| Filter „Ocjena" | vidi gore |
| Traka thumbnaila na detalju | `Product.imageUrl` je **jedna** slika |
| „Kartično plaćanje (online)" | nema platne integracije |
| „Preuzimanje u prodavnici" | nema ni polja ni toka za to |
| Kategorije „Pekara / Zdrava hrana / Piće / Ostalo" | ne postoje; koriste se stvarne |
| Stranica „Kako radi" | ne pojavljuje se nigdje na screenshotu, samo u tekstu briefa |

Takođe se **ne dira**: admin panel, vozačka aplikacija, tok narudžbe, autentifikacija,
istorija narudžbi, optimizacija ruta.

## Odluke korisnika

| Pitanje | Odluka |
|---|---|
| Omiljeni (srce) | pravi backend, ne localStorage |
| Srce za neprijavljene | gost ga **ne vidi** |
| Moj nalog | nalog + adresar na postojećem `/account/addresses` |
| Lični podaci | dodati `PUT /account/profile` |
| Checkout | zadržati zonu i termin, prestilizovati ih |
| „O nama" i „Kontakt" | napraviti obje, kratke |
| Tempo | faza po faza, uz pregled u browseru |

## Vizuelni sistem

### Tokeni

Mijenjaju se **vrijednosti**, ne imena. Imena (`cream`, `sand`, `brand-*`, `ink`, `muted`,
`line`, `surface`) koristi 33 fajla; preimenovanje bi značilo stotine izmjena bez dobitka,
a `globals.css` je već napisan tako da se izgled mijenja odatle.

| Token | Staro | Novo | Uloga i kontrast |
|---|---|---|---|
| `cream` | `#f7f2e9` | `#f8f5ed` | pozadina stranice |
| `surface` | `#fffdf8` | `#ffffff` | kartice |
| `sand` | `#efe8da` | `#eef5ee` | svijetlo zelena površina (ikone benefita, čipovi) |
| `brand-600` | `#2d5016` | `#0e5b38` | ispuna dugmadi — **8.28:1** sa bijelim tekstom |
| `brand-700` | `#24400f` | `#083f29` | hover, tamno zelena |
| `ink` | `#1a1710` | `#171914` | tekst — **16.3:1** na `cream` |
| `muted` | `#5a5244` | `#5f6459` | sekundarni tekst — **5.66:1** na `cream` |
| `line` | `#e2d9c8` | `#e5e1d7` | hairline borderi |
| `radius-card` | `1rem` | `0.875rem` | 14px, sredina traženog raspona |

`brand-50…900` se izvodi oko novog `600`. Fontovi ostaju: Playfair Display za naslove,
postojeći sans za ostalo.

### Šta biva sa amber i tamnim tokenima

`honey-*` se **ne briše** — uvozi ga 33 fajla i brisanje bi oborilo build na mjestima koja
ovaj redizajn ne dira. Svodi se na jedan akcent, za badge „Izdvojeno".

`night` / `night-card` prestaju da nose sekcije. Ostaju samo za **donji promo baner**
(„Domaće. Svježe. Pouzdano.") i footer, gdje mockup i ima tamnu zelenu površinu.
Hero i „Izdvojeni proizvodi" se prepravljaju na svijetlo.

### Kontrast je obaveza, ne preporuka

Svaka kombinacija teksta i podloge mora proći WCAG AA (4.5:1 za sitan tekst). Tri para gore
su izračunata. Isto pravilo važi za svaki novi par koji se pojavi u implementaciji — npr.
bijeli tekst na `sand` (`#eef5ee`) **pada** i ne smije se koristiti.

## Rute

### Postojeće, redizajniraju se

`/` · `/products` · `/products/[slug]` · `/cart` · `/checkout`

### Nove

| Ruta | Sadržaj |
|---|---|
| `/kategorije` | desktop grid 3–4 po redu; mobile lista (slika, naziv, strelica). Klik → `/products?category=slug` |
| `/account` | Moj nalog: podaci, Moje narudžbe, Adrese, Omiljeni, Odjava |
| `/o-nama` | kratka stranica o piljari |
| `/kontakt` | telefon, email, područje dostave |

**Tekst za te dvije stranice niko još nije napisao, i ne izmišlja se.** Prave se sa
očiglednim rezerviranim tekstom, svaki pasus označen `{{ZAMIJENITI}}`, da se ne može
slučajno isporučiti kao gotov. Kontakt stranica isto: telefon i email stoje kao
`{{ZAMIJENITI}}` dok ih korisnik ne da. Jedino što se upisuje kao činjenica je područje
dostave — Banja Luka i okolina, što je već svuda u aplikaciji.

Putanju `/account` je već odredio odobreni spec `2026-09-04-moj-nalog-adresar-design.md`;
drži se ista da se ne razilaze dva dokumenta o istom ekranu.

### Bez rute

„Domaćini" ostaje sekcija `#domacini` na početnoj — već radi preko `useProducers()`.
„Kako radi" otpada.

## Header: screenshot nadjačava tekst briefa

Tekst briefa traži desktop navigaciju *Početna / Proizvodi / Kategorije / Domaćini / Kako radi
/ Kontakt*. **Na screenshotu toga nema.** Desktop header je:

```
red 1:  logo        [ search ]                    user · srce · korpa(badge)
red 2:  ◯Povrće ◯Voće ◯Mliječni proizvodi ◯Meso i suhomesnato ◯Jaja ◯Med i pčelinji ◯Zimnica
```

Imena u traci su **naša stvarna**, ne mockupova („Mlijeko i jaja", „Pekar", „Zdrava hrana",
„Piće", „Ostalo" ne postoje kod nas). Redoslijed ide po `Category.sortOrder`.

Tekstualni meni se na screenshotu pojavljuje **samo u mobilnom draweru**. Pošto je screenshot
proglašen izvorom istine, slijedi se screenshot: nema desktop tekstualnog nava, a traka
kategorija nosi sedam stvarnih kategorija (Povrće, Voće, Mliječni proizvodi, Meso i
suhomesnato, Jaja, Med i pčelinji proizvodi, Zimnica i džemovi).

Mobilno: gornji header (hamburger, logo, search, korpa) + donja traka sa pet tabova
(Početna, Proizvodi, Kategorije, Korpa, Profil), aktivan tab zelen, iznad safe-area.

## Početna

Sedam sekcija, redom kao na screenshotu:

1. **Hero** — svijetao split: lijevo nadnaslov „ZDRAVA ISHRANA", serif naslov
   „Bolji izbori za zdraviji život.", opis i zeleno dugme „Pogledaj ponudu →";
   desno velika fotografija (`/hero/hero-domacinstvo.jpg`). **Više nije tamni blok.**
2. **Četiri benefita** u jednom redu bijelih kartica — zelena ikona na `sand` kvadratu,
   naslov, sitan opis:
   | Naslov | Opis |
   |---|---|
   | 100% svježe | Provjereni kvalitet |
   | Domaći proizvodi | Podrška lokalnim proizvođačima |
   | Brza dostava | Banja Luka i okolina |
   | Sigurna kupovina | Vaši podaci su zaštićeni |
3. **Popularne kategorije** — `SectionHeader` + „Prikaži sve →" (vodi na `/kategorije`),
   red pločica sa slikom i nazivom.
4. **Izdvojeni proizvodi** — `SectionHeader` + mreža `ProductCard`-ova, `featured: true`.
   **Više nije tamna sekcija.**
5. **Domaćini** — postojeća `#domacini` sekcija preko `useProducers()`, prestilizovana.
6. **Promo baner** — tamnozelena površina preko fotografije, serif „Domaće. Svježe.
   Pouzdano.", podnaslov i svijetlo dugme. Ovo je jedno od dva mjesta gdje `night` ostaje.
7. **Footer** — drugo mjesto gdje `night` ostaje.

Postojeća animirana traka kategorija (`.animate-traka`, beskonačno pomjeranje) **se uklanja** —
na screenshotu je nema, a njenu ulogu preuzima statična traka u headeru.

## Stranica proizvoda: filtriranje je klijentsko

`GET /products` vraća **cijelu listu** — `GetProductsQuery` prima samo `Category`, `Search` i
`Featured`. Nema paginacije, sortiranja, raspona cijene ni filtera po proizvođaču.

Katalog je 18 proizvoda i 7 kategorija. Zato se **ne dira backend**: sortiranje, raspon cijene,
proizvođač, dostupnost i paginacija računaju se nad već učitanom listom. Pretraga ostaje
serverska, jer taj parametar postoji i radi.

Stanje filtera živi u URL-u (`?category=&cijena=&proizvodjac=&sort=&str=`) da se pogled može
podijeliti linkom i da dugme „nazad" radi.

> **`useSearchParams()` mora biti u `<Suspense>`.** Bez toga `npm run build` pada u prerender
> prolazu, dok `tsc` i `eslint` prolaze čisto. U ovom repozitoriju je to već oborilo `/login`,
> `/register` i `/admin/dostava`.

Logika filtriranja ide u `lib/catalog/filter.ts` kao čiste funkcije (ulaz: lista + kriterijumi,
izlaz: lista), da stranica ostane prikaz i da se ponašanje može provjeriti bez renderovanja.

Sidebar zadržava četiri sekcije kao na screenshotu, sa podacima koji postoje:

| Screenshot | Implementacija | Izvor |
|---|---|---|
| Kategorije (checkbox + broj) | isto | `useCategories()`, `productCount` |
| Cijena (slider) | isto, klijentski | `product.price` |
| Brend | **Proizvođač** | `useProducers()` |
| Ocjena | **Dostupnost** | `product.isAvailable` |

Grid 4×3 = 12 po strani. Mobilno: 2 kartice po redu, filteri u bottom sheetu sa dugmetom
„Primijeni filtere".

## Detalj proizvoda

Lijevo velika slika (bez thumbnaila — jedna je). Desno naziv, cijena, jedinica, stepper i
zeleno „Dodaj u korpu". Ispod četiri benefita. Bez zvjezdica.

Od četiri taba sa screenshota ostaju **dva**:

- **Opis** — `product.description`
- **Dostava** — stvarne zone i termini iz `deliveryApi.zones()` i `deliveryApi.slots()`

„Slični proizvodi" = proizvodi iste kategorije bez trenutnog; horizontalni slider na mobilnom.

## Kartica proizvoda

Jedan izgled za sva mjesta: slika 1:1 sa zaobljenim uglovima, badge gore lijevo
(naziv kategorije, ili „Izdvojeno" kad je `isFeatured`), srce gore desno (od Faze 5, samo
prijavljenima), naziv, proizvođač sitnije, cijena istaknuta sa jedinicom manjim tekstom
(`4,00 KM` + `/kg`), pa zeleno dugme preko cijele širine.

Na mobilnom dugme postaje kvadratno `+` u donjem desnom uglu kad red nosi dvije kartice —
tekst „Dodaj u korpu" tu ne stane čitljivo.

Zadržava se postojeće ponašanje: `add(product)`, toast, `flyToCart`, logika za nedostupan
proizvod i link na detalj.

## Korpa

Desktop: naslov „Korpa (X proizvoda)", lista u čistim redovima — slika, naziv, cijena po
jedinici, `QuantityStepper`, ikona za brisanje, iznos reda. Desno kartica sa međuzbirom,
dostavom i ukupnim iznosom, pa veliko zeleno „Nastavi na plaćanje →".

Mobilno: kompaktna kartica po proizvodu (slika lijevo, naziv i cijena desno, količina ispod),
sažetak na dnu.

**Polje za kod za popust se ne pravi** — kupona nema u domenu.

Dostava u sažetku dolazi iz izabrane zone; dok zona nije poznata, prikazuje se raspon ili
„računa se na naplati", nikad izmišljena nula.

## Checkout

Backend obavezno traži `deliveryZoneId` i `deliverySlotId`, a gost ide kroz dva poziva
(`/orders/guest-request` → mejl → `/orders/confirm`). Mockupov checkout nema ni zonu ni termin.

Rješenje: koraci se zadržavaju, ali dobijaju jezik i izgled mockupa.

1. **Podaci za dostavu** — ime, telefon, email, adresa (postojeći `AddressChooser` +
   `AddressPicker` za pin), napomena
2. **Način dostave** — zona i termin isporuke, kao radio-kartice u stilu mockupa
3. **Način plaćanja** — samo gotovina pri preuzimanju (jedina opcija koja postoji)
4. **Pregled narudžbe** — sticky na desktopu, na dnu na mobilnom

Gostu se u koraku 4 jasno kaže da stiže mejl sa linkom za potvrdu — to je postojeće ponašanje
i ne smije izgledati kao da je narudžba već kreirana.

## Moj nalog

Jedna stranica `/account`, bez prijave → redirect na `/login`.

- **Podaci** — ime, email, telefon. Izmjena preko novog `PUT /account/profile`.
  Email se ne mijenja (vidi spec od 2026-09-04 — traži ponovnu potvrdu, zaseban posao).
- **Moje narudžbe** — link na postojeći `/orders`
- **Adrese** — adresar nad postojećim `/account/addresses`
- **Omiljeni proizvodi** — vidi ispod
- **Odjava**

## Backend: dvije dopune

### Omiljeni proizvodi

Novi entitet `Favorite` (`EPiljara.Domain/Entities/Favorite.cs`), nasljeđuje `Entity`:

| Polje | Tip | Napomena |
|---|---|---|
| `UserId` | `Guid` | FK ka `Users`, **kaskadno brisanje** |
| `ProductId` | `Guid` | FK ka `Products`, kaskadno brisanje |
| `CreatedAt` | `DateTime` | |

Jedinstveni indeks na `(UserId, ProductId)` — isti proizvod se ne može dva puta dodati.

Endpointi na postojećem `AccountController`, svi `[Authorize]`:

- `GET /account/favorites` → `ProductDto[]`
- `POST /account/favorites/{productId}` → 204
- `DELETE /account/favorites/{productId}` → 204

**Proizvod je soft-delete** (globalni filter na `IsDeleted`). Upit za omiljene prolazi kroz taj
filter, pa obrisan proizvod nestaje iz liste sam od sebe — red u `Favorites` ostaje i to je u redu.

**Ponavljani `POST` ne smije rušiti.** Jedinstveni indeks bi dao 500; handler prvo provjeri
postoji li red i vrati 204 ako postoji. Isto za `DELETE` nepostojećeg.

Srce na kartici vide **samo prijavljeni korisnici** — gost ga ne renderuje. Klik je optimističan
(srce se odmah popuni), uz vraćanje u staro stanje i toast ako poziv padne.

### Izmjena ličnih podataka

`PUT /account/profile` (ime, telefon) sa `AbstractValidator`-om. Ovo dovršava **Task 4** plana
`2026-09-04-moj-nalog-adresar.md`, koji je specificiran i odobren ali nikad urađen.

> Čekboksi u tom planu **nisu održavani** — stoje neoznačeni iako su Task 0–3 urađeni.
> Ne vjerovati im; stanje je provjereno u kodu.

### Mock se ne proširuje

`frontend/src/server/mock/` je po `CLAUDE.md` već nepotpun — nema `account/*`, guest rute ni
pola admin ruta. Nove endpointe ne dobija. To je svjesna rupa, ne previd: mock je demo
kataloga i prijave, a ne drugi runtime na kojem se razvija funkcija.

## Komponente

### Nove

| Fajl | Uloga |
|---|---|
| `components/ui/section-header.tsx` | naslov sekcije + „Prikaži sve →" |
| `components/shop/benefit-cards.tsx` | četiri benefita; dijele ih početna i detalj proizvoda |

| `components/shop/category-tile.tsx` | slika + naziv, hover scale |
| `components/shop/product-filters.tsx` | filteri; renderuje se dvaput iz istog stanja |
| `components/layout/mobile-drawer.tsx` | hamburger side drawer |
| `lib/catalog/filter.ts` | čiste funkcije filtriranja i sortiranja |

### Prepravljaju se

`globals.css` · `ui/button.tsx` · `layout/shop-navbar.tsx` · `layout/mobile-tab-bar.tsx`
(4 → 5 tabova) · `shop/product-card.tsx` · `layout/shop-footer.tsx`

### Ostaju, samo naslijede tokene

`QuantityStepper` · `CartDrawer` · `AddressChooser` · `AddressPicker` · `toast` ·
`flyToCart` · `SmartImage` · `EmptyState` · `Spinner` · `StatusBadge`

### Granice

`product-card.tsx` je **jedino** mjesto koje zna kako proizvod izgleda — koriste ga početna,
`/products`, „Slični proizvodi" i Omiljeni. Ako se kartica negdje crta ručno, granica je
pukla.

`product-filters.tsx` se renderuje dvaput (desktop sidebar, mobile bottom sheet) **iz istog
stanja i iste komponente**, da se dvije verzije vremenom ne raziđu.

## Rubni slučajevi

- Prazan rezultat filtera → „Nema proizvoda za ove filtere" + dugme za poništavanje
- `useQuery` loading / error / prazno → postojeći `skeleton` i `EmptyState`
- Srce dok traje poziv → optimistično, vraćanje na grešku + toast
- `/account` bez prijave → redirect na `/login`
- Proizvod bez slike → postojeći `SmartImage` fallback
- Nedostupan proizvod → prigušena kartica, dugme „Nema na stanju" (postojeće ponašanje)

## Faze

| Faza | Sadržaj |
|---|---|
| 1 | Tokeni, button, section header, header + traka kategorija, bottom nav (5 tabova), product card, **početna** |
| 2 | `/products` (sidebar, bottom sheet, klijentski filteri i paginacija), detalj proizvoda |
| 3 | Korpa, checkout |
| 4 | `/kategorije`, `/account` + adresar, mobile drawer, `/o-nama`, `/kontakt` |
| 5 | Backend: `Favorite` + `PUT /account/profile`, pa srce na kartici i „Omiljeni" u nalogu |

Poslije svake faze korisnik pregleda rezultat u browseru prije nego što počne sljedeća.

### Dvije stvari koje faze presijecaju

**Srce se gradi u Fazi 1, pali u Fazi 5.** `product-card.tsx` se prepravlja u Fazi 1, a
endpointi stižu tek u Fazi 5. Do tada kartica **ne renderuje srce uopšte** — ni gostu ni
prijavljenom. Ne pravi se onemogućeno dugme koje ništa ne radi; prazno mjesto je iskrenije i
raspored se ne pomjera kad se srce kasnije uključi, jer mu je mjesto u gornjem desnom uglu
slike (apsolutno pozicionirano, van toka).

**„Omiljeni proizvodi" u `/account`.** Stranica se pravi u Fazi 4, stavka radi od Faze 5. U
Fazi 4 se ta stavka **ne prikazuje**; dodaje se u Fazi 5 zajedno sa srcem. Isti razlog:
nema mrtvih stavki u meniju.

## Provjera

Poslije svake faze, ovim redom:

```
npx tsc --noEmit        # typecheck
npm run lint            # eslint
npm run build           # OBAVEZNO — vidi ispod
```

`npm run build` nije opcion. `tsc` i `eslint` u ovom repozitoriju **prolaze čisto** na greškama
koje se pojave tek u Next-ovom prerender prolazu. Zeleni `tsc` i `lint` nisu dokaz da se
frontend može isporučiti.

Zatim prolaz kroz browser na **375 / 390 / 430 / 768 / 1024 / 1440 px**, uz provjeru:

- nema horizontalnog skrola ni na jednoj širini
- na mobilnom najmanje 2 proizvoda po redu
- tap target najmanje ~40 px
- donja navigacija iznad safe-area

Za faze koje diraju backend: `dotnet test` iz `backend/`.

Frontend nema test setup i ovaj posao ga ne uvodi — to bi bio zaseban projekat.
`lib/catalog/filter.ts` je napisan kao čiste funkcije pa je spreman za testove kad se setup
pojavi.

## Dnevnik

Tok rada se zapisuje u `docs/superpowers/plans/2026-10-02-redizajn-opcija3-dnevnik.md` —
status faza i hronologija, da se poslije prekida zna dokle se stiglo.
