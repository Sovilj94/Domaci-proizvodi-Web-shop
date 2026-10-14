# P5 — Proizvođači, O nama, Recepti, Analize, Postani partner

**Datum:** 2026-10-14
**Grana:** `p5-sadrzaj` (iz `main`, HEAD `84f0dd9`)
**Mjesto u cjelini:** peti potprojekat (P1…P7); P1–P4 su spojeni u `main`
**Opseg iz dekompozicije:** Analize, Recepti, O nama, Postani partner; iz P4 prebačena stranica proizvođača

## Šta se radi

Pet javnih stranica koje odgovaraju na pitanje „ko stoji iza ovoga i mogu li mu vjerovati", i
tri admin ekrana koji ih pune:

| Ruta | Šta | Odakle sadržaj |
|---|---|---|
| `/proizvodjaci` | svi domaćini | postojeći `Producer` |
| `/proizvodjaci/[id]` | jedan domaćin: opis, selo, njegovi artikli, njegove analize | `Producer`, `Product`, `Analiza` |
| `/o-nama` | ko smo, kako radimo, kontakt (`#kontakt`) | statičan tekst |
| `/recepti`, `/recepti/[slug]` | recepti sa sastojcima iz ponude i dugmetom „Dodaj sastojke u korpu" | novi `Recept` |
| `/analize` | laboratorijski nalazi: šta je ispitano, gdje, kad, rezultat, PDF | nova `Analiza` |
| `/postani-partner` | forma za domaćinstvo koje hoće da prodaje preko nas | nova `PartnerPrijava` |
| `/admin/recepti`, `/admin/analize`, `/admin/partneri` | unos i obrada | — |

Uz to: kartice domaćina na početnoj vode na `/proizvodjaci/[id]`, sekcija dobija „Svi
domaćini →", a footer dobija kolonu „E piljara" sa linkovima na nove stranice.

## Zašto

Model (korpica, pretplata) traži povjerenje unaprijed: kupac plaća za sadržaj koji još ne zna.
Uzor (farmit.rs) to rješava javnim laboratorijskim nalazima („svi rezultati analiza su javno
dostupni"), receptima koji pokazuju šta se od sedmične korpice može skuhati, i licima
proizvođača. E piljara ima proizvođače u bazi od početka, ali nijednu stranicu na kojoj se
vide — kartica na početnoj ne vodi nikud.

„Postani partner" je druga strana iste predvidivosti iz P1: ugovorena proizvodnja traži više
domaćinstava, a danas ih nema kako naći osim telefonom.

## Šta se NE radi u P5

| Odloženo | Zašto / gdje |
|---|---|
| Upload slika i PDF-ova | Projekat nema upload nigdje; slike proizvođača su URL/putanja. Admin unosi URL (spec, O6). |
| Mejl adminu za novu prijavu partnera | Badge u admin navigaciji je dovoljan za sada (O9). |
| Kontakt forma | „O nama" nosi kontakt podatke; poruka ide telefonom (O4). |
| Filter proizvoda po proizvođaču na `/products` | Stranica proizvođača ga zamjenjuje; filter u katalogu je zaseban posao. |
| Recepti vezani za sedmičnu korpicu („šta skuhati od ove sedmice") | Lijepo, ali traži vezu sa `SedmicniSadrzaj`; nije u opsegu. |
| Komentari i ocjene recepata | P6 ili nikad. |
| Analize na kartici proizvoda | Prikazuju se na stranici proizvođača i na `/analize`. |

**Ne dira se:** checkout, `Order`, korpice, pretplate, rute, vozačka aplikacija.

## Odluke koje donosim ovdje (nisu bile postavljene)

| # | Odluka | Cijena ako je pogrešna |
|---|---|---|
| O1 | Jedan spec i jedna grana za svih pet stranica; tri nova entiteta, tri migracije (po jedna u tasku koji ih uvodi) | Dug plan (11 taskova). Dijelovi su nezavisni, pa se plan može prekinuti poslije bilo kog taska bez polovičnog stanja. |
| O2 | **Analize se nikad ne seeduju**, ni u demo seedu | Prazna stranica `/analize` u demou. Izmišljen laboratorijski nalaz je laž o hrani — gore od svakog praznog stanja. |
| O3 | Recepti se **seeduju u demo seedu** (dva, sa sastojcima vezanim za seed proizvode), ne u produkciji | Recept nije tvrdnja o stvarnosti kao utisak ili nalaz; prazna stranica recepata u demou ne pokazuje glavnu funkciju (dodaj sastojke u korpu). |
| O4 | „O nama" i „Kontakt" su **jedna stranica** (`/o-nama#kontakt`); kontakt podaci (telefon, adresa) se vade iz footera u `lib/kontakt.ts` | Redizajn je tražio „obje, kratke" — ovdje su dvije sekcije jedne stranice. Ako zatreba zasebna ruta, `/kontakt` postaje redirect na `/o-nama#kontakt`. |
| O5 | Tekst „O nama" pišem samo iz onoga što aplikacija stvarno drži (Manjača, Banja Luka i zone, pouzeće, sedmični ciklus, proizvođači iz baze) — **bez brojeva, godina osnivanja i imena osnivača** | Stranica je opštija nego što bi vlasnik napisao. Vlasnik dopisuje kroz kod (statičan tekst). |
| O6 | Slike recepata i PDF analiza su **URL ili putanja** koju admin unese, kao `Producer.PhotoUrl` | Admin mora fajl negdje postaviti sam. Upload je zaseban posao za sve slike odjednom. |
| O7 | Sastojak recepta je **red u tabeli** (`ReceptSastojak`) sa opcionom vezom na `Product`; koraci su **jedno tekstualno polje**, korak po redu | Koraci se ne mogu pojedinačno preurediti u adminu; dovoljno je za recept od 4–8 koraka. Sastojci su tabela jer jedino oni nose vezu na proizvod. |
| O8 | „Dodaj sastojke u korpu" dodaje samo sastojke **vezane za dostupan proizvod**, po jedan komad/jedinicu, kroz postojeći `add(product)`; ostali se samo nabrajaju | Količina iz recepta („150 g") se ne preračunava u jedinicu proizvoda (kg, kom). Kupac koriguje u korpi. |
| O9 | Prijava partnera nema mejl; admin navigacija dobija **badge sa brojem novih** (kao narudžbe) | Prijava stoji dok admin ne otvori panel. |
| O10 | Javni `POST /partneri/prijave` je bez prijave, iza postojeće politike ograničenja `"auth"` (10/min po IP) i sa **honeypot** poljem `web` koje čovjek ne vidi | Bot koji preskoči honeypot i ostane ispod 10/min može puniti tabelu; admin to vidi i odbija. CAPTCHA se ne uvodi. |
| O11 | Honeypot prijava se **tiho prihvata** (201 bez upisa) | Bot ne saznaje da je otkriven; čovjek koji je slučajno popunio skriveno polje gubi prijavu — praktično nemoguće jer polje nije vidljivo ni fokusabilno. |
| O12 | Analiza ima **rezultat `Uskladjeno` / `NijeUskladjeno`** i objavljuje se i kad nije usklađena | Nalaz koji pokazuje problem je i dalje javan — inače „svi rezultati su javni" nije istina. Admin može ne objaviti (`Objavljena = false`), ali to je njegova odluka, ne sistemska. |
| O13 | Analiza je vezana za **proizvođača** (obavezno) i opciono za proizvod; naziv uzorka je slobodan tekst („paradajz, berba 12.10.") | Analiza „cijelog gazdinstva" (tlo, voda) se unosi kao uzorak sa opisom. |
| O14 | Stranica proizvođača koristi **`GET /products?producer={id}`** — novi parametar postojećeg kataloškog upita | Katalog dobija filter koji UI kataloga još ne koristi. |
| O15 | Recept ima `Slug` jedinstven, pravljen iz naslova istim `Slug.From` kao proizvodi; slug se ne mijenja pri izmjeni naslova | Promijenjen naslov ostavlja stari slug — ali stari linkovi ne pucaju. |
| O16 | Nove stranice su klijentske komponente kao ostatak prodavnice (TanStack Query) | Nema SEO prednosti serverskog renderovanja; isto kao `/korpice` i `/products`. |

## Model podataka

### `Recept` i `ReceptSastojak` (`Domain/Entities/Recept.cs`)

```csharp
public enum TezinaRecepta { Lako, Srednje, Zahtjevno }

public class Recept : Entity
{
    public string Naslov { get; set; } = "";       // 3–120
    public string Slug { get; set; } = "";         // jedinstven
    public string Opis { get; set; } = "";         // 0–500, uvod
    public string? SlikaUrl { get; set; }
    public int VrijemeMinuta { get; set; }         // 1–600
    public int Porcije { get; set; }               // 1–20
    public TezinaRecepta Tezina { get; set; }
    public string Koraci { get; set; } = "";       // 10–8000; korak po redu (prazni redovi se ignorišu)
    public bool Objavljen { get; set; }
    public DateTime Kreiran { get; set; } = DateTime.UtcNow;
    public List<ReceptSastojak> Sastojci { get; set; } = [];
}

public class ReceptSastojak : Entity
{
    public Guid ReceptId { get; set; }
    public Recept? Recept { get; set; }
    public int Redoslijed { get; set; }
    public string Naziv { get; set; } = "";        // 1–100, „mladi luk"
    public string Kolicina { get; set; } = "";     // 0–40, „2 perca", „150 g"
    public Guid? ProductId { get; set; }           // veza na ponudu, opciona
    public Product? Product { get; set; }
}
```

Brisanje recepta briše sastojke (cascade). Brisanje proizvoda: veza `SetNull` — recept ostaje,
sastojak samo izgubi dugme. Proizvodi se u projektu ionako soft-brišu.

### `Analiza` (`Domain/Entities/Analiza.cs`)

```csharp
public enum RezultatAnalize { Uskladjeno, NijeUskladjeno }

public class Analiza : Entity
{
    public Guid ProducerId { get; set; }
    public Producer? Producer { get; set; }
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public string Uzorak { get; set; } = "";       // 2–120, „paradajz, berba 12.10."
    public string Laboratorija { get; set; } = ""; // 2–120
    public string BrojIzvjestaja { get; set; } = ""; // 0–60
    public DateOnly DatumUzorkovanja { get; set; }
    public string Ispitano { get; set; } = "";     // 2–300, „650+ pesticida, teški metali"
    public RezultatAnalize Rezultat { get; set; }
    public string Napomena { get; set; } = "";     // 0–1000
    public string? PdfUrl { get; set; }
    public bool Objavljena { get; set; }
    public DateTime Kreiran { get; set; } = DateTime.UtcNow;
}
```

`Producer` se ne briše dok ima analiza (`Restrict`) — isto pravilo kao sa proizvodima
(`producer_in_use`). Proizvod: `SetNull`.

### `PartnerPrijava` (`Domain/Entities/PartnerPrijava.cs`)

```csharp
public enum StatusPrijave { Nova, UObradi, Prihvacena, Odbijena }

public class PartnerPrijava : Entity
{
    public string Ime { get; set; } = "";          // 2–80, ime i prezime
    public string Gazdinstvo { get; set; } = "";   // 0–120
    public string Mjesto { get; set; } = "";       // 2–80
    public string Telefon { get; set; } = "";      // 6–30
    public string Email { get; set; } = "";        // 0–120, ako postoji mora biti ispravan
    public string Proizvodi { get; set; } = "";    // 5–1000, šta proizvodi i koliko
    public StatusPrijave Status { get; set; } = StatusPrijave.Nova;
    public string AdminNapomena { get; set; } = ""; // 0–1000
    public DateTime Kreirana { get; set; } = DateTime.UtcNow;
}
```

## API

```
GET    /products?producer={id}           postojeći katalog, novi filter (O14)

GET    /recepti                          objavljeni, najnoviji prvi; ReceptKratkoDto
GET    /recepti/{slug}                   objavljen; ReceptDto sa sastojcima i ProductDto za vezane
GET    /admin/recepti                    svi
POST   /admin/recepti                    ReceptInput { naslov, opis, slikaUrl?, vrijemeMinuta, porcije,
                                                       tezina, koraci, objavljen, sastojci[{naziv, kolicina, productId?}] }
PUT    /admin/recepti/{id}               isti ulaz; sastojci se zamjenjuju cijelom listom
DELETE /admin/recepti/{id}

GET    /analize?producer={id}            objavljene, po DatumUzorkovanja opadajuće
GET    /admin/analize                    sve
POST   /admin/analize                    AnalizaInput
PUT    /admin/analize/{id}
DELETE /admin/analize/{id}

POST   /partneri/prijave                 javno, rate limit "auth", honeypot `web` (O10, O11)
GET    /admin/partneri?status=           najnovije prve
PATCH  /admin/partneri/{id}              { status, adminNapomena }
```

Kodovi grešaka: `recept_nije_pronadjen` nije potreban — `NotFoundException` kao svuda;
`proizvod_ne_postoji` (400) kad sastojak ili analiza pokazuje na nepostojeći proizvod;
`proizvodjac_ne_postoji` (400) za analizu; brisanje proizvođača sa analizama → postojeći
`producer_in_use` (409) — provjera se proširuje i na analize.

Ključevi grešaka validacije na žici prate obrazac `input.<polje>`; sastojci
`input.sastojci[0].naziv`.

## Frontend

| Fajl | Šta |
|---|---|
| `lib/kontakt.ts` | telefon, adresa, opis područja — jedini izvor; footer i `/o-nama` ga čitaju |
| `app/(shop)/proizvodjaci/page.tsx` | mreža svih domaćina (ista kartica kao na početnoj, sada link) |
| `app/(shop)/proizvodjaci/[id]/page.tsx` | domaćin, njegovi artikli (`ProductCard`), njegove objavljene analize |
| `components/pocetna/domacin-kartica.tsx` | izvađena kartica; dijele je početna i `/proizvodjaci` |
| `app/(shop)/o-nama/page.tsx` | ko smo, kako radimo (korak po korak, bez brojeva), `#kontakt` |
| `app/(shop)/recepti/page.tsx`, `recepti/[slug]/page.tsx` | lista kartica; detalj sa sastojcima, koracima i dugmetom (O8) |
| `app/(shop)/analize/page.tsx` | tabela/kartice nalaza, zeleni/crveni bedž rezultata, link na PDF |
| `components/analize/analiza-kartica.tsx` | jedna analiza; dijele je `/analize` i stranica domaćina |
| `app/(shop)/postani-partner/page.tsx` | forma + potvrda poslije slanja |
| `app/admin/recepti`, `app/admin/analize`, `app/admin/partneri` | lista + drawer forma (obrazac *Proizvođači* / *Utisci*); partneri: lista sa filterom statusa i promjenom statusa |
| `components/admin/admin-nav.tsx` | tri stavke; „Partneri" sa badge-om broja `Nova` |
| `components/layout/shop-footer.tsx` | kolona „E piljara" (O nama, Domaćini, Recepti, Analize, Postani partner); kontakt iz `lib/kontakt.ts` |

Svaka nova javna stranica ima prazno stanje koje ne laže („Još nema objavljenih analiza" — ne
„sve je ispravno"), stanje greške i skeleton.

**Kontrast WCAG AA** za svaki novi par; bedž „Nije usklađeno" ne smije biti bijelo na
svijetlocrvenom.

## Greške i prazna stanja

| Stanje | Ponašanje |
|---|---|
| `/proizvodjaci/[id]` za nepostojeći id | „Domaćin nije pronađen" + link na `/proizvodjaci` |
| `/recepti/[slug]` za neobjavljen ili nepostojeći | isto, „Recept nije pronađen" |
| recept bez vezanih dostupnih sastojaka | dugme „Dodaj sastojke u korpu" se ne prikazuje |
| svi vezani proizvodi nedostupni | dugme ugašeno sa tekstom „Sastojci trenutno nisu u ponudi" |
| prijava partnera — greška validacije | poruke ispod polja (`input.*`), ne toast |
| prijava partnera — 429 | „Previše pokušaja, pokušajte za minut." |
| analiza bez PDF-a | bez linka, ostalo isto |

## Testiranje

**Backend (xUnit, `TestDb`):**
- Katalog: `producer` filter vraća samo artikle tog proizvođača, bez korpica.
- Recepti: javni upiti vraćaju samo objavljene; detalj nosi sastojke po redoslijedu i
  `ProductDto` za vezane; update zamjenjuje listu sastojaka (stari redovi nestaju); sastojak sa
  nepostojećim `productId` → `proizvod_ne_postoji`; jedinstven slug za dva ista naslova;
  validator (granice iz modela).
- Analize: javni upit samo objavljene, filter po proizvođaču, redoslijed po datumu;
  nepostojeći proizvođač → `proizvodjac_ne_postoji`; brisanje proizvođača sa analizom → 409.
- Partneri: prijava se upisuje sa statusom `Nova`; honeypot ne upisuje ništa a vraća uspjeh;
  validator (telefon obavezan, email ako postoji ispravan); admin filter po statusu; promjena
  statusa.

**Frontend:** nema test okruženja. `tsc`, `lint`, `npm run build` (dev server ugašen) i ručno u
pregledaču: svaka nova stranica na 375px i desktopu, recept → korpa, prijava partnera →
admin badge → promjena statusa, analiza na stranici domaćina.

## Rizici

| Rizik | Odgovor |
|---|---|
| Tekst „O nama" obeća nešto što ne stoji | O5 — samo činjenice koje aplikacija drži; recenzija provjerava svaku tvrdnju. |
| Javna forma kao kanal za smeće | O10/O11 — rate limit + honeypot; admin ima status `Odbijena`. |
| Sastojak vezan za proizvod koji postane korpica ili bude obrisan | Javni DTO nosi proizvod samo ako je `Vrsta == Obican` i nije obrisan; inače se sastojak prikazuje bez dugmeta. |
| PDF link vodi van sajta | Otvara se u novom tabu sa `rel="noopener noreferrer"`; admin je jedini koji ga unosi. |
| Tri migracije u jednoj grani | Svaka u svom tasku, svaka samo svoje tabele; recenzent provjerava `Up()`. |
