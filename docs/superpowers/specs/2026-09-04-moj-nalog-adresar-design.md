# Moj nalog — adresar kupca

**Datum:** 2026-09-04
**Grana:** `moj-nalog-adresar`

## Šta se radi

Kupac dobija stranicu „Moj nalog" na kojoj uređuje svoje podatke (ime, telefon) i
vodi **adresar** — više sačuvanih adresa, svaka sa zonom dostave i tačkom na mapi,
od kojih je jedna glavna.

Adresar postaje jedini izvor sačuvanih adresa: checkout ga koristi umjesto
dosadašnjeg spiska izvedenog iz narudžbi.

## Zašto, konkretno

Danas u aplikaciji **ne postoji nijedan način da korisnik promijeni bilo šta o sebi.**
`GET /auth/me` je jedini korisnički endpoint i samo čita. Ime, telefon, adresa i zona
upisuju se pri registraciji i tu ostaju zauvijek.

Ono što checkout naziva „sačuvanim adresama" nisu sačuvane adrese nego tekst izvučen
iz **dostavljenih** narudžbi (`GetMyAddressesQuery`). Te adrese nemaju ni zonu ni
koordinate — `address-chooser.tsx` to i kaže u komentaru: sačuvana adresa ne nosi svoj
pin.

Posljedica nije kozmetička. Vozačka aplikacija navigira po koordinatama, a optimizacija
rute bez tačke pada na sortiranje po zonama. Kupac koji ponovo izabere svoju raniju
adresu šalje narudžbu **bez tačke na mapi** — i to tiho, bez ijedne poruke. Adresar sa
upisanim koordinatama usput popravlja i to.

## Šta se ne radi

- Promjena e-maila. E-mail je i korisničko ime i potvrđena adresa; promjena traži
  ponovnu potvrdu, rukovanje sudarom sa postojećim nalogom i odluku šta biva sa starim
  tokenom. Zaseban posao.
- Promjena lozinke.
- Adresar za vozače i admine kao korisnike panela — stranica je dio shopa.
- Bilo kakva izmjena istorije narudžbi.

## Model podataka

Novi entitet `Address` (`EPiljara.Domain/Entities/Address.cs`), nasljeđuje `Entity`:

| Polje | Tip | Napomena |
|---|---|---|
| `UserId` | `Guid` | FK ka `Users`, **kaskadno brisanje** — adresar nema smisla bez naloga |
| `Label` | `string?` | „Kuća", „Posao"; opciono, max 60 |
| `FullAddress` | `string` | obavezno, max 400 (isto kao `Order.DeliveryAddress`) |
| `Latitude` | `double?` | nullable |
| `Longitude` | `double?` | nullable |
| `DeliveryZoneId` | `Guid?` | FK ka `DeliveryZones`, `OnDelete(Restrict)` — kao i drugdje |
| `IsDefault` | `bool` | tačno jedna glavna po korisniku |
| `CreatedAt` | `DateTime` | |

Dodaje se `DbSet<Address> Addresses` u `IAppDbContext` i konfiguracija u `AppDbContext`,
uz indeks na `UserId`.

### Koordinate su nullable, namjerno

Adrese prenesene migracijom iz `User.Address` nemaju pin i nikad ga nisu imale.
Obavezne koordinate značile bi da migracija mora izmisliti tačku, a izmišljena tačka na
ruti je gora od nikakve — vozač bi bio poslan na pogrešno mjesto sa punim povjerenjem.
Forma gura korisnika da postavi pin, ali ga ne blokira.

### Tačno jedna glavna adresa

Postavljanje nove glavne gasi staru, u **jednom** `SaveChangesAsync`. Ovo nije stilska
sitnica: `AdminDispatch` u ovom repozitoriju već nosi bilješku da razbijanje takve
izmjene na više upita vraća entitete kroz navigaciju i daje pogrešno stanje.

Prva adresa koju korisnik doda automatski je glavna. Brisanje glavne adrese prebacuje
oznaku na najskorije dodanu preostalu; ako više nema nijedne, korisnik ostaje bez glavne
i checkout se ponaša kao za gosta.

### Zona na adresi je opciona, i može zastarjeti

`DeliveryZoneId` je nullable jer adresa prenesena migracijom je možda nema, a i kupac
smije sačuvati adresu prije nego što zna zonu. Adresa bez zone se ponudi na checkoutu
kao i svaka druga, samo ne popunjava polje zone — kupac je bira ručno, kao i sada.

Zona se u međuvremenu može ugasiti (`DeliveryZone.IsActive = false`);
`CreateOrderValidator` tada odbija narudžbu sa `invalid_zone`. Da kupac ne bi udario u
tu grešku tek pri slanju, checkout prefill **preskače neaktivnu zonu** i ostavlja polje
prazno, a adresar takvu adresu prikazuje sa napomenom da zona više nije u ponudi.

### Ograničenje

Najviše **10** adresa po korisniku. Provjera u `AbstractValidator`-u nije dovoljna jer
ne vidi bazu — broji se u handleru i prekoračenje baca `DomainException` sa kodom
`address_limit`.

## Endpointi

Svi `[Authorize]`, u novom `AccountController`:

```
GET    /account/addresses
POST   /account/addresses
PUT    /account/addresses/{id}
DELETE /account/addresses/{id}
POST   /account/addresses/{id}/default
PUT    /account/profile          — ime i telefon
```

Svaki handler koji dira pojedinačnu adresu provjerava vlasništvo:
`address.UserId != current.Id` → `ForbiddenException`. Isti obrazac koji već stoji u
`OrderQueries.cs:71`. Provjera ide **prije** svake izmjene, a ne kroz filter u upitu,
da razlika između „ne postoji" i „nije tvoja" ostane namjerna odluka, a ne slučajna.

Validacija adrese preuzima pravila koja `CreateOrderValidator` već ima za tačku:
opseg širine i dužine, obje ili nijedna, i pripadnost okviru BiH (`GeoBounds`). Te
poruke idu pod ključ `fullAddress`, jer forma ima jedno polje sa mapom — isti razlog
zbog kojeg `CreateOrderValidator` koristi `OverridePropertyName("deliveryAddress")`.

## Brisanje adrese ne dira istoriju

`Order` čuva `DeliveryAddress` kao tekst, isto kao što `OrderItem` čuva cijenu u trenutku
narudžbe. Brisanje adrese je običan `DELETE` reda u `Addresses` i ne mijenja nijednu
raniju narudžbu. Nema stranog ključa sa `Order` ka `Address` i neće ga ni biti — narudžba
mora ostati tačna i kad adresa nestane.

## Migracija

Jedna EF migracija u tri koraka:

1. Kreiranje tabele `Addresses`.
2. Prenos podataka: za svakog korisnika sa nepraznim `Address` upisuje se jedan red
   (`FullAddress` = `User.Address`, `DeliveryZoneId` = `User.DeliveryZoneId`,
   `IsDefault = true`, koordinate `NULL`).
3. Brisanje kolona `Users.Address` i `Users.DeliveryZoneId`.

Korak 2 se piše ručno kao `migrationBuilder.Sql(...)` — EF ga ne generiše sam.
`Down` migracija vraća kolone i prepisuje glavnu adresu nazad; adrese koje nisu glavne
se pri vraćanju gube, i to je prihvaćeno (`Down` postoji za rollback u razvoju, ne kao
garancija očuvanja podataka).

### Šta migracija dira uzvodno

- `RegisterCommand` umjesto polja na korisniku pravi prvu `Address` sa `IsDefault = true`.
- `MeDto` gubi `address` i `deliveryZoneId`; frontend tip `AuthUser` isto.
- `checkout/page.tsx:86` i `:92` prefill uzimaju iz glavne adrese adresara umjesto sa
  korisnika.

## Frontend

Nova ruta **`/account`** (putanje u shopu su engleske: `cart`, `checkout`, `orders`),
natpis u navigaciji **„Moj nalog"**. Dvije sekcije:

- **Podaci** — ime i telefon (izmjenjivi), e-mail prikazan i zaključan, sa kratkim
  objašnjenjem zašto.
- **Adrese** — spisak kartica: oznaka, adresa, zona, značka „Glavna", i naznaka kad
  adresa nema pin. Radnje: dodaj, izmijeni, obriši, postavi kao glavnu. Mapa se ne piše
  iznova nego se koristi postojeći `AddressPicker`.

Link u korisničkom meniju navbara, pored „Moje narudžbe".

`AddressChooser` prima `Address[]` umjesto `string[]`; izbor sačuvane adrese postavlja
adresu, zonu **i** koordinate. `GetMyAddressesQuery`, `orderApi.addresses` i endpoint
`/orders/addresses` se brišu.

## Kako se provjerava

Repozitorij nema automatske testove; provjera je prolaz kroz stvarni tok uz pokrenut
backend i bazu:

- `npx tsc --noEmit` i `npm run lint` bez grešaka, `dotnet build` bez upozorenja.
- Migracija se pusti nad bazom koja **ima** postojeće korisnike sa adresom, pa se
  provjeri da je svaki dobio tačno jednu glavnu adresu.
- Dodavanje adrese sa pinom → checkout je nudi sa zonom i tačkom → narudžba stigne sa
  `latitude`/`longitude` → tačka je vidljiva na admin mapi. Ovo je glavni razlog cijelog
  posla i mora se vidjeti od kraja do kraja.
- Pokušaj izmjene tuđe adrese preko API-ja vraća 403.
- Brisanje adrese ne mijenja ranije narudžbe koje su na nju otišle.
