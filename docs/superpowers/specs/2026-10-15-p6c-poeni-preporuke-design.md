# P6c — Poeni i preporuke

**Datum:** 2026-10-15
**Grana:** `p6c-poeni` (iz `p6b-promo`, HEAD `5b66263` — P6a i P6b još nisu spojene u `main`)
**Mjesto u cjelini:** P6a ocjena (gotova) → P6b promo kodovi (gotova) → **P6c poeni i preporuke** →
P7 višejezičnost.

## Šta se radi

Kupac sa nalogom skuplja poene za svaku **isporučenu** narudžbu i troši ih kao popust na checkoutu.
Svaki kupac ima lični kod za preporuku; prijatelj koji se registruje preko linka i primi prvu
isporuku donosi poene i sebi i onome ko ga je preporučio. Stanje, istorija i link su na novoj
stranici „Moj nalog → Poeni".

## Zašto

Zadržavanje kupaca (poeni) i rast preko postojećih kupaca (preporuke) — model sa farmit.rs (sekcija
`points` u nalogu). Popust od poena koristi isti put kroz narudžbu koji je P6b uveo za promo kod.

## Šta se NE radi u P6c

| Odloženo | Zašto |
|---|---|
| Poeni za goste | Gost nema nalog na koji bi se poeni upisali. |
| Istek poena | Nema roka; dodaje se kasnije kao nova vrsta stavke bez promjene modela. |
| Admin ekran za ručnu korekciju poena | Nema potrebe dok nema žalbi; stavka `Korekcija` postoji u enumu da se doda bez migracije. |
| Preporuka preko prijave Google/Facebook nalogom | Socijalna prijava nema korak registracije gdje bi se kod upisao. |
| Mejl „Dobio si poene" | Kupac ih vidi na stranici Poeni. |
| Poeni za ocjenu isporuke | Podsticao bi ocjene radi poena, ne radi iskrenosti. |

## Odluke (nisu bile postavljene)

| # | Odluka | Cijena ako je pogrešna |
|---|---|---|
| O1 | **Zarada:** 1 poen po cijeloj KM plaćene robe (`Subtotal − Popust − PoeniPopust`, zaokruženo naniže); dostava ne donosi poene | Kupac sa 9,90 KM robe dobije 9 poena. |
| O2 | **Vrijednost:** 1 poen = 0,05 KM (20 poena = 1 KM) — povrat od 5 % | Promjena kursa je jedna konstanta u opcijama; već zarađeni poeni mijenjaju vrijednost s njom. |
| O3 | Poeni se upisuju tek kad narudžba pređe u **`Delivered`** (isti trenutak kao brojač pretplate), na oba mjesta gdje se to dešava (admin i vozač) — jedan pomoćnik | Kupac čeka isporuku da vidi poene. |
| O4 | **Trošenje:** checkbox „Iskoristi poene" na checkoutu (samo prijavljen); troši se **najveći cijeli broj KM** koji pokrivaju poeni, ali ne više od robe poslije promo popusta | Kupac ne bira koliko poena troši — ili sve što se može, ili ništa. |
| O5 | Promo kod se primjenjuje **prije** poena; poeni pokrivaju samo robu, nikad dostavu | Uz veliki promo popust kupac možda ne može potrošiti sve poene na toj narudžbi; ostatak čeka sljedeću. |
| O6 | **Otkazivanje** narudžbe vraća potrošene poene (stavka `Povrat`); zarađeni poeni se ne vraćaju jer se isporučena narudžba ne može otkazati | Nema. |
| O7 | Stanje = **zbir stavki** (`PoeniStavka`), bez kolone stanja na korisniku | Upit nad stavkama pri svakom prikazu; kod ove veličine nebitno. Nema razilaženja keša i istorije. |
| O8 | Dvije istovremene narudžbe istog kupca mogu potrošiti iste poene (bez zaključavanja); stanje može otići u minus, a minus blokira trošenje dok se ne vrati iznad nule | Gubitak najviše vrijednosti jedne narudžbe; kupac sa dva taba istovremeno. |
| O9 | **Preporuka:** lični kod od 8 znakova (`A–Z` bez `O`/`I`, `2–9`), pravi se **lijeno** pri prvom otvaranju stranice Poeni; link `/register?preporuka=KOD` | Postojeći kupci nemaju kod dok ne otvore stranicu — niko ga ne može upisati prije toga, pa nema štete. |
| O10 | Registracija prima opcioni `preporukaKod`; nepoznat kod se **tiho ignoriše** (registracija ne pada) | Kupac koji pogriješi kod ne sazna da nije upisan — ali izgubljeni poeni su manja šteta od odbijene registracije. |
| O11 | **Nagrada za preporuku:** kad preporučeni primi **prvu isporuku**, i on i onaj ko ga je preporučio dobiju po **100 poena** (5 KM); jednom po preporučenom | Lažni nalozi traže stvarnu isporučenu narudžbu, pa je zloupotreba skupa. |
| O12 | Poeni i nagrada za preporuku se dodjeljuju i za **pretplatne** narudžbe | Pretplatnik dobija poene i na već sniženu cijenu — namjerno, pretplata je najvredniji kupac. |
| O13 | Konstante (`PoenaPoKM = 1`, `VrijednostPoenaKM = 0.05`, `NagradaPreporuka = 100`) su u `PoeniOpcije` (sekcija `Poeni`, registrovana kao `PretplateOpcije`; vrijednosti su podrazumijevane u kodu, `appsettings.json` se ne dira) | Nema. |
| O14 | Checkout prikazuje popust od poena po istoj formuli kao server (`floor(min(stanje × vrijednost, roba − promo))`), iz `stanje` i `vrijednostPoenaKM` koje vraća `GET /account/poeni`; server je mjerodavan | Ako se formula promijeni samo na serveru, prikaz se razlikuje od narudžbe dok se klijent ne ažurira. |

## Model podataka

```csharp
public enum VrstaPoena { Isporuka, Iskoristeno, Povrat, PreporukaPreporucio, PreporukaNovi, Korekcija }

public class PoeniStavka : Entity
{
    public Guid UserId { get; set; }
    public int Iznos { get; set; }            // + zarada/povrat/nagrada, − trošenje
    public VrstaPoena Vrsta { get; set; }
    public Guid? OrderId { get; set; }        // narudžba koja je izazvala stavku
    public Guid? PreporuceniId { get; set; }  // za PreporukaPreporucio — koga je preporučio
    public DateTime Kreirana { get; set; } = DateTime.UtcNow;
}
```

`User` dobija `string? PreporukaKod` (jedinstven kad postoji, max 8) i `Guid? PreporucioId` (FK na
`User`, `SetNull`). `Order` dobija `int IskoristeniPoeni` i `decimal PoeniPopust` (10,2, default 0).
`TotalAmount = Subtotal − Popust − PoeniPopust + DeliveryFee`. Indeksi: `PoeniStavka(UserId)`,
`PoeniStavka(OrderId)`. `Vrsta` kao string. FK `PoeniStavka.UserId → User` `Cascade`,
`OrderId → Order` `SetNull`. Jedna migracija `DodajPoene`.

## Pravila — jedno mjesto

`PoeniPravila` (Application, `Features/Poeni`):

```csharp
Task<int> StanjeAsync(IAppDbContext db, Guid userId, CancellationToken ct);
(int poena, decimal popust) ZaTrosenje(int stanje, decimal robaPoslijePromo, PoeniOpcije o);
int ZaradaZaNarudzbu(Order order, PoeniOpcije o);   // floor(Subtotal − Popust − PoeniPopust) × PoenaPoKM
Task ZabiljeziPromjenuStatusaAsync(IAppDbContext db, Order order, OrderStatus novi, PoeniOpcije o, CancellationToken ct);
```

`ZaTrosenje`: `stanje ≤ 0` → (0, 0); `popust = floor(min(stanje × vrijednost, robaPoslijePromo))`
(cijele KM), `poena = popust / vrijednost` (cijeli broj — 20 po KM).

`ZabiljeziPromjenuStatusaAsync` (zove se PRIJE `TransitionTo`, kao `BrojacIsporuka`, iz `AdminOrders`
i `DriverFeature`; ne radi ništa kad je stari status jednak novom):
- novi `Delivered` i `CustomerId` postoji: stavka `Isporuka` sa zaradom (ako > 0, i ako za tu narudžbu
  `Isporuka` već ne postoji); zatim, ako kupac ima `PreporucioId` i za njega još nema stavke
  `PreporukaNovi`: `PreporukaNovi` (+100) njemu i `PreporukaPreporucio` (+100, `PreporuceniId`)
  onome ko ga je preporučio.
- novi `Cancelled` i `IskoristeniPoeni > 0`: stavka `Povrat` (+`IskoristeniPoeni`), ako za tu
  narudžbu `Povrat` već ne postoji.

## API

```
GET  /account/poeni            [Authorize] → { stanje, vrijednostPoenaKM, vrijednostKM, nagradaPreporuka, preporukaKod,
                                brojPreporuka, istorija: [{ iznos, vrsta, opis, orderCode?, kreirana }] (50 najnovijih) }
POST /orders                   + iskoristiPoene?: bool
POST /auth/register            + preporukaKod?: string
```

`opis` gradi server na srpskom po vrsti: „Isporuka EP-…", „Iskorišteno na EP-…", „Vraćeno —
otkazana EP-…", „Preporuka: {Ime P.} je primio prvu isporuku", „Dobrodošlica — preporuka",
„Korekcija". `GET` pravi `PreporukaKod` ako ga korisnik nema (jedinstvenost: do 5 pokušaja).
`CreateOrderHandler`: ako `IskoristiPoene` i kupac prijavljen (`customerId` postoji i nije posao
pretplata), poslije promo popusta: `ZaTrosenje(stanje, subtotal − popust)`; ako `poena > 0` →
stavka `Iskoristeno` (−poena, `OrderId`) u istom upisu, `Order.IskoristeniPoeni/PoeniPopust`.
`iskoristiPoene` za gosta (zahtjev gosta) ne postoji.

`OrderDto` dobija `iskoristeniPoeni` i `poeniPopust`.

## Frontend

| Fajl | Šta |
|---|---|
| `app/(shop)/moj-nalog/poeni/page.tsx` | stanje (veliki broj + „= X KM"), kako se zarađuje (iz vrijednosti sa servera), link za preporuku sa dugmetom „Kopiraj", broj preporuka, istorija |
| `components/nalog/nalog-nav.tsx` | stavka „Poeni" (`Coins`) |
| `app/(shop)/register/page.tsx` | čita `?preporuka=`, šalje `preporukaKod`, napomena „Preporučio te prijatelj — po prvoj isporuci dobijaš 100 poena." |
| `app/(shop)/checkout/page.tsx` | prijavljen i `stanje > 0`: checkbox „Iskoristi poene (N poena = −X KM)" u koraku 3; ukupno uključuje `poeniPopust`; `iskoristiPoene` u tijelu |
| `order-summary-panel.tsx`, `order-receipt.tsx`, `admin/order-card.tsx` | red „Poeni (N)" sa −X KM kad `poeniPopust > 0` |

## Mejl i Viber

Red „Poeni (N): −X KM" kad `PoeniPopust > 0`, kao red popusta iz P6b.

## Testiranje

**Backend:** `ZaTrosenje` (stanje 0/negativno, ograničenje robom, cijele KM, 20 poena po KM);
`ZaradaZaNarudzbu` (floor, bez dostave, poslije promo i poena); status hook — isporuka upisuje zaradu
jednom (ponovno postavljanje istog statusa ne duplira), gost ne dobija ništa, preporuka nagrađuje oba
jednom (druga isporuka ne), otkaz vraća potrošeno jednom; `CreateOrderHandler` troši poene (stavka,
polja, `TotalAmount`), ne troši bez checkboxa, ne troši za stanje 0, poeni poslije promo popusta;
`GET /account/poeni` lijeno pravi kod i vraća istoriju sa opisima; registracija sa važećim kodom
upisuje `PreporucioId`, sa nepoznatim prolazi bez njega.

**Frontend:** `tsc`, `lint`, `npm run build`.
