# P2b — Moja korpica

**Datum:** 2026-10-09
**Grana:** `moja-korpica-p2b` (iz `redizajn-opcija3`)
**Mjesto u cjelini:** druga polovina P2; prva je P2a — Moj nalog, spojena `45595c7`

## Šta se radi

Kupac koji je naručio korpicu može da joj izmijeni sadržaj — izbaci što ne jede, doda što
hoće — sve dok je u budžetu i prije roka za tu sedmicu. Ekran je `/moj-nalog/korpica`, stavka
koju je P2a već postavila u navigaciju kao zaključanu.

Uz to, dva preduslova bez kojih editor nema na čemu da stoji, i koja se plaćaju i izvan njega:
narudžba dobija sedmicu, a pakerska lista počinje da broji narudžbe umjesto šablona.

## Zašto

Korisnikov zahtjev od početka glasi: automatska dodjela proizvoda, ali da kupac kroz svoj
nalog može da zamijeni određene. P1 je isporučio automatsku dodjelu. Ovo je druga polovina
rečenice.

Ali tri stvari danas stoje na putu, i dvije od njih su pogrešne i bez editora.

**Narudžba ne zna za koju je sedmicu.** `DeliverySlot` je ponavljajući dan u sedmici
(`DayOfWeek`, `TimeFrom`) bez ijednog datuma. Na `Order` ne postoji nijedno polje koje kaže
koju sedmicu ta narudžba isporučuje. Posljedica nije samo da se „moja korpica za 42. sedmicu"
ne može pronaći — ne može se odgovoriti ni na pitanje koliko je korpica naručeno za ovu
sedmicu, što je prvo pitanje svakog ponedjeljka.

**Pakerska lista broji šablon, ne narudžbe.** `GetPakerskaListaHandler`
(`AdminKorpice.cs:168`) sabira `StavkeSadrzaja` za zadatu sedmicu i grupiše po artiklu. To su
redovi **šablona** — sadržaj jedne kutije. Ako je četrdeset kupaca naručilo povrćnu korpicu,
lista i dalje ispisuje količine za jednu. Oblik dokumenta je nabavni i ispravan; broj na njemu
nije. Operater koji po njemu naruči robu naručiće je za jednu korpicu.

**Personalizovana korpica nema gdje da živi.** `StavkaSadrzaja` pripada sedmičnom šablonu i
dijele ga svi kupci. Nema nijednog reda u bazi koji pripada korpici jednog kupca.

## Odluke korisnika

| Pitanje | Odluka |
|---|---|
| Iz čega kupac bira zamjenu | Iz cijele ponude u sezoni (`MozeUKorpicu` + `Mjeseci`), ne samo iz kategorija te korpice |
| Šta smije sa stavkom | Izbaci i dodaj, u okviru budžeta. Količinu ne mijenja |

Prva odluka je donesena uz izričito upozorenje na njenu cijenu — vidi rizik R1.

## Model podataka

### `Order` dobija sedmicu

```csharp
public int Godina { get; set; }
public int Sedmica { get; set; }   // ISO 8601, 1–53
```

ISO sedmica **datuma isporuke**, ne datuma narudžbe. Računa se pri kreiranju narudžbe iz
izabranog `DeliverySlot`-a, istim `SedmicaHelper`-om koji korpice već koriste — jedan izvor
istine za to šta je „sedmica", umjesto dva koja se raziđu.

**Koja je to sedmica tačno — i rupa koju to zatvara.** `GetTekucaSedmicaHandler`
(`KorpiceQueries.cs:206`) uvijek vraća **tekuću kalendarsku sedmicu** i uz nju zastavicu
`RokIstekao`. Ne prelazi na sljedeću. To znači da javna stranica `/korpice` poslije utorka do
kraja sedmice piše „rok istekao" umjesto da ponudi narednu — što je podnošljivo dok je korpica
samo izlog.

Ali korpica je `Product`, a checkout ne provjerava nikakav rok. Danas, poslije isteka roka,
korpica se **i dalje može staviti u korpu i platiti**. Niko to nije primijetio jer prije P2b
narudžba korpice nije ni bila vezana za sedmicu, pa nije imala šta da prekrši.

P2b to zatvara, jer mora: sedmica narudžbe je **prva sedmica čiji rok još nije prošao**.

```csharp
public static (int Godina, int Sedmica) SedmicaZaNarudzbu(
    DayOfWeek dan, string vrijemeOd, int cutoffSati, DateTime? sada = null)
```

Prije roka to je tekuća sedmica; poslije roka sljedeća. Granica godine se ne računa ručno —
uzima se ponedjeljak tekuće ISO sedmice, doda sedam dana, i iz tog datuma ponovo izvedu ISO
godina i sedmica, pa prelaz 52/53 → 1 izlazi tačan sam od sebe.

Ako za tu sedmicu nema objavljenog sadržaja, korpica se ne može naručiti (vidi dolje). To je
ispravan ishod: prodaja koju ne možemo isporučiti ne treba da se desi.

`GetTekucaSedmicaHandler` se u P2b **ne mijenja** — javna stranica i dalje govori o tekućoj
sedmici. Uskladiti izlog sa ovim pravilom je zaseban posao; ovdje je bitno da naplata ne
prolazi tamo gdje isporuka ne može.

Polja su obavezna, ne `int?`. Svaka narudžba se isporučuje u nekoj sedmici; nullable bi tu
istinu pretvorio u granu koju svaki upit mora da obrađuje. Migracija postojećim redovima
upisuje ISO sedmicu njihovog `CreatedAt`. Za narudžbe otprije P2b to je **približno** — ako je
neko naručio u nedjelju za četvrtak sljedeće sedmice, upisana sedmica je pogrešna. To se
prihvata, jer nijedna narudžba otprije P2b ne nosi sadržaj korpice koji bi o tu sedmicu visio;
polje je za njih samo metapodatak. Migracija to mora reći u komentaru, da sljedeća sesija ne
povjeruje starim redovima.

### Nova tabela: `StavkaKorpiceNarudzbe`

```csharp
/// Jedan artikal u korpici JEDNE narudžbe. Ne miješati sa StavkaSadrzaja —
/// ta pripada sedmičnom šablonu i dijele je svi kupci; ova pripada jednom kupcu.
public class StavkaKorpiceNarudzbe : Entity
{
    public Guid OrderItemId { get; set; }      // OrderItem čiji je Product Vrsta == Korpica
    public OrderItem? OrderItem { get; set; }

    public Guid ProductId { get; set; }
    public string ProductName { get; set; }    // snapshot
    public Unit Unit { get; set; }             // snapshot
    public decimal Kolicina { get; set; }
    public decimal JedinicnaCijena { get; set; } // snapshot

    /// True kad je kupac sam dodao artikal; false kad je stigao iz šablona.
    /// Pakerima je to razlika između „ovo ide u svaku kutiju" i „ovo je nečiji izbor".
    public bool DodaoKupac { get; set; }
}
```

Veza ide na `OrderItem`, ne na `Order`. Jedna narudžba može nositi dvije različite korpice, i
svaka mora imati svoj sadržaj.

Snapshot imena, jedinice i cijene prati disciplinu koju `OrderItem` već ima: kasnija izmjena
artikla ne smije pomjeriti ono što je kupac kupio.

**Nema novih entiteta osim ovog.** `SedmicniSadrzaj.CiljnaVrijednost` već postoji i njen
komentar iz P1 doslovno kaže da je to budžet kupčevog editora u P2 — ovo je trenutak za koji
je upisan.

## Kako sadržaj dospije na narudžbu

`CreateOrderHandler` za svaku stavku čiji `Product.Vrsta == Korpica`:

1. nađe `SedmicniSadrzaj` za (`Godina`, `Sedmica`, `KorpicaId`) sa statusom `Objavljen`
2. kopira njegove `Stavke` u `StavkaKorpiceNarudzbe` redove, sa `DodaoKupac = false`

Ako objavljenog sadržaja za tu sedmicu nema, **korpica se ne može naručiti**:
`DomainException("…", "korpica_bez_sadrzaja")`. To je nov način da checkout padne i mora biti
naveden, ali alternativa je narudžba korpice koja ne sadrži ništa — a kupac je plati.

`OrderItem.Quantity` za korpicu može biti veći od 1 (dvije kutije). Sadržaj se čuva **jednom**
po `OrderItem`-u, a pakerska lista ga množi sa `Quantity`. Dvije kutije iste korpice na istoj
narudžbi su iste kutije; kupac koji hoće dvije različite naručuje ih kao dvije stavke.

## Editor

### Pravila

| Pravilo | Izvor |
|---|---|
| Vrijednost sadržaja u **95–100%** `SedmicniSadrzaj.CiljnaVrijednost` | odluka P2b |
| Najmanje `MinimalanBrojArtikala[Velicina]` artikala | `KorpiceOpcije`, postoji |
| Samo prije roka za tu sedmicu | `UcitajRokAsync`, postoji |
| Samo dok narudžba nije u `Packing` ili dalje | `Order.Status` |
| Dodati artikal: `MozeUKorpicu`, u sezoni, dostupan, nije obrisan | `Product` |

**Cijena korpice se ne mijenja.** Budžet i postoji zato da bi cijena ostala fiksna: kupac
mijenja šta dobija, ne koliko plaća. Donja granica od 95% ga sprečava da se sam zakine,
gornja od 100% da uzme više nego što je platio.

**Količina dodatog artikla je 1 u jedinici tog artikla** — 1 kg, 1 veza, 1 kom, 1 l. Generator
računa količine iz budžeta po stavci; za kupčevo dodavanje to bi značilo da mu se količina
mijenja pod rukom dok bira. Fiksna jedinica je predvidiva i pakerima jednoznačna, a budžet
odbija ono što je preskupo. Ako se u upotrebi pokaže nezgrapno (1 kg nečega je mnogo), rješenje
je polje `PorcijaUKorpici` na `Product` — zaseban posao, ne ovaj.

### Endpointi

```
GET /account/korpice
    → korpice koje kupac trenutno može mijenjati: po jedna po OrderItem-u
      koji zadovoljava SVE uslove iz tabele pravila (prije roka, narudžba
      nije u Packing ili dalje, kupčeva je)

GET /account/korpice/{orderItemId}
    → sadržaj, stanje budžeta (trenutno / cilj / procenat), rok,
      i ponuda artikala koji se mogu dodati

PUT /account/korpice/{orderItemId}
    body { productIds: [guid, …] }
    → zamjenjuje CIJELU listu
```

`PUT` koji zamjenjuje cijelu listu, a ne zasebni `POST`/`DELETE` po stavci, bira se zato što
provjera budžeta mora biti atomična. Sa zasebnim radnjama svaki međukorak mora biti validan,
pa kupac koji hoće da zamijeni skup artikal za skup ne može — nema redoslijeda u kojem je i
međustanje u 95–100%.

**Tijelo nosi samo identifikatore, nijednu količinu ni cijenu.** Količinu određuje server:
artiklu koji je u šablonu te sedmice daje količinu iz šablona, svemu ostalom 1 u jedinici tog
artikla. Kupac ionako ne mijenja količinu, pa bi je slati značilo primiti broj koji se ne
koristi — ili, gore, koji se koristi i time otvara put da klijent sam sebi upiše deset kila
po cijeni jednog. Isto važi za cijenu: snapshot se uzima sa `Product.Price` na serveru.

## Pakerska lista

`GetPakerskaListaHandler` prestaje da čita `StavkeSadrzaja`. Umjesto toga sabira
`StavkaKorpiceNarudzbe × OrderItem.Quantity` preko svih narudžbi u toj sedmici čiji status
nije `Cancelled`.

**Posljedica koju operater mora znati unaprijed:** dok za sedmicu nema nijedne narudžbe, lista
je prazna. Danas u istoj situaciji pokazuje sadržaj šablona. To je ispravna promjena — nabavka
po listi koja broji nula narudžbi kao jednu je upravo greška koju ovo zatvara — ali izgleda
kao da je nešto pokvareno ako se ne očekuje. Lista zato dobija zaglavlje sa brojem narudžbi po
korpici, da prazno stanje čita kao „nula narudžbi", a ne kao kvar.

Red zadržava `ArtikalObrisan` zastavicu i `IgnoreQueryFilters()` iz P1, iz istog razloga:
soft-obrisan artikal mora ostati vidljiv sa stvarnom količinom, obilježen.

Obične stavke narudžbe (sve što nije korpica) **ostaju izvan** pakerske liste, kao i danas.
Lista je dokument za pakovanje korpica; proširiti je na cio katalog je zaseban posao.

## Frontend

`/moj-nalog/korpica` — stavka koju `nalog-nav.tsx` već nosi sa oznakom „uskoro". P2b je
otključava; ljuska, straža i navigacija iz P2a se ne diraju.

Ekran: jedna kartica po korpici koju kupac može mijenjati. U kartici — spisak artikala sa
dugmetom za izbacivanje, traka budžeta (iskorišteno / cilj, sa procentom), brojač artikala
naspram minimuma, odbrojavanje do roka, i birač za dodavanje.

Birač prikazuje **kategorije te korpice prve**, pa ostatak ponude u sezoni. Ne zabranjuje
ništa — odluka korisnika je cijela ponuda — ali čini očekivani izbor najbližim.

Dugme „Sačuvaj" je onemogućeno dok budžet ili broj artikala nisu u dozvoljenom opsegu, sa
tekstom koji kaže šta nedostaje („još 1.20 KM do 95%", „fali 1 artikal"). Serverska provjera
ostaje mjerodavna i njenu grešku ekran prikazuje; klijentska postoji da kupac ne šalje zahtjev
koji unaprijed pada.

## Testiranje

Backend, `EPiljara.Application.Tests` (xUnit + SQLite in-memory, postojeći `TestDb`,
`FakeCurrentUser`):

- snapshot pri narudžbi: korpica dobije redove šablona, sa `DodaoKupac = false`
- korpica bez objavljenog sadržaja za tu sedmicu → `korpica_bez_sadrzaja`
- granice budžeta: 94.9% odbijeno, 95.0% prihvaćeno, 100.0% prihvaćeno, 100.1% odbijeno
- ispod minimalnog broja artikala → odbijeno
- poslije roka → odbijeno
- narudžba u `Packing` → odbijeno
- tuđa narudžba → 403
- dodat artikal van sezone, bez `MozeUKorpicu`, ili obrisan → odbijeno
- količinu određuje server: artikal iz šablona dobije šablonsku količinu, dodat artikal 1
- isti `productId` dvaput u tijelu → odbijeno, ne udvostručena količina
- pakerska lista: dvije narudžbe iste korpice daju dvostruku količinu
- pakerska lista: `OrderItem.Quantity = 2` množi količine
- pakerska lista: otkazana narudžba se ne broji
- izmjena kupca se vidi na pakerskoj listi

Frontend nema test setup. Provjera je `npx tsc --noEmit`, `npm run lint` i **obavezno
`npm run build`** sa ugašenim dev serverom, plus prolazak kroz tok uz pokrenut stek.

## Rizici

| Rizik | Odgovor |
|---|---|
| **R1.** Povrćna korpica može završiti puna suhomesnatog — naziv, pakovanje i hladni lanac se raziđu | Prihvaćen rizik, izričita odluka korisnika. Birač stavlja kategorije te korpice prve. Pakerska lista po narudžbama sada pokazuje stvaran sadržaj svake kutije, pa se to bar **vidi** pri pakovanju umjesto da iznenadi na vratima |
| **R2.** Pakerska lista mijenja značenje; operater zatekne prazno | Zaglavlje sa brojem narudžbi po korpici; prazno čita kao „nula narudžbi" |
| **R3.** `Order.Godina/Sedmica` za stare narudžbe je približno | Dokumentovano u migraciji i u `CLAUDE.md`. Nijedna stara narudžba nema sadržaj korpice |
| **R4.** Količina 1 za dodat artikal je nezgrapna za skupe artikle po kg | Budžet odbija; ako se pokaže kao stvaran problem, `PorcijaUKorpici` je zaseban posao |
| **R5.** Rok se računa iz `DeliverySlot.TimeFrom` koji nema vremensku zonu — zatvara se dva sata kasnije od nominalnog | **Postojeća, prihvaćena rupa iz P1** (`CLAUDE.md`). P2b je nasljeđuje i ne pogoršava. Zatvara se kad se uvede vremenska zona, a ne usput |
| **R6.** `/korpice` i dalje piše „rok istekao" za ostatak sedmice, dok checkout prodaje za narednu | Izlog i naplata govore o različitim sedmicama. Naplata je sada tačna, izlog zaostaje. Uskladiti `GetTekucaSedmicaHandler` je zaseban posao; prodaja koja se ne može isporučiti je ovim već zaustavljena |

## Šta P2b NE pokriva

| Izostavljeno | Gdje ide |
|---|---|
| Pretplate, pauziranje, promjena paketa | P3 |
| Izmjena količine postojeće stavke | izričita odluka korisnika protiv |
| Pakerska lista za obične stavke narudžbe | zaseban posao |
| `PorcijaUKorpici` po artiklu | zaseban posao, vidi R4 |
| Birač adrese na mapi | ostatak iz P2a, prva stavka nastavka |
| Vremenska zona na `DeliverySlot` | zaseban posao, vidi R5 |
| Usklađivanje `/korpice` izloga sa sedmicom naručivanja | zaseban posao, vidi R6 |

## Dekompozicija (P1…P7)

| | Potprojekat | Stanje |
|---|---|---|
| P1 | Korpice i sedmični ciklus | **gotovo**, spojeno `6906c26` |
| P2a | Moj nalog | **gotovo**, spojeno `45595c7` |
| **P2b** | Sedmica na narudžbi, pakerska lista po narudžbama, editor sadržaja | **ovaj spec** |
| P3 | Pretplate | čeka |
| P4 | Marketinška početna | čeka |
| P5 | Analize, Recepti, O nama, Postani partner | čeka |
| P6 | Poeni i referral, promo kodovi, ocjena isporuke | čeka |
| P7 | Višejezičnost | čeka |
