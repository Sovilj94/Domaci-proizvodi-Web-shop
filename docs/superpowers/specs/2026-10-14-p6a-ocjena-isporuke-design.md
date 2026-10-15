# P6a — Ocjena isporuke

**Datum:** 2026-10-14
**Grana:** `p6a-ocjena` (iz `main`, HEAD `193c8e7`)
**Mjesto u cjelini:** P6 (poeni i referral, promo kodovi, ocjena isporuke) je podijeljen na tri
potprojekta jer dva od tri diraju iznos narudžbe:

| | Šta | Zašto ovim redom |
|---|---|---|
| **P6a** | Ocjena isporuke | Nezavisna od cijene; iz nje utisak može nastati sam (obećano u P4, O3) |
| P6b | Promo kodovi | Uvodi popust na narudžbi — mehanizam koji P6c koristi |
| P6c | Poeni i referral | Troši poene kao popust; zavisi od P6b |

## Šta se radi

Kupac poslije isporuke ocijeni narudžbu (1–5 zvjezdica i kratak komentar) i, ako hoće, dozvoli
da se komentar objavi kao utisak. Admin vidi sve ocjene, prosjek, i jednim klikom od dozvoljene
ocjene napravi **neobjavljen** utisak koji zatim uređuje i objavljuje na postojećem ekranu
`/admin/utisci`.

## Zašto

Utisci na početnoj (P4) danas unosi admin rukom i nema dokaza da su od kupca. Ocjena isporuke je
izvor koji to dokazuje: utisak nastao iz nje nosi vezu na stvarnu, isporučenu narudžbu. Uz to,
ocjena je jedini signal o radu vozača i pakovanja koji sistem uopšte dobija.

## Šta se NE radi u P6a

| Odloženo | Zašto |
|---|---|
| Mejl „Kako je prošla isporuka?" poslije isporuke | Kupac sa nalogom vidi poziv u „Mojim narudžbama", gost na stranici praćenja (link je u mejlu potvrde). Mejl je zaseban posao sa servisom za mejl. |
| Ocjena pojedinačnih artikala | Ocjenjuje se isporuka kao cjelina. |
| Izmjena ili brisanje ocjene od strane kupca | Jedna ocjena po narudžbi, konačna (O3). |
| Ocjena vozača na ekranu vozača | Admin vidi ocjene; vozaču se ne prikazuju. |
| Poeni za ocjenu | P6c, ako uopšte. |

**Ne dira se:** cijena, checkout, `Order` (osim čitanja), pretplate, rute.

## Odluke (nisu bile postavljene)

| # | Odluka | Cijena ako je pogrešna |
|---|---|---|
| O1 | Ocijeniti se može samo narudžba u statusu **`Delivered`** | Neuspjela isporuka (`FailedDelivery`) se ne ocjenjuje, iako bi baš ona dala najkorisniji signal. Dodaje se proširenjem jednog uslova. |
| O2 | **Bez roka** za ocjenu | `ZatvorenaU` upisuje samo vozačka aplikacija, admin ne — rok vezan za njega ne bi važio za ručno zatvorene narudžbe. Ocjena stara mjesecima je rijetka i bezopasna. |
| O3 | **Jedna ocjena po narudžbi, konačna** (jedinstven indeks na `OrderId`); drugi pokušaj → 409 `vec_ocijenjeno` | Kupac koji pogriješi zvjezdicu ne može ispraviti. Izmjena unutar kratkog prozora se dodaje kasnije bez promjene modela. |
| O4 | Ocjenjuje **vlasnik** (prijavljen, `CustomerId` narudžbe) ili **gost preko `PublicToken`-a** sa stranice praćenja | Ko ima link iz mejla, može ocijeniti — isto pravo koje već ima da vidi narudžbu. |
| O5 | Komentar je opcion (0–1000); **dozvola za objavu** je zaseban checkbox, podrazumijevano isključen | Manje utisaka; ali objava bez izričite dozvole bi bila objava tuđih riječi. |
| O6 | „Napravi utisak" pravi **neobjavljen** `Utisak` sa imenom u obliku „Ime P." (prvo ime + inicijal prezimena iz `CustomerName`), mjestom = naziv zone narudžbe, tekstom = komentar; admin ga objavljuje na `/admin/utisci` | Admin može htjeti drugi oblik imena — uređuje ga prije objave. Puno ime se nikad ne objavljuje automatski. |
| O7 | `Utisak` dobija nullable `OcjenaId` (jedinstven kad postoji); od jedne ocjene najviše jedan utisak; brisanje utiska oslobađa ocjenu | Nema. Ovo je veza obećana u P4 (O3). |
| O8 | Ocjena bez komentara ili bez dozvole ne može postati utisak (400 `ocjena_nije_za_objavu`) | Nema — to je cijela poenta dozvole. |
| O9 | Admin ekran `/admin/ocjene`: prosjek i broj ocjena, filter po broju zvjezdica, lista najnovijih prvo | Bez grafikona i perioda; dovoljno za početak. |

## Model podataka

```csharp
public class OcjenaIsporuke : Entity
{
    public Guid OrderId { get; set; }          // jedinstven
    public Order? Order { get; set; }
    public int Ocjena { get; set; }            // 1–5
    public string Komentar { get; set; } = ""; // 0–1000, trimovan
    public bool DozvolaZaObjavu { get; set; }
    public DateTime Kreirana { get; set; } = DateTime.UtcNow;
}
```

`Order` → `OcjenaIsporuke`: `Cascade` (narudžbe se u praksi ne brišu; ako se obriše, ocjena nema
smisla). `Utisak.OcjenaId` (`Guid?`), FK `SetNull`, filtrirani jedinstveni indeks `OcjenaId IS NOT NULL`.
Jedna migracija `DodajOcjeneIsporuke`.

## API

```
POST /orders/{id}/ocjena                  [Authorize] vlasnik (uz postojeći GET /orders/{id}); tijelo OcjenaInput { ocjena, komentar?, dozvolaZaObjavu }
POST /orders/track/{token}/ocjena         javno, politika "auth"; isto tijelo
GET  /admin/ocjene?ocjena=                lista + sažetak { prosjek, broj, poBroju: {1..5} }
POST /admin/ocjene/{id}/utisak            pravi neobjavljen utisak (O6–O8) → UtisakDto
```

Postojeći DTO-i dobijaju polje da UI zna stanje:
- `OrderDto` (koristi ga i praćenje i „Moje narudžbe") dobija `ocjena?: { ocjena, komentar }` —
  prisutno kad je narudžba ocijenjena — i `mozeSeOcijeniti: bool` (`Delivered` i još nije ocijenjena).
- `UtisakDto` (admin) dobija `izOcjene: bool`.

Greške: `NotFoundException` za nepostojeću ili tuđu narudžbu (tuđa se ne razlikuje od nepostojeće —
ne otkriva se da postoji); `narudzba_nije_isporucena` (400); `vec_ocijenjeno` (409);
`ocjena_nije_za_objavu` (400); `utisak_vec_postoji` (409). Ključevi validacije `input.ocjena`,
`input.komentar`. Validator počinje sa `Input NotNull` i ima srpsku poruku na svakom pravilu.

## Frontend

| Fajl | Šta |
|---|---|
| `components/ocjena/ocjena-forma.tsx` | pet zvjezdica (radio grupa, tastaturom upravljiva, `aria-label` „N od 5"), komentar, checkbox „Dozvoljavam da se komentar objavi na sajtu (kao „Ime P.")", dugme; prima `posalji(input)` da je dijele obje stranice |
| `components/ocjena/zvjezdice.tsx` | prikaz ocjene (samo čitanje) |
| `app/(shop)/narudzba/[token]/page.tsx` | kad `mozeSeOcijeniti` — forma; kad postoji `ocjena` — „Vaša ocjena" sa zvjezdicama |
| `app/(shop)/moj-nalog/narudzbe/page.tsx` | isto, po narudžbi (forma se otvara klikom „Ocijeni isporuku") |
| `app/admin/ocjene/page.tsx` | sažetak (prosjek sa jednom decimalom, broj, raspodjela 5→1), filter, lista (zvjezdice, komentar, kod narudžbe, kupac, datum, oznaka „dozvoljena objava"), dugme „Napravi utisak" / „Utisak napravljen" |
| `app/admin/utisci/page.tsx` | oznaka „iz ocjene" kod utisaka koji imaju `izOcjene` |
| `components/admin/admin-nav.tsx` | stavka „Ocjene" (`Star`) |

## Testiranje

**Backend (xUnit):** ocjena isporučene narudžbe se upisuje; neisporučena → 400; druga ocjena → 409;
tuđa narudžba (drugi `CustomerId`) → 404; gost preko tokena radi, pogrešan token → 404; validator
(0 i 6 zvjezdica, komentar > 1000, null Input; srpske poruke); `OrderDto.mozeSeOcijeniti` i `ocjena`;
admin sažetak (prosjek, raspodjela, filter); „napravi utisak": ime „Ime P." iz „Ime Prezime", jedno
ime ostaje kako jeste, mjesto = zona, neobjavljen, `OcjenaId` upisan; drugi put → 409; bez dozvole
ili bez komentara → 400; brisanje utiska oslobađa ocjenu.

**Frontend:** `tsc`, `lint`, `npm run build`; ručno: ocjena kao gost sa stranice praćenja i kao Mujo
iz „Mojih narudžbi" na isporučenoj narudžbi, admin pravi utisak, objavi ga, pojavi se na početnoj.

## Rizici

| Rizik | Odgovor |
|---|---|
| Token iz mejla procuri i neko ocijeni tuđu isporuku | Isto pravo kao gledanje narudžbe; jedna ocjena po narudžbi ograničava štetu; admin ne objavljuje bez pregleda. |
| Ime kupca na sajtu bez pristanka | O5/O6 — samo uz checkbox, samo „Ime P.", samo neobjavljeno dok admin ne odluči. |
| Spam ocjena preko javnog endpointa | Politika `"auth"` i uslov `Delivered` + jedna po narudžbi. |

## Napomena o modelu

`Order` dobija samo navigaciju `OcjenaIsporuke? Ocjena` (bez kolone — FK je na ocjeni). `ToDto()`
popunjava `ocjena` i `mozeSeOcijeniti` iz te navigacije, pa je **tri kupčeva upita** (`GetMyOrders`,
`GetOrderById`, `TrackOrder`) moraju `Include`-ovati. Upit koji je ne učita bi za isporučenu,
već ocijenjenu narudžbu rekao `mozeSeOcijeniti: true` — zato test pokriva svaki od ta tri upita.
