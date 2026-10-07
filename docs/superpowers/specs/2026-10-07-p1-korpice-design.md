# P1 — Korpice i sedmični ciklus

**Datum:** 2026-10-07
**Grana:** `korpice-p1` (iz `redizajn-opcija3`, ne iz `main`-a — P1 dira ekrane koje taj redizajn upravo prepravlja)
**Uzor:** farmit.rs, analiza od 2026-10-06
**Mjesto u cjelini:** prvi od sedam potprojekata (P1…P7), vidi „Dekompozicija" na dnu

## Šta se radi

E piljara dobija **korpicu** — kuratorsko pakovanje domaćih proizvoda sa **fiksnom cijenom**,
čiji se sadržaj sastavlja iznova svake sedmice iz onoga što je tada dostupno.

Pet vrsta × tri veličine:

| Vrsta | Šta ide unutra |
|---|---|
| Povrće | sezonsko povrće sa Manjače |
| Voće | sezonsko voće |
| Mliječna | sir, kajmak, mlijeko, pavlaka |
| Suhomesnata | pečenica, slanina, kobasica, suvo meso |
| Domaćinska | miješana — komad pečenice, slanina, flaša mlijeka, komad sira |

U P1 ulazi: korpica kao prodajni artikal, sedmični sadržaj, generator prijedloga, admin ekran
za sedmicu, javni prikaz sadržaja, rok naručivanja sa odbrojavanjem i pakerska lista.

## Zašto

E piljara danas prodaje artikal po artikal. Kupac mora da zna šta hoće, da sastavi korpu i da
to ponovi svake sedmice iznova. Konverzija zavisi od njegove volje da bira.

Korpica mijenja jedinicu prodaje: kupuje se **povjerenje u izbor**, ne lista artikala. Odluka je
jedna („mala ili srednja"), ponavlja se sama, a sadržaj je iznenađenje koje je ujedno i sadržaj
marketinga („evo šta je ove sedmice u korpici").

Operativno je važnije: fiksna cijena, fiksni sedmični ritam i rok naručivanja daju **predvidivu
nabavku**. Proizvođač sa Manjače unaprijed zna koliko ide, umjesto da pogađa. To je jedino što
omogućava ugovorenu proizvodnju, a ona je razlog zašto ovakav model uopšte postoji.

## Šta se NE radi u P1

| Odloženo | Gdje ide |
|---|---|
| Pretplate, ponavljanje isporuke, pauziranje, promjena paketa | P3 |
| „Moj nalog" i kupčeva izmjena sadržaja | P2 |
| Analize, recepti, marketinška početna, Postani partner | P4, P5 |
| Poeni, referral, promo kodovi, ocjena isporuke | P6 |
| Višejezičnost | P7 |
| Kartično plaćanje | nikad — plaćanje ostaje **pouzećem** |
| Skidanje zaliha po komponentama korpice | svjesno izostavljeno, vidi „Zalihe" |

**Ne dira se:** `Order`, `OrderItem`, checkout, `DeliveryZone`, `DeliverySlot`, rute, vozačka
aplikacija, autentifikacija, naručivanje bez naloga. Korpica se u korpu i narudžbu ponaša kao
svaki drugi proizvod — to je glavna poenta izabranog modela.

## Odluke korisnika

| Pitanje | Odluka |
|---|---|
| Naplata | pouzećem; bez platne integracije |
| Korpica u domenu | `Product` sa `Vrsta = Korpica`, sadržaj u zasebnim tabelama |
| Ko sastavlja sadržaj | sistem generiše **prijedlog**, admin ga doradi i objavi |
| Izmjena od strane kupca | dozvoljena, ali tek u P2 |
| Pravilo izmjene | budžet 95–100% vrijednosti sadržaja; **cijena korpice se ne mijenja nikad** |
| Vrste korpica | Povrće, Voće, Mliječna, Suhomesnata, Domaćinska |
| Veličine | Mala, Srednja, Velika |
| Periodi pretplate (P3) | puna matrica kao kod farmita, ali se **popust zarađuje unazad** |

Posljednja odluka je jedino mjesto gdje svjesno odstupamo od uzora. Farmit naplati 6 mjeseci
unaprijed karticom, pa smije dati popust odmah. Pouzećem je duži period samo obećanje — zato
prvih pet isporuka ide po punoj cijeni, a niža cijena počinje od šeste. Ko odustane ranije,
nije koštao ništa.

## Model podataka

### Nove vrijednosti enuma (`Domain/Enums/Enums.cs`)

```csharp
public enum VrstaProizvoda  { Obican, Korpica }
public enum TipKorpice      { Povrce, Voce, Mlijecna, Suhomesnata, Domacinska }
public enum VelicinaKorpice { Mala, Srednja, Velika }
public enum StatusSadrzaja  { Nacrt, Objavljen, Zakljucan }
```

### `Product` — nova polja

| Polje | Tip | Napomena |
|---|---|---|
| `Vrsta` | `VrstaProizvoda` | default `Obican`; postojeći redovi ostaju nepromijenjeni |
| `TipKorpice` | `TipKorpice?` | obavezno kad je `Vrsta == Korpica`, inače `null` |
| `Velicina` | `VelicinaKorpice?` | isto pravilo |
| `MozeUKorpicu` | `bool` | za obične artikle: smije li ući u sadržaj; default `false` |
| `PrioritetUKorpici` | `int` | `0` = osnovni (ide skoro uvijek), veći broj = dopuna; default `0` |
| `Mjeseci` | `int[]` | mjeseci u kojima je dostupan; **prazno = cijele godine** |

`Mjeseci` je `int[]` mapiran na Postgres `integer[]` — EF Core i Npgsql to podržavaju izvorno,
nema potrebe za tabelom veze ni za bitmaskom.

Mliječno i suhomesnato ostavljaju `Mjeseci` prazno jer nemaju sezonu. Povrće i voće ga koriste.
Jedan kod, različito ponašanje po kategoriji, bez `if`-a po tipu korpice.

**Invarijanta:** `Vrsta == Korpica` ⇒ `TipKorpice != null && Velicina != null && MozeUKorpicu == false`.
Korpica ne smije ući u drugu korpicu. Provjerava se u `AbstractValidator`-u i u domenskoj metodi.

### `KorpicaKategorija` — iz čega se koja korpica puni

| Polje | Tip |
|---|---|
| `TipKorpice` | `TipKorpice` |
| `CategoryId` | `Guid` → `Categories` |

Tabela veze, bez zasebnog `Id` (složeni ključ). Sir pripada kategoriji „Mliječni proizvodi", koja
je vezana i za `Mlijecna` i za `Domacinska` — bez duplog unosa proizvoda.

### `SedmicniSadrzaj`

| Polje | Tip | Napomena |
|---|---|---|
| `KorpicaId` | `Guid` → `Products` | mora biti `Vrsta == Korpica` |
| `Godina` | `int` | |
| `Sedmica` | `int` | ISO 8601 broj sedmice (1–53) |
| `Status` | `StatusSadrzaja` | `Nacrt` → `Objavljen` → `Zakljucan` |
| `CiljnaVrijednost` | `decimal` | **zamrznuta** maloprodajna vrijednost sadržaja pri objavi |
| `ObjavljenU` | `DateTime?` | UTC |
| `ZakljucanU` | `DateTime?` | UTC |

Jedinstveni indeks `(KorpicaId, Godina, Sedmica)`. Jedna korpica ima najviše jedan sadržaj po
sedmici.

Status je **state machine**, isto kao `Order.Status`: dozvoljeni prelazi su `Nacrt → Objavljen`
i `Objavljen → Zakljucan`. Nazad se ne ide; nedozvoljen prelaz baca `ConflictException`
(`nedozvoljen_prelaz_sadrzaja`). Objavljen sadržaj se više ne generiše iznova — kupci su ga vidjeli.

### `StavkaSadrzaja`

| Polje | Tip | Napomena |
|---|---|---|
| `SedmicniSadrzajId` | `Guid` | kaskadno brisanje |
| `ProductId` | `Guid` → `Products` | `Restrict` |
| `Kolicina` | `decimal` | u `Product.Unit` |
| `JedinicnaCijena` | `decimal` | **snapshot** `Product.Price` |

`JedinicnaCijena` se upisuje kad stavka nastane (generisanjem ili ručnim dodavanjem) i **ponovo
se snima pri objavi**, da objavljeni sadržaj bude dosljedan jednom trenutku. Razlog je isti kao
kod `OrderItem`: kad admin kasnije promijeni cijenu artikla, vrijednost objavljenog sadržaja se
ne smije pomjeriti. Budžet editora u P2 računa se po ovom snapshotu, ne po živoj cijeni.

### Dvije različite „ciljne vrijednosti" — ne miješati

| Pojam | Kad postoji | Čemu služi |
|---|---|---|
| **cilj generisanja** = `Korpica.Price × KoeficijentVrijednosti` | samo dok generator radi | koliko sadržaja da sastavi; nigdje se ne čuva |
| **`SedmicniSadrzaj.CiljnaVrijednost`** = `Σ(Kolicina × JedinicnaCijena)` | upisuje se **pri objavi**, nepromjenjiva | budžet kupčevog editora u P2 |

Dok je sadržaj `Nacrt`, `CiljnaVrijednost` je `0`. Admin na ekranu vidi tekući zbir i cilj
generisanja kao orijentir, ali se ništa ne zamrzava do objave.

## Generisanje sadržaja

`GenerisiSedmicniSadrzajCommand(int Godina, int Sedmica, Guid? KorpicaId)` —
bez `KorpicaId` generiše za sve korpice koje tu sedmicu još nemaju sadržaj.

Algoritam, po korpici:

1. **Bazen:** proizvodi gdje je `MozeUKorpicu`, `IsAvailable`, `!IsDeleted`, `Stock > 0`,
   `CategoryId` je vezan za `TipKorpice` te korpice, i mjesec isporuke je u `Mjeseci`
   (ili je `Mjeseci` prazno).
2. **Cilj generisanja:** `Korpica.Price × KoeficijentVrijednosti` (vidi tabelu iznad — ovo se
   ne upisuje u `CiljnaVrijednost`, koja nastaje tek pri objavi).
   `KoeficijentVrijednosti = 1.15` — sadržaj vrijedi 15% više od cijene korpice. To je popust
   koji kupac dobija zato što ne bira sam, i razlog zašto korpica ima smisla kao proizvod.
   Koeficijent je konfiguracija (`appsettings`), ne konstanta u kodu.
3. **Osnova:** uzmi artikle sa `PrioritetUKorpici == 0`, nasumično izmiješane unutar prioriteta,
   dok se ne ispuni `MinimalanBrojArtikala` za tu veličinu (Mala 5, Srednja 7, Velika 9).
4. **Dopuna:** dodavaj po rastućem prioritetu dok vrijednost ne dosegne cilj, uz toleranciju ±5%.
5. **Količine:** `Kolicina = zaokruzi(ciljnaVrijednostStavke / Product.Price, korak)` gdje je
   korak `0.1` za `Kg`/`L` i `1` za `Kom`. Količina nikad ne prelazi `Stock`.
6. Rezultat je **`Nacrt`**. Admin ga doradi i objavi.

Generator je **prijedlog, ne odluka**. Admin nikad ne dobija nešto što ne može promijeniti —
to je razlika između alata i automata, i razlog zašto se sadržaj uopšte može generisati bez
rizika.

Ako bazen ne može ispuniti ni minimalan broj artikala, komanda ne baca grešku nego vraća
`Nacrt` sa onim što ima i upozorenjem u `meta` — admin vidi problem na ekranu, umjesto da
generisanje tiho padne.

## Sedmični ciklus i rokovi

Ciklus je vezan za `DeliverySlot`, ne za zakucane dane:

```
Objava sadržaja  →  Rok naručivanja  →  Zaključavanje  →  Isporuka
     (admin)         (konfiguracija)      (automatski)      (rute)
```

- **Rok naručivanja** = `datum isporuke − RokSati` (konfiguracija, default 48h).
- Po isteku roka pozadinski posao prebacuje sve `Objavljen` sadržaje te sedmice u `Zakljucan`.
- Od tog trenutka pakerska lista je zamrznuta, a editor sadržaja (P2) se gasi.

Na sajtu se rok prikazuje kao **odbrojavanje**, isto kao kod farmita:
`Poruči do <dan> u <sat> i korpica stiže <dan isporuke>`.

Odbrojavanje je istinito — vezano je za stvarni operativni rok, ne za izmišljeni tajmer. To je
jedini razlog zašto takav element uopšte smije da stoji na stranici.

## API

Sve pod postojećom kovertom `{ data, error, meta }`, preko `Mediator.Send`.

### Javno

| Metoda | Ruta | Vraća |
|---|---|---|
| `GET` | `/korpice` | sve aktivne korpice; filter `?tip=povrce` |
| `GET` | `/korpice/{slug}` | jedna korpica |
| `GET` | `/korpice/{slug}/sadrzaj` | objavljeni sadržaj za tekuću sedmicu |
| `GET` | `/korpice/sedmica` | rok, datum isporuke, broj sedmice — za odbrojavanje |

`/korpice/{slug}/sadrzaj` vraća `404 sadrzaj_nije_objavljen` dok je sadržaj `Nacrt`. Nacrt ne
smije procuriti na sajt.

### Admin (`[Authorize(Roles = "Admin")]`)

| Metoda | Ruta | Radnja |
|---|---|---|
| `GET` | `/admin/korpice/sadrzaj?godina=&sedmica=` | sve korpice i njihov status za tu sedmicu |
| `POST` | `/admin/korpice/sadrzaj/generisi` | generiše nacrte |
| `PUT` | `/admin/korpice/sadrzaj/{id}` | izmjena stavki nacrta |
| `POST` | `/admin/korpice/sadrzaj/{id}/objavi` | `Nacrt → Objavljen` |
| `GET` | `/admin/korpice/pakerska-lista?godina=&sedmica=` | zbir po artiklu |

Novi DTO-i idu u `Application/DTOs/KorpiceDtos.cs`, use-case-ovi u
`Application/Features/Korpice/`. Frontend tipovi u `types/domain.ts`, zahtjevi u
`lib/api/contracts.ts`, poziv isključivo kroz `lib/api/endpoints.ts`.

**Mock se ne dopunjava.** Mock je već nepotpun i jaz raste sa svakom funkcijom; korpice se
razvijaju protiv pravog backenda. To je nastavak odluke već zapisane u `CLAUDE.md`, ne nova.

## Frontend

### Kupčevi ekrani

**`/korpice`** — izbor korpice. Prepisuje farmitov raspored:
- tabovi po vrsti: `povrće · voće · mliječna · suhomesnata · domaćinska`
- tri kartice veličina u redu, srednja označena **„Najčešći izbor"** i jedinog punog dugmeta
- svaka kartica: slika, naziv, za koliko članova, **četiri reda sa ikonicom** (broj isporuka,
  okvirna težina, broj artikala, „sadržaj se mijenja svake sedmice"), cijena, dugme,
  i link **„Pogledaj sadržaj korpice"**
- traka sa rokom na vrhu, ljepljiva traka sa korpom na dnu

**Modal „Sadržaj korpice"** — po stavci: slika, naziv, količina, **selo i ime proizvođača**.
`Producer` već nosi `Village` i `Name`, pa je ovo spajanje postojećih podataka, ne nova tabela.
Ovo je najjači element cijelog farmitovog sajta i najjeftiniji za nas.

Ispod liste: *„Slike su informativnog karaktera, a sadržaj se mijenja u zavisnosti od sezone."*

**Detalj korpice `/korpice/{slug}`** — isto plus opis, proizvođači te sedmice, i dugme u korpu.

Korpica u korpi i na checkoutu izgleda kao svaki drugi artikal. **Nijedan ekran checkouta se ne
mijenja.**

### Admin

**`/admin/korpice`** — ekran sedmice. Birač sedmice gore, ispod red po korpici: status
(`Nacrt`/`Objavljen`/`Zaključan`), broj artikala, vrijednost naspram cilja, dugmad
`Generiši` · `Izmijeni` · `Objavi`.

**Izmjena nacrta** — tabela stavki sa dodavanjem artikla iz bazena, promjenom količine i
brisanjem. Ukupna vrijednost i odstupanje od cilja računaju se **uživo**, da admin vidi
posljedicu prije nego sačuva.

**Pakerska lista** — zbir svih korpica te sedmice po artiklu: `artikal · ukupna količina ·
proizvođač`. Štampa se i nosi u magacin.

## Zalihe — svjesno pojednostavljenje

`Product.Stock` korpice je **broj korpica raspoloživih te sedmice** i admin ga postavlja.
Postojeća provjera zaliha u narudžbi radi nad njim bez izmjene.

Zalihe **komponenti se po narudžbi ne skidaju.** Razlog: od P2 sastav varira po kupcu, pa je
tačan obračun moguć tek nakon zaključavanja. Izvor istine za nabavku je **pakerska lista**, ne
`Stock` pojedinačnog artikla.

Posljedica koju treba znati: ako se isti sir prodaje i zasebno u špajzu i ulazi u korpicu,
njegov `Stock` odražava samo pojedinačnu prodaju. Admin mora da vodi računa o preklapanju.
Tačno vezivanje zaliha je zaseban posao i ne ulazi u P1.

## Migracije i seed

Jedna migracija `Korpice`:
- šest kolona na `Products` (sa `DEFAULT`-ima, bez ručnog popunjavanja)
- tabele `KorpicaKategorije`, `SedmicniSadrzaji`, `StavkeSadrzaja`
- jedinstveni indeks `(KorpicaId, Godina, Sedmica)`

Seeder (idempotentan, kao postojeći) dodaje pet korpica × tri veličine = **15 artikala**, veze
`TipKorpice → Category`, i jedan objavljen sadržaj za tekuću sedmicu da ekrani imaju šta da
prikažu.

Postojeći proizvodi dobijaju `MozeUKorpicu = true` i `Mjeseci = []` kroz seed, da generator ima
bazen od prvog pokretanja.

## Testiranje

Backend, u `EPiljara.Application.Tests` (xUnit + SQLite in-memory, postojeća infrastruktura
`TestDb`/`FakeCurrentUser`):

- `GeneratorSadrzajaTests` — poštuje sezonu; poštuje `Stock`; pogađa cilj u toleranciji;
  vraća nacrt sa upozorenjem kad je bazen premali; ne stavlja korpicu u korpicu
- `SadrzajStatusTests` — dozvoljeni i nedozvoljeni prelazi
- `SadrzajQueryTests` — nacrt nije vidljiv javnom endpointu
- `PakerskaListaTests` — zbir po artiklu preko više korpica

Frontend nema test setup. Provjera je `npx tsc --noEmit`, `npm run lint` i **obavezno
`npm run build`** — `tsc` i `eslint` ne vide greške Next-ovog prerender prolaza. Zeleni `tsc`
i `lint` nisu dokaz da se frontend može isporučiti.

## Rizici

| Rizik | Odgovor |
|---|---|
| Generator daje loš sadržaj | Rezultat je uvijek nacrt; admin je posljednja instanca |
| Preklapanje zaliha špajz/korpica | Priznato gore; rješava se zasebno |
| Fiksna cijena, promjenjiva nabavna | `KoeficijentVrijednosti` je podesiv; prati se po sedmici |
| Rok promakne, sadržaj ostane otvoren | Zaključavanje je pozadinski posao, ne ručna radnja |
| Korpica zavisi od redizajna u `redizajn-opcija3` | Grana je izvedena iz njega, ne iz `main`-a |

## Dekompozicija (P1…P7)

| | Potprojekat | Stanje |
|---|---|---|
| **P1** | Korpice i sedmični ciklus | **ovaj spec** |
| P2 | Moj nalog (7 sekcija) + editor sadržaja sa budžetom | čeka |
| P3 | Pretplate: ponavljanje, pauza, promjena paketa, periodi | čeka |
| P4 | Marketinška početna: hero, odbrojavanje, utisci, FAQ, proizvođači | čeka |
| P5 | Analize, Recepti, O nama, Postani partner | čeka |
| P6 | Poeni i referral, promo kodovi, ocjena isporuke | čeka |
| P7 | Višejezičnost | čeka |

Svaki potprojekat dobija svoj spec, svoj plan i svoju granu.
