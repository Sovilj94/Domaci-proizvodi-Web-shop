# Naručivanje bez naloga — potvrda mejlom

**Datum:** 2026-09-24
**Grana:** `feat/narucivanje-bez-naloga`

## Šta se radi

Kupac koji nema nalog može naručiti, ali mu narudžba nastaje **tek kad klikne link u
mejlu**, i to u roku od 15 minuta. Prijavljenom kupcu se ništa ne traži — njegova
narudžba nastaje odmah, a mejl mu javlja detalje i kad da očekuje dostavu.

Između korpe i forme staje nova stranica koja pita ima li nalog i nudi tri puta:
prijava, registracija, ili naručivanje bez naloga.

Forma za dostavu se preuređuje po uzoru na `ananas.ba/placanje/dostava`: ime i prezime
u dva polja, ulica sa kućnim brojem i stanom, telefon sa pozivnim, napomena dostavljaču
sa brojačem znakova, i sažetak narudžbe sa strane. Ekran poslije slanja se preuređuje po
uzoru na njihov `/hvala-na-porudzbini`: broj narudžbe, stavke, podaci o dostavi, način
plaćanja i razložen iznos.

## Zašto, konkretno

Naručivanje bez naloga **tehnički već radi** — `POST /orders` je anoniman i kontroler to
izričito kaže u komentaru. Ali aplikacija to nigdje ne nudi: „Nastavi na dostavu" iz
korpe vodi pravo na formu, pa kupac bez naloga ne zna da smije naručiti, a onaj sa
nalogom ne dobije priliku da se prijavi i povuče svoje adrese.

Druga strana je zloupotreba. Anonimna narudžba danas ulazi u dispečerski ekran bez
ijedne provjere — dovoljno je upisati tuđu adresu i poslati. Potvrda mejlom traži da
onaj ko naručuje bar ima pristup adresi koju je upisao.

## Šta se ne radi

- **Fizičko / pravno lice.** Ananas to traži jer izdaje račun na firmu; mi nemamo
  fakturisanje.
- **Poštanski broj i mjesto.** Dostavljamo samo po Banjaluci, a zone to već pokrivaju
  zajedno sa minimalnim iznosom i cijenom dostave.
- **Autocomplete ulica.** Već imamo `AddressChooser` sa mapom i geokodiranjem; on
  ostaje za ulicu.
- **Plaćanje karticom.** Ostaje pouzeće.
- **Naknada za plaćanje pouzećem i podaci za slanje računa.** Ananas ih ima jer naplaćuje
  pouzeće i izdaje račun; kod nas je dostava jedini trošak.
- **Pretraga narudžbe po broju i e-mailu.** Praćenje ide isključivo preko trajnog linka
  iz mejla; forma za pretragu bi bila način da se pogađaju tuđe narudžbe.
- **Nalog za gosta poslije narudžbe.** Ananas na ekranu zahvale nudi „unesi lozinku i
  sačuvaćemo podatke". Zaseban posao.
- **Slanje novog linka.** Istekao zahtjev se briše; kupac šalje formu ponovo.

## Tok

```
/cart ──„Nastavi na dostavu"──▶ /checkout/nalog
                                  ├── Prijavi se    → /login?next=/checkout
                                  ├── Registruj se  → /register?next=/checkout
                                  └── Bez naloga    → /checkout?gost=1
/checkout  (Adresa ─ Podaci ─ Potvrda)
   │
   ├─ prijavljen → POST /orders ────────────▶ narudžba odmah + mejl (detalji + termin)
   │                                          ekran „Narudžba primljena"
   └─ gost      → POST /orders/guest-request ▶ zahtjev, NEMA narudžbe, mejl sa linkom
                                              ekran „Provjeri mejl" + odbrojavanje
                                                 │
                              klik na link ──▶ /potvrda/[token] → POST /orders/confirm
                                                 ├─ vrijedi  → narudžba nastaje,
                                                 │             korpa se čisti tek tu
                                                 └─ isteklo  → „Potvrda je istekla"
                                                               + „Vrati u korpu"
```

Prijavljenom kupcu se `/checkout/nalog` preskače — ide pravo na `/checkout`.

## Model podataka

Dvije nove tabele. Nijedna ne dira `Orders`.

**`GuestOrderRequest`**

| Kolona | Tip | Napomena |
|---|---|---|
| `Id` | uuid | |
| `Token` | text, unique | `Guid.NewGuid().ToString("N")`, isto kao potvrda registracije |
| `ExpiresAt` | timestamptz | `CreatedAt + 15 min` |
| `CreatedAt` | timestamptz | |
| `CustomerName` | text | |
| `CustomerEmail` | text | obavezan — bez njega nema gdje poslati link |
| `CustomerPhone` | text | |
| `DeliveryZoneId` | uuid | FK |
| `DeliverySlotId` | uuid | FK |
| `DeliveryAddress` | text | |
| `Latitude`, `Longitude` | double? | |
| `Note` | text? | |
| `Subtotal`, `DeliveryFee`, `TotalAmount` | numeric | zaključani u trenutku slanja |

**`GuestOrderRequestItem`** — `Id`, `RequestId` (FK, cascade delete), `ProductId`,
`ProductName`, `Unit`, `UnitPrice`, `Quantity`.

**`Orders` dobija `PublicToken`** — text, unique, `Guid.NewGuid().ToString("N")`, upisuje
se svakoj novoj narudžbi. To je trajni ključ za praćenje narudžbe bez naloga; ne ističe
i ne briše se. Razlikuj ga od tokena zahtjeva: onaj živi 15 minuta i služi da narudžba
uopšte nastane, ovaj živi zauvijek i služi samo da se narudžba pogleda.

Migracija mora popuniti `PublicToken` i **postojećim** narudžbama, inače kolona ne može
biti unique i not-null.

Stavke nose **snimljenu cijenu, naziv i jedinicu**. To je isti princip po kojem
`OrderItem` ionako snima podatke u trenutku naručivanja — ovdje samo pomjeren unazad,
na trenutak slanja forme. Kupac plaća ono što je vidio, i ako cijena u međuvremenu
poraste i ako padne.

`ProductId` ostaje zapisan bez stranog ključa na `Products`, da brisanje proizvoda ne
obori zahtjev prije nego mu istekne vrijeme; dostupnost se provjerava tek na potvrdu.

## Endpointi

**`POST /orders/guest-request`** — anoniman. Tijelo je isto kao `CreateOrderCommand` uz
obavezan `customer.email`. Provjerava sve što i `CreateOrder`: zona i termin postoje,
proizvodi su dostupni, iznos je iznad minimuma za zonu. Upisuje zahtjev, šalje mejl sa
linkom, vraća `{ expiresAt }`. **Narudžbu ne pravi.**

Prijavljeni kupac koji pogodi ovaj endpoint dobija 400 `already_authenticated` — njegov
put je `POST /orders`.

**`POST /orders/confirm`** — anoniman, tijelo `{ token }`. Nađe zahtjev kojem
`ExpiresAt > now`; u jednoj transakciji napravi `Order` sa `Status = Pending` i
`OrderItems` po snimljenim cijenama, obriše zahtjev, pošalje mejl sa detaljima
narudžbe i terminom. Vraća `OrderDto` — isti oblik koji `POST /orders` već vraća, pa
frontend prikaže postojeći ekran potvrde bez ijedne izmjene.

**`GET /orders/track/{publicToken}`** — anoniman. Vraća `OrderDto` za narudžbu sa tim
tokenom, ili 404 ako ga nema. Ovo je naš „Status narudžbe": gost otvara link iz mejla i
vidi gdje mu je narudžba, bez naloga i bez isteka.

Token je jedina zaštita, pa mora biti nepogodiv — `Guid.NewGuid().ToString("N")` je 32
heksadecimalna znaka i to zadovoljava. Endpoint ide pod **stroži rate limit** (`auth`
particija), da se ne može gruba sila po tokenima. Odgovor nosi isto što i narudžba sama,
dakle ime, telefon i adresu kupca — zato token i ne smije nikad završiti u URL-u koji se
dijeli dalje; u mejlu je i tu ostaje.

Greške:

| Slučaj | Odgovor |
|---|---|
| token ne postoji ili je istekao i obrisan | 410 `guest_request_expired` |
| proizvod je u međuvremenu ugašen ili obrisan | 409 `product_unavailable`, sa nazivom proizvoda u poruci |
| token je već iskorišten | 410 `guest_request_expired` — zahtjev je obrisan pri potvrdi, pa se ne razlikuje od isteklog |

Dvoklik na link u mejlu tako ne pravi dvije narudžbe: prvi klik obriše zahtjev, drugi
dobije 410. Ali kupac koji dvaput klikne vidi „potvrda je istekla" iako je narudžba
uredno prošla, pa ekran isteka mora reći i to — da je narudžba možda već primljena i da
provjeri mejl prije nego šalje ponovo. Bez te rečenice bi isti ekran značio dvije
suprotne stvari.

## Čišćenje

`ExpiredGuestRequestsCleaner : BackgroundService` budi se svakih 5 minuta i briše
zahtjeve kojima je `ExpiresAt` prošao. Prvi pozadinski servis u projektu, registruje se
u `Program.cs` preko `AddHostedService`.

Potvrda se **ne oslanja** na čistač: provjerava `ExpiresAt` sama, pa zahtjev koji čeka
brisanje ne može proći. Čistač postoji samo da smetlje stvarno nestane, kako je
traženo.

## Mejlovi

Dva šablona, oba kroz postojeći `IEmailService`:

1. **Potvrda gosta** — novi `GuestOrderConfirmTemplate`. Link
   `{App:BaseUrl}/potvrda/{token}`, jasno napisano da vrijedi 15 minuta i da narudžba
   do tada ne postoji. Sažetak stavki i iznos.
2. **Obavještenje o narudžbi** — postojeći `OrderEmailTemplate`, dopunjen **terminom
   dostave** (dan i vremenski prozor iz `DeliverySlot`) jer to je ono „kad očekuje da
   će doći", i **trajnim linkom** `{App:BaseUrl}/narudzba/{publicToken}` za praćenje.
   Ide i prijavljenom kupcu odmah, i gostu nakon potvrde.

Slanje mejla za potvrdu gosta **nije** best-effort. Ako mejl ne prođe, kupac nema kako
potvrditi, pa `guest-request` vraća 500 i zahtjev se ne upisuje — suprotno od
`CreateOrder`, gdje neuspjeh mejla namjerno ne obara narudžbu. Ta razlika je namjerna i
treba da ostane napisana u kodu.

## Frontend

**`/checkout/nalog`** — tri kartice: „Prijavi se", „Registruj se", „Naruči bez naloga".
Prve dvije vode na `/login` odnosno `/register` sa `?next=/checkout`, treća na
`/checkout?gost=1`. Prijavljen kupac se preusmjerava na `/checkout`.

**`/checkout`** — postojeći stepper `Adresa → Podaci → Potvrda` ostaje, forma se
preuređuje:

- **Podaci:** Ime i Prezime u dva polja, telefon sa fiksnim `+387` prefiksom, e-mail
  (obavezan za gosta, predpopunjen za prijavljenog)
- **Adresa:** `AddressChooser` za ulicu, plus zasebna polja Kućni broj i Stan
- **Napomena:** textarea „Pomozi dostavljaču da lakše pronađe lokaciju" sa brojačem
- **Sažetak narudžbe** sa strane (stavke, iznos, dostava, ukupno) umjesto samo iznosa u
  dnu

Ime i prezime se spajaju u `customer.name`, a ulica, broj i stan u `deliveryAddress`.
**Ugovor sa backendom ostaje isti**, pa admin, dispečerski ekran, vozačka aplikacija i
geokodiranje rade kao i dosad.

**Ekran „Provjeri mejl"** — prikazuje se gostu poslije slanja: na koju adresu je poslano
i odbrojavanje do isteka. Korpa se **ne čisti**.

**Ekran zahvale** — postojeći `Confirmation` se proširuje po uzoru na ananasov
`/hvala-na-porudzbini`. Danas pokazuje samo broj, status, termin, adresu i ukupno.
Novi raspored, u dvije kolone:

*Lijevo:*
- „Hvala ti!" sa brojem narudžbe i rečenicom da je potvrda poslana na e-poštu, sa
  ispisanom adresom
- **Narudžba** — u zaglavlju **kad se očekuje dostava** (dan i vremenski prozor iz
  termina), pa stavke sa slikom, količinom i cijenom po stavci
- **Podaci o dostavi** — ime, telefon, adresa, napomena dostavljaču
- **Način plaćanja** — pouzećem pri dostavi

*Desno:*
- **Ukupna cijena** — iznos stavki, trošak dostave, ukupno
- dugmad „Nastavi kupovinu" i, za prijavljenog, „Moje narudžbe"

Isti ekran vidi i prijavljeni kupac odmah po slanju, i gost poslije potvrde mejlom, i
svako ko kasnije otvori trajni link — jedna komponenta, tri ulaza. Na trajnom linku se
umjesto „Hvala ti!" prikazuje **trenutni status** narudžbe (`StatusBadge`), jer tada to
više nije trenutak zahvale nego praćenje.

**`/narudzba/[token]`** — ruta za trajni link iz mejla. Zove `GET /orders/track/{token}`
i crta isti ekran. Nema `clear()` korpe i nema odbrojavanja; 404 daje poruku da narudžba
ne postoji ili da je link pogrešan.

`OrderDto` dobija **`CustomerEmail`**. Sve ostalo što ekran traži — stavke, iznosi,
telefon, adresa, napomena, termin — DTO već nosi. Bez toga gost poslije potvrde ne bi
imao odakle prikazati adresu na koju je mejl poslan, jer je forma do tada zaboravljena.
Polje je dodatak, ne izmjena postojećih, pa ništa nizvodno ne puca.

**`/potvrda/[token]`** — šalje `POST /orders/confirm` i prikazuje postojeći
`Confirmation` ekran ako uspije (uz `clear()` korpe tu), a „Potvrda je istekla" sa
dugmetom „Vrati u korpu" ako ne. Korpa je i dalje u `localStorage` istog pretraživača,
pa povratak stavki ne traži ništa sa servera — dugme samo vodi na `/checkout`.

Mock (`frontend/src/server/mock/`) se **ne dira**; nova dva endpointa rade samo protiv
pravog backenda, kao i ostatak dispečerskog i `account/*` dijela.

## Rizik

`AddressChooser` danas prima jednu adresu i iz nje vadi koordinate. Kad se razbije na
ulicu i broj, geokodiranju mora ići spojeno `"ulica broj"` ili će pin biti na sredini
ulice umjesto na kući. Vozačka aplikacija navigira po koordinatama, pa je ovo jedino
mjesto gdje preuređenje forme može naškoditi dostavi — rješava se tako što se
`AddressChooser` hrani spojenom vrijednošću, a ne samo ulicom.

## Testovi

Handler testovi (`backend/tests/EPiljara.Application.Tests`, SQLite in-memory):

- zahtjev se upisuje sa snimljenim cijenama, narudžba se **ne** pravi
- potvrda pravi narudžbu sa snimljenim cijenama i briše zahtjev
- potvrda istekloga baca `guest_request_expired`
- potvrda istim tokenom dvaput pravi tačno jednu narudžbu
- potvrda pada ako je proizvod u međuvremenu ugašen
- prijavljeni kupac ne može kroz `guest-request`
- čistač briše istekle, a netaknute ostavlja
- svaka nova narudžba dobija `PublicToken`, i dvije narudžbe nikad ne dijele isti
- praćenje po tokenu vraća narudžbu, a pogrešan token 404

Frontend se provjerava sa `npx tsc --noEmit`, `npm run lint` i prolaskom kroz oba toka
uz pokrenut stek — gost do potvrde mejlom preko Mailpita (`:8025`), i prijavljen kupac.
