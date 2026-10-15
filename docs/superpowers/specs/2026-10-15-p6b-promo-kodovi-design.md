# P6b — Promo kodovi

**Datum:** 2026-10-15
**Grana:** `p6b-promo` (iz `p6a-ocjena`, HEAD `a134ac4` — P6a još nije spojena u `main`, pa P6b
gradi na njoj; spajaju se redom)
**Mjesto u cjelini:** P6a ocjena isporuke (gotova) → **P6b promo kodovi** → P6c poeni i referral
(troši poene kroz isti mehanizam popusta) → P7 višejezičnost.

## Šta se radi

Admin pravi promo kodove (procenat ili fiksan iznos, rok važenja, minimum korpe, ukupni limit,
„jednom po kupcu", „samo za nove kupce"). Kupac — prijavljen ili gost — na checkoutu upiše kod,
vidi popust prije slanja, a narudžba pamti kod i iznos popusta. Popust se vidi svuda gdje se
vidi iznos: potvrda, praćenje, „Moje narudžbe", mejl, Viber, admin kartica narudžbe.

## Zašto

Akvizicija kupaca (kod za dobrodošlicu, kod za letak na pijaci) i mehanizam popusta na nivou
narudžbe koji P6c (poeni) ponovo koristi. Danas narudžba ima samo `Subtotal + DeliveryFee`.

## Šta se NE radi u P6b

| Odloženo | Zašto |
|---|---|
| Kod za besplatnu dostavu | Isti efekat daje fiksan iznos; poseban tip je proširenje enuma. |
| Kod vezan za proizvod/kategoriju/korpicu | Popust je na cijeloj robi. |
| Više kodova na jednoj narudžbi | Jedan kod po narudžbi. |
| Kod na pretplatnim narudžbama | Pretplata već ima svoj popust; posao pretplata ne šalje kod. |
| Kod u admin formi „Nova narudžba" | Admin može da unese cijenu ručno; forma ostaje kakva jeste. |
| Lični kodovi po kupcu, referral | P6c. |
| Statistika (prihod po kodu) | Admin vidi broj korišćenja; ostalo kasnije. |

## Odluke (nisu bile postavljene)

| # | Odluka | Cijena ako je pogrešna |
|---|---|---|
| O1 | Popust se računa **samo na robu** (`Subtotal`), nikad na dostavu; nikad veći od `Subtotal` | Kod „−5 KM" na korpi od 4 KM daje 4 KM, ne 5. |
| O2 | **Minimum zone** (`MinOrderAmount`) i **minimum koda** se porede sa `Subtotal` **prije** popusta | Kupac sa 30 KM robe i 20 % popusta prolazi minimum od 30 KM iako plaća 24 KM za robu. |
| O3 | Tipovi: `Procenat` (1–90) i `Iznos` (> 0, ≤ 1000 KM); popust = `Math.Round(Subtotal × p / 100, 2, AwayFromZero)` ili `min(Iznos, Subtotal)` | Procenat iznad 90 nije dozvoljen — 100 % je besplatna roba, to nije promo nego greška u kucanju. |
| O4 | Kod je **velikim slovima**, `A–Z`, `0–9`, `-`, dužine 3–30; unos kupca se trimuje i diže u velika slova prije poređenja | Kupac koji ukuca „dobrodosli10" dobije isti kod; dva koda koja se razlikuju samo po velikim slovima ne mogu postojati. |
| O5 | Kod se **ne mijenja** poslije kreiranja; sve ostalo se mijenja | Pogrešno ukucan kod se gasi i pravi novi. |
| O6 | Rok je `VaziOd`/`VaziDo` (`DateOnly?`, oba uključiva), po **lokalnom datumu u Sarajevu** (`Europe/Sarajevo`) | Kod „do 31.10." važi i u 23:59 tog dana po našem vremenu, nezavisno od UTC servera. |
| O7 | **Otkazane narudžbe ne troše kod** — ni ukupni limit ni „jednom po kupcu" ni „samo novi kupci" ne broje `Cancelled` | Kupac može otkazati pa naručiti opet sa istim kodom — namjerno. |
| O8 | „Jednom po kupcu" i „samo novi kupci" prepoznaju kupca po `CustomerId` **ili** e-mailu (bez obzira na velika slova); prijavljeni bez e-maila — samo po `CustomerId` | Isti čovjek sa drugim e-mailom kao gost prolazi. Telefon se ne koristi (ukucava se u različitim oblicima). |
| O9 | „Samo novi kupci" = nema nijedne neotkazane narudžbe tog kupca | Kupac čija je jedina narudžba otkazana je i dalje „nov". |
| O10 | Ukupni limit (`MaxKoristenja`) se broji kao broj neotkazanih narudžbi sa tim kodom; **bez zaključavanja** | Dva istovremena checkouta mogu preći limit za jedan. Za promo kod je to prihvatljivo. |
| O11 | Gost: kod i popust se snimaju u `GuestOrderRequest` (kao cijene); **pri potvrdi se kod provjerava ponovo**; ako više ne važi → 409 `promo_kod_nevazeci`, zahtjev se briše, narudžba ne nastaje | Gost koji čeka 15 minuta dok se limit potroši mora ponovo poslati narudžbu. Bez ponovne provjere, jedan gost bi sa pet zahtjeva istim e-mailom iskoristio „jednom po kupcu" pet puta. |
| O12 | Neaktivan kod i nepostojeći kod daju **istu poruku** („Promo kod ne postoji ili nije aktivan.") | Ne otkriva se da je ugašeni kod postojao. Ostale poruke (istekao, iskorišten, minimum) su konkretne jer kupcu kažu šta da uradi. |
| O13 | Provjera koda (`POST /promo-kodovi/provjera`) je javna, pod politikom `"auth"` | Pogađanje kodova je ograničeno brzinom; dugački nasumični kodovi su na adminu. |
| O14 | Kod koji je bar jednom korišćen (ijedna narudžba, i otkazana) **ne može se obrisati** → 409 `promo_kod_koristen`; gasi se isključivanjem `Aktivan` | Lista kodova raste. Narudžba bez svog koda bi izgubila objašnjenje popusta. |
| O15 | Narudžba pamti `PromoKodId` (FK, `Restrict`) **i** tekst koda (`PromoKod`) i iznos (`Popust`) | Tekst je snimak — prikaz narudžbe ne zavisi od tabele kodova. |
| O16 | `TotalAmount = Subtotal − Popust + DeliveryFee` | Sve što čita `TotalAmount` (dashboard, vozač, mejl) automatski vidi iznos sa popustom. |
| O17 | Checkout: polje „Promo kod" u koraku 3 („Potvrdi narudžbu"); popust prikazuje **server** (`useQuery` po kodu + stavkama), klijent ga ne računa | Promjena korpe ponovo pita server. |
| O18 | **Bezbjednosna popravka:** `POST /orders` je vezivao `CreateOrderCommand` direktno iz tijela, pa je prijavljeni kupac mogao poslati `customerId` (narudžba na tuđi nalog) ili `pretplataId` (pretplatna cijena). Kontroler sada prima `CreateOrderRequest` bez ta dva polja | Nema — ta polja nikad nisu smjela doći sa žice (komentar na komandi to već tvrdi). |
| O19 | Demo seed dobija kod `DOBRODOSLI` (10 %, samo novi kupci, bez roka); produkcijski seed ga nema | Nema. |

## Model podataka

```csharp
public enum TipPopusta { Procenat, Iznos }

public class PromoKod : Entity
{
    public string Kod { get; set; } = "";          // jedinstven, VELIKA slova, 3–30
    public string Opis { get; set; } = "";         // interni, 0–200
    public TipPopusta Tip { get; set; }
    public decimal Vrijednost { get; set; }        // procenat 1–90 ili iznos u KM
    public decimal? MinIznos { get; set; }         // minimum robe (Subtotal)
    public DateOnly? VaziOd { get; set; }
    public DateOnly? VaziDo { get; set; }
    public int? MaxKoristenja { get; set; }        // ukupno, neotkazane narudžbe
    public bool JednomPoKupcu { get; set; }
    public bool SamoNoviKupci { get; set; }
    public bool Aktivan { get; set; } = true;
    public DateTime Kreiran { get; set; } = DateTime.UtcNow;
}
```

`Order` i `GuestOrderRequest` dobijaju `Guid? PromoKodId`, `string? PromoKod` (max 30) i
`decimal Popust` (podrazumijevano 0, preciznost 10,2). FK `Order.PromoKodId → PromoKod`
`Restrict`; na `GuestOrderRequest` FK `SetNull` (zahtjev je privremen). `Tip` se čuva kao string.
Jedna migracija `DodajPromoKodove`.

## Pravila — jedno mjesto

`PromoKodPravila` (Application, `Features/PromoKodovi`):

```csharp
public static string Normalizuj(string kod);                          // Trim().ToUpperInvariant()
public static decimal Popust(PromoKod kod, decimal subtotal);         // O1, O3
public static async Task<PromoKod> ProvjeriAsync(
    IAppDbContext db, string kod, decimal subtotal, KupacZaPromo kupac, DateOnly danas, CancellationToken ct);
public record KupacZaPromo(Guid? CustomerId, string? Email);
```

`ProvjeriAsync` baca `DomainException(poruka, "promo_kod_nevazeci")` sa jednom od poruka:
- „Promo kod ne postoji ili nije aktivan." (nepostojeći ili `Aktivan = false`)
- „Promo kod još ne važi." (`danas < VaziOd`)
- „Promo kod je istekao." (`danas > VaziDo`)
- „Promo kod je iskorišten." (limit)
- „Promo kod ste već iskoristili." (jednom po kupcu)
- „Promo kod važi samo za prvu narudžbu."
- „Promo kod važi za narudžbe od {Min:0.##} KM." (minimum)

Zove ga provjera, `CreateOrderHandler`, `CreateGuestOrderRequestHandler` i
`ConfirmGuestOrderHandler`. `danas` dolazi iz `LokalniDatum.Danas()` (Sarajevo), u testovima se
prosljeđuje.

## API

```
POST /promo-kodovi/provjera        javno, "auth"; { kod, email?, items: [{productId, quantity}] }
                                   → { kod, opis?, popust, subtotal }
POST /orders                       [Authorize]; tijelo CreateOrderRequest (+ promoKod?)
POST /orders/guest-request         + promoKod?
GET  /admin/promo-kodovi           lista sa brojem korišćenja (neotkazane)
POST /admin/promo-kodovi           PromoKodInput
PUT  /admin/promo-kodovi/{id}      PromoKodInput (Kod se ignoriše — O5)
DELETE /admin/promo-kodovi/{id}    409 promo_kod_koristen ako ima narudžbi
```

Provjera računa `Subtotal` iz trenutnih cijena proizvoda (kao checkout); nedostupan proizvod →
`product_unavailable` kao i pri slanju. Prijavljeni kupac se prepoznaje iz `ICurrentUser`
(e-mail iz naloga), gost po poslanom `email`-u; ako gost još nije unio e-mail, provjera „jednom
po kupcu"/„samo novi" za njega preskače e-mail, a konačna provjera pri slanju je hvata.

`PromoKodInput { kod, opis?, tip, vrijednost, minIznos?, vaziOd?, vaziDo?, maxKoristenja?,
jednomPoKupcu, samoNoviKupci, aktivan }`; validator (srpske poruke, ključevi `input.*`):
kod regex `^[A-Z0-9-]{3,30}$` posle normalizacije; tip `Procenat`/`Iznos` (TryParse + IsDefined);
vrijednost po O3; `minIznos` ≥ 0; `vaziDo ≥ vaziOd` kad su oba; `maxKoristenja` ≥ 1. Duplikat koda
→ 409 `promo_kod_postoji`.

`PromoKodDto { id, kod, opis, tip, vrijednost, minIznos, vaziOd, vaziDo, maxKoristenja,
jednomPoKupcu, samoNoviKupci, aktivan, brojKoristenja, kreiran }`.

`OrderDto` dobija `popust` (decimal) i `promoKod` (string?).

## Frontend

| Fajl | Šta |
|---|---|
| `lib/api/endpoints.ts`, `contracts.ts`, `types/domain.ts` | `promoApi.provjeri`, admin CRUD, `Order.popust/promoKod`, `CreateOrderRequest.promoKod?` |
| `components/shop/promo-kod-polje.tsx` | input + „Primijeni"; primijenjen kod prikazuje „KOD — −X KM" i „Ukloni"; greška ispod polja |
| `app/(shop)/checkout/page.tsx` | korak 3 dobija polje; primijenjen kod ide u tijelo; `useQuery(["promo", kod, stavke, email])` daje popust; ukupno = subtotal − popust + dostava (iznos sa servera) |
| `components/shop/order-summary-panel.tsx` | red „Popust (KOD)" kad postoji |
| `components/shop/order-receipt.tsx` | red „Popust (KOD)" |
| `app/(shop)/moj-nalog/narudzbe/page.tsx` | ukupno ostaje `totalAmount`; ništa novo osim ako lista prikazuje razradu |
| `components/admin/order-card.tsx` | red „Popust (KOD)" |
| `app/admin/promo-kodovi/page.tsx` | lista (kod, popust, važi, korišćeno x / max, aktivan), forma za novi/izmjenu, isključi/uključi, obriši (samo nekorišćen) |
| `components/admin/admin-nav.tsx` | stavka „Promo kodovi" (`TicketPercent`) |

Greška `promo_kod_nevazeci` na slanju se prikazuje ispod polja za kod, ne kao opšta greška.

## Mejl i Viber

`OrderEmailTemplate` i `ViberMessage`: red „Popust (KOD): −X KM" kad je `Popust > 0`.
`GuestOrderEmailTemplate` (mejl sa linkom za potvrdu): isto, iz zahtjeva.

## Testiranje

**Backend (xUnit):** `Popust` (procenat sa zaokruživanjem, iznos, iznos veći od subtotal-a);
`ProvjeriAsync` — svaka od sedam poruka, velika/mala slova unosa, otkazana narudžba ne troši
(limit, jednom po kupcu, samo novi), e-mail bez obzira na velika slova, `VaziDo` uključiv;
`CreateOrderHandler` upisuje `PromoKodId/PromoKod/Popust` i `TotalAmount`; minimum zone se gleda
prije popusta; gost — zahtjev snima popust, potvrda ponovo provjerava i odbija (409, zahtjev
obrisan); provjera endpoint računa subtotal iz cijena; admin CRUD — duplikat 409, kod se ne
mijenja na PUT, brisanje korišćenog 409, `brojKoristenja` ne broji otkazane; validator.
**O18:** test da `CreateOrderRequest` nema `CustomerId`/`PretplataId` (refleksija) i da
kontroler gradi komandu bez njih.

**Frontend:** `tsc`, `lint`, `npm run build`; ručno: Mujo primijeni `DOBRODOSLI` (odbijen — ima
narudžbe), admin napravi `JESEN5` (5 KM), Mujo naruči sa njim, potvrda i admin kartica
pokazuju popust; gost sa kodom kroz potvrdu mejlom (log mejl servis).

## Rizici

| Rizik | Odgovor |
|---|---|
| Pogađanje kodova | Politika `"auth"`; admin bira dugačke kodove. |
| Limit pređen za jedan pri istovremenim narudžbama | O10 — prihvaćeno. |
| Stari klijent bez `promoKod` | Polje je opciono; bez njega sve radi kao prije. |
| `TotalAmount` sada može biti manji od `Subtotal + DeliveryFee` | Svi prikazi dobijaju red popusta; dashboard sabira `TotalAmount`, što je sad tačan prihod. |
