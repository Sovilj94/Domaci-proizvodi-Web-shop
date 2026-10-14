# P4 — Marketinška početna

**Datum:** 2026-10-14
**Grana:** `pocetna-p4` (iz `main`, HEAD `092223a`)
**Mjesto u cjelini:** četvrti potprojekat (P1…P7); P1–P3 su spojeni u `main`
**Opseg iz dekompozicije (P1 spec):** hero, odbrojavanje, utisci, FAQ, proizvođači

## Šta se radi

Početna `/` prestaje biti izlog artikala i postaje stranica koja **prodaje korpicu**. Od P1
je korpica glavni proizvod, a početna o njoj ne kaže ni riječ: hero vodi na `/products`, a
korpice su dostupne samo kroz meni.

Nove sekcije, redom od vrha:

1. **Hero o korpici** — naslov, cijena „od X KM", dugme na `/korpice`, sporedno na
   `/products`, i **odbrojavanje** do roka naručivanja u samom heroju.
2. **Benefiti** — postojeće, ne mijenjaju se.
3. **Kako funkcioniše** — tri koraka (izaberi korpicu → mi je spakujemo → stiže na vrata).
4. **Popularne kategorije** i **Izdvojeni proizvodi** — postojeće, ne mijenjaju se.
5. **Domaćini** — postojeća sekcija, sada sa svim proizvođačima i onim što proizvode.
6. **Utisci kupaca** — novo; sadržaj unosi admin.
7. **Česta pitanja** — novo; harmonika.
8. **Promo baner** — postojeći, dugme vodi na `/korpice` umjesto `/products`.

Uz to i jedna popravka koju odbrojavanje izvlači na vidjelo: footer piše „Narudžbe do
utorka u 12h", što je zakucan tekst koji se ne slaže sa stvarnim rokom iz `DeliverySlot`-a.

## Zašto

Korpica je od P1 jedinica prodaje, a od P3 se na nju može i pretplatiti. Kupac koji prvi put
otvori sajt to ne saznaje: početna je ostala onakva kakva je bila u redizajnu „Opcija 3",
prije korpica.

Odbrojavanje nije ukras. Rok je stvarno pravilo (poslije njega sadržaj se zaključava i
narudžba ide za narednu sedmicu), a kupac ga do sada vidi tek na `/korpice`.

Utisci i FAQ odgovaraju na dva pitanja koja kupac ima prije prve narudžbe: „je li ovo
ozbiljno" i „kako tačno radi plaćanje, dostava i pretplata".

## Šta se NE radi u P4

| Odloženo | Gdje ide |
|---|---|
| Stranice O nama, Postani partner, Recepti, Analize | P5 |
| Zasebna stranica proizvođača (`/proizvodjaci/[id]`) | P5, uz „O nama" |
| Ocjena isporuke kao izvor utisaka | P6 — tada utisak može nastati iz ocjene; do tada ga unosi admin |
| FAQ koji admin uređuje | nije planirano; vidi O4 |
| Filter proizvoda po proizvođaču | nije planirano; kartica domaćina ne vodi nikud |
| SEO (metadata, JSON-LD za FAQ) | izvan opsega; početna je klijentska komponenta |

**Ne dira se:** checkout, `Order`, korpice kao proizvod, pretplate, rute, vozačka aplikacija,
ponašanje dugmeta „Naruči korpicu" na `/korpice` poslije roka.

## Odluke koje donosim ovdje (nisu bile postavljene)

Korisnik je tražio da ne pita. Sve niže su moje i svaka nosi cijenu ako je pogrešna.

| # | Odluka | Cijena ako je pogrešna |
|---|---|---|
| O1 | Utisci su **entitet u bazi** koji unosi admin, ne tekst u kodu | Za tri rečenice je to tabela, migracija i admin ekran. Ali utisak u kodu znači deploy za svaku izmjenu, a P6 (ocjena isporuke) treba mjesto gdje utisci žive. |
| O2 | **Nema seedovanih utisaka**, ni u demo seedu. Sekcija se ne prikazuje dok nijedan nije objavljen | Demo i nova instalacija izgledaju bez te sekcije dok admin ne unese prvi. Izmišljen utisak koji se pojavi kao stvaran na staging deployu (`Seed:Demo=true`) je gora greška od prazne sekcije. |
| O3 | Utisak ima `Objavljen` i `Redoslijed`; nema veze na kupca ni narudžbu | Ne može se dokazati da je utisak od stvarnog kupca. Veza na kupca dolazi sa P6 kao nullable kolona, bez diranja postojećih redova. |
| O4 | FAQ je **statičan u kodu** (`lib/cesta-pitanja.ts`), ali brojevi u odgovorima dolaze sa servera | Izmjena pitanja traži deploy. Brojevi (rok, dan isporuke, prag i iznos popusta) se ne kucaju u tekst — oni su konfiguracija i već su jednom pogrešno zakucani (footer). |
| O5 | Popust u FAQ i heroju se **izvodi iz `KorpicaDto`** (`cijena`, `cijenaSaPretplatom`, `isporukaDoPopusta`), bez novog endpointa | Za neprijavljenog kupca `isporukaDoPopusta` je prag (5), pa je „od šeste isporuke" tačno. Ako se `KorpicaDto` ikad počne personalizovati, FAQ bi prikazao kupčev broj umjesto pravila — tada treba javni endpoint pravila. |
| O6 | `/korpice/sedmica` dobija **sljedeći krug** (`SljedeciRok`, `SljedecaIsporuka`) | Aditivno polje u DTO-u; postojeći potrošači (traka roka, `/korpice`) ga ignorišu. Bez njega odbrojavanje poslije roka nema do čega brojati. |
| O7 | Odbrojavanje broji do `rokNarucivanja` dok rok nije prošao, a poslije do `sljedeciRok` sa porukom da je ova sedmica zatvorena | Isto tumačenje kao checkout (`SedmicaZaNarudzbu`): poslije roka se naručuje za narednu sedmicu. Izlog `/korpice` i dalje gasi dugme poslije roka — taj otvoreni nalaz ostaje kakav jeste, P4 ga ne rješava. |
| O8 | Odbrojavanje kuca svake sekunde, ali prikazuje sekunde samo u posljednja 24 sata | Do tada su dani/sati/minute dovoljni i stranica ne treperi. |
| O9 | Domaćini: svi proizvođači (do 8), uz do četiri imena **dostupnih, običnih** proizvoda (korpice se ne broje). `ProducerDto` dobija `Artikli: string[]` | Polje putuje i u admin listu proizvođača, gdje se ne koristi. Kartica i dalje ne vodi nikud, jer filter po proizvođaču ne postoji. |
| O10 | Footer čita rok i dan isporuke iz `/korpice/sedmica` umjesto zakucanog teksta. Zone ostaju zakucane | Popravka izvan užeg opsega, ali odbrojavanje bi inače na istoj stranici reklo jedan rok, a footer drugi. Zone se ne diraju jer se rjeđe mijenjaju i nisu dio P4. |
| O11 | Imena dana i padeži se vade iz `rok-traka.tsx` u `lib/sedmica.ts` | Tri nova potrošača (odbrojavanje, FAQ, footer) bi inače kopirala iste dvije liste. |
| O12 | „Kako funkcioniše" je statičan, tri koraka, bez brojeva iz konfiguracije | Ne obećava ništa mjerljivo, pa nema šta da se razide sa serverom. |
| O13 | Admin ekran utisaka je jedna stranica: lista + forma u istom ekranu (kao *Proizvođači*) | Bez paginacije; utisaka će biti desetak. |

## Model podataka

### Novi entitet `Utisak` (`Domain/Entities/Utisak.cs`)

```csharp
public class Utisak : Entity
{
    public string Ime { get; set; } = string.Empty;     // „Marija" ili „Marija P."
    public string Mjesto { get; set; } = string.Empty;  // „Borik"; može biti prazno
    public string Tekst { get; set; } = string.Empty;
    public bool Objavljen { get; set; }
    public int Redoslijed { get; set; }                 // manji ide prvi
    public DateTime Kreiran { get; set; } = DateTime.UtcNow;
}
```

`Entity` nosi samo `Id`, pa `Kreiran` služi kao drugi ključ redoslijeda. Tabela `Utisci`, migracija `DodajUtiske`.

**Pravila (validator):** `Ime` 2–60 znakova; `Mjesto` 0–60; `Tekst` 10–500; `Redoslijed`
0–999. Prazni stringovi se trimuju prije provjere.

### `SedmicaDto` dobija dva polja

```csharp
public DateTime SljedeciRok { get; init; }
public DateTime SljedecaIsporuka { get; init; }
```

Računaju se za sedmicu poslije tekuće, preko `SedmicaHelper.Pomjeri(godina, sedmica, 1)` i
istog `DatumIsporuke`/cutoff-a. Nema nove logike o rokovima — samo drugi argument.

### `ProducerDto` dobija `Artikli`

`IReadOnlyList<string> Artikli` — do četiri imena proizvoda tog proizvođača koji su
`IsAvailable` i `Vrsta == Obican`, po imenu (obrisane već skida globalni filter `IsDeleted`). Puni ga `GetProducersHandler` jednim upitom nad
`Products` grupisanim po `ProducerId`, ne N upita.

## API

```
GET    /utisci                    javno; samo objavljeni, po Redoslijed pa Kreiran
GET    /admin/utisci              svi, isti redoslijed
POST   /admin/utisci              tijelo: UtisakInput { ime, mjesto, tekst, objavljen, redoslijed }
PUT    /admin/utisci/{id}
DELETE /admin/utisci/{id}
```

Kontroler: `GET /utisci` u novi `UtisciController`; admin rute u `AdminController` uz ostale,
po istom obrascu kao proizvođači (`Envelope`, `EnvelopeCreated`, `EnvelopeList`).

## Frontend

| Fajl | Šta |
|---|---|
| `lib/sedmica.ts` | `DANI_AKUZATIV`, `DANI_GENITIV`, `formatRok(date)` — izvađeno iz `rok-traka.tsx` |
| `lib/cesta-pitanja.ts` | pitanja i odgovori; odgovor je funkcija koja prima `{ sedmica, popust }` |
| `components/pocetna/hero-korpica.tsx` | hero sa cijenom „od", dva dugmeta i odbrojavanjem |
| `components/pocetna/odbrojavanje.tsx` | brojač; čita `["korpice","sedmica"]`, invalidira ga kad dođe do nule |
| `components/pocetna/kako-funkcionise.tsx` | tri koraka |
| `components/pocetna/domacini.tsx` | izvađena sekcija sa početne, sa artiklima |
| `components/pocetna/utisci.tsx` | vraća `null` kad nema objavljenih |
| `components/pocetna/cesta-pitanja.tsx` | harmonika na `<details>`/`<summary>` (pristupačno bez JS-a) |
| `app/(shop)/page.tsx` | slaže sekcije; postojeće ostaju |
| `components/layout/shop-footer.tsx` | rok iz servera (O10) |
| `app/admin/utisci/page.tsx` + `components/admin/utisak-form.tsx` | admin ekran |
| `components/admin/admin-nav.tsx` | stavka „Utisci" |

**Cijena „od":** najniža `cijena` iz `GET /korpice` (bez `tip`), među `dostupna`. Kad lista
nije stigla ili je prazna, hero piše naslov i dugme bez cijene — ne izmišlja broj.

**Popust:** `procenat = round((1 - cijenaSaPretplatom / cijena) * 100)`, `od = isporukaDoPopusta + 1`.
Kad je `cijenaSaPretplatom >= cijena`, FAQ odgovor o pretplati ne spominje popust.

**Kontrast:** svaki novi par tekst/podloga prolazi WCAG AA (pravilo iz redizajna
„Opcija 3"). Odbrojavanje stoji na `surface`/`sand`, ne na fotografiji.

## Greške i prazna stanja

| Stanje | Ponašanje |
|---|---|
| `/korpice/sedmica` ne odgovori | hero bez odbrojavanja; footer bez reda o roku; FAQ odgovori bez brojeva roka (rečenica ostaje tačna bez njih) |
| `/korpice` prazan | hero bez „od X KM"; FAQ o pretplati bez procenta |
| nema objavljenih utisaka | sekcija se ne renderuje |
| `/utisci` greška | sekcija se ne renderuje (nije kritična) |
| proizvođač bez aktivnih artikala | kartica bez reda artikala |
| odbrojavanje dođe do nule | `invalidateQueries(["korpice","sedmica"])`; dok novi odgovor ne stigne pokazuje „Rok je upravo istekao" |

## Testiranje

**Backend (xUnit, `TestDb`):**
- `SedmicaDto`: sljedeći rok je tačno sedam dana poslije roka; prelaz godine (sedmica 52/53 → 1).
- Utisci: javni upit vraća samo objavljene i u redoslijedu; validator odbija kratak tekst i
  prazno ime; update i delete nepostojećeg daju `NotFoundException`.
- Proizvođači: `Artikli` ne sadrži korpice ni neaktivne, ima najviše četiri.

**Frontend:** nema test okruženja. Provjera: `tsc`, `lint`, `npm run build` (sa ugašenim dev
serverom) i ručno u pregledaču — početna na 375px i desktopu, odbrojavanje prije i poslije
roka (pomjeranjem roka kroz `DeliverySlot.CutoffHours` u dev bazi), admin unos utiska koji
se pojavi na početnoj.

## Rizici

| Rizik | Odgovor |
|---|---|
| Odbrojavanje i checkout razilaze se oko sedmice | Oba čitaju isti `DeliverySlot` preko `SedmicaHelper`; sljedeći krug se računa istim `DatumIsporuke`. |
| Vremenska zona na `DeliverySlot` | Naslijeđeno iz P1 (rok se zatvara 2h kasnije od nominalnog). Odbrojavanje je dosljedno serveru, pa broji do istog trenutka u kojem se rok stvarno zatvara. |
| Hidratacija sa satom | Početna je klijentska komponenta; brojač se računa tek u efektu, pa server i klijent ne renderuju različit tekst. |
| Prazna početna na novoj instalaciji | Svaka nova sekcija ima prazno stanje koje ne laže (vidi tabelu gore). |
