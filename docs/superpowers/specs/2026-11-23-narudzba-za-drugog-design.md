# Narudžba za nekog drugog (primalac) — dizajn

Datum: 2026-11-23

## Namjera

Prijavljeni kupac želi da naruči korpicu (ili špajz) za nekog drugog — roditelje, rodbinu,
prijatelja — na **njihovu** adresu. Primjer vlasnika: kupac naručuje za roditelje; **roditelji
primaju i plaćaju pouzećem, vozač zove njih**. Narudžba ipak pripada kupcu: vidi je u „Moje
narudžbe", potvrda stiže **samo njemu**.

Danas se to može samo „na silu": na koraku 2 prepisati svoje ime i telefon roditeljskim. Tada
narudžba gubi podatke o tome ko je naručio, i ništa se ne pamti za sljedeći put.

## Ciljevi

- U checkoutu, prijavljeni kupac može izabrati **„Naruči za nekog drugog"**: adresa + ime i
  telefon primaoca.
- Primalac sa adresom se **pamti** i sljedeći put se samo bira (npr. „Mama i tata").
- Korak 2 ostaje **„Tvoji podaci"** — naručilac (ime, telefon, e-mail) se ne mijenja.
- Vozač vidi i zove **primaoca**; admin vidi i naručioca i primaoca.

## Ne-ciljevi

- Gost (bez naloga) ne dobija ovu opciju — za sada.
- Obavještenje primaocu (mejl/Viber/SMS) — potvrda ide samo naručiocu (odluka vlasnika).
- Pretplate za drugu osobu — pretplatna narudžba ostaje kakva jeste.
- Izmjene vozačke Android aplikacije (`../EPiljaraVozac`).
- Plaćanje unaprijed od strane naručioca — i dalje pouzeće, plaća primalac.

## Anti-ciljevi (neuspjeh i ako „radi")

- Vozač zove naručioca umjesto primaoca.
- Potvrda ili link za praćenje odu primaocu umjesto naručiocu.
- Kupac koji naručuje za sebe primijeti ikakvu promjenu toka (dodatni klik, novo obavezno polje).
- Primalac „procuri" u tuđu narudžbu (npr. ostane popunjen kad kupac pređe na svoju adresu).

## Kako (odluke vlasnika)

- Korak 2 ostaje „Tvoji podaci".
- Na koraku 1 postoje opcije „Nova adresa" i „Naruči za nekog drugog".
- Primalac i adresa se čuvaju.
- Potvrda samo naručiocu.

## Kako (moje odluke — graditelj ih može preispitati)

### Podaci

- **`Address` (Adresar) dobija `PrimalacIme` i `PrimalacTelefon`** (oba nullable). Adresa bez
  primaoca je „moja"; sa primaocem je „za nekog drugog". Nema posebne tabele osoba — primalac
  živi uz adresu, jer je ta veza (roditelji ↔ njihova kuća) upravo ono što se ponavlja.
  Granica od 10 adresa važi za sve zajedno.
- **`Order` dobija `PrimalacIme` i `PrimalacTelefon`** (nullable, snimak u trenutku narudžbe, kao
  i ostala polja narudžbe — kasnija izmjena adrese u adresaru ne mijenja narudžbu).
  `CustomerName/Phone/Email` ostaju **naručilac**, bez promjene značenja.
- Migracija: dvije kolone na `Addresses`, dvije na `Orders`. Bez podataka u migraciji.
- Validacija (backend): ime i telefon primaoca idu **u paru** — oba ili nijedno; ime ≤ 160,
  telefon ≤ 40 (iste granice kao `Customer`).

### Ugovor (HTTP)

- `CreateOrderRequest` (i `CreateOrderCommand`) dobija opcioni `Primalac { Ime, Telefon }`.
  Samo `POST /orders` (prijavljeni). `guest-request` ga ne prima.
- Adresar `POST/PUT /account/addresses` dobija opcione `primalacIme`, `primalacTelefon`;
  `AddressDto` ih vraća.
- `OrderDto` (kupac i admin) dobija `primalacIme?`, `primalacTelefon?`.
- **Vozač:** `RouteStopDto.CustomerName/CustomerPhone` (`MappingExtensions.ToDto(RouteStop)`)
  vraća **primaoca kad postoji**, inače naručioca. Tako vozačka aplikacija bez izmjene zove pravu
  osobu. Isto važi za sve što vozač ili pakovanje koriste kao „kome se nosi":
  `Pakovanje.cs` (`Kupac`/`Telefon` na listi pakovanja) → primalac kad postoji.
- Pretraga admin narudžbi (`AdminOrders.cs`) traži i po imenu/telefonu primaoca.
- Mejl potvrde (`OrderEmailTemplate`) ide naručiocu kao i danas i dobija red
  „Prima: {ime}, {telefon}" kad primalac postoji.
- Fiskalni račun / poslovanje (`Poslovanje*`) — ne dirati; kupac je naručilac.

### Checkout (frontend)

Korak 1, prijavljeni kupac (`AddressChooser`):

- Lista: adrese iz **Adresara** (sa nazivom; ako imaju primaoca, ispod piše „Prima: Ime · tel."),
  zatim ranije adrese iz narudžbi (`GET /orders/addresses`) koje nisu već u adresaru (poređenje
  po tekstu adrese).
- Adresa iz adresara nosi svoju tačku → ne geokodira se ponovo. Bez tačke → isto ponašanje kao
  za raniju adresu (geokodiranje, pa „Potvrdi lokaciju na mapi").
- Dugmad ispod liste: **„Nova adresa"** (kao danas) i **„Naruči za nekog drugog"**.
- „Naruči za nekog drugog" otvara: mapu (`AddressPicker`, kućni broj, stan — isto kao „Nova
  adresa"), plus polja **Ime i prezime primaoca** i **Telefon primaoca** (+387 prefiks kao na
  koraku 2), plus opcioni **Naziv** („npr. Mama i tata"). Oba polja primaoca su obavezna u tom
  režimu — korak 1 ne ide dalje bez njih.
- Ta adresa se **automatski sačuva u Adresar** pri uspješnoj narudžbi (vlasnik: „neka se čuvaju
  podaci"). Ako je adresar pun (10), narudžba ide normalno, a kupac dobija poruku da adresa nije
  sačuvana. Čuvanje ne smije oboriti narudžbu.
- Izbor adrese sa primaocem iz liste postavlja primaoca; izbor bilo koje druge adrese ga **briše**.
- Gost: bez promjene (nema liste, nema opcije).

Korak 2: bez promjene („Tvoji podaci").

Korak 3 (pregled): ispod adrese red „Prima: Ime Prezime · +387 …" kad postoji primalac.

### Adresar (`/moj-nalog/adrese`)

- Forma adrese (`adresa-forma.tsx`) dobija opcionu sekciju „Ova adresa je za nekog drugog" sa
  imenom i telefonom primaoca (par — oba ili nijedno).
- Kartica adrese pokazuje primaoca kad postoji.

### Moje narudžbe / praćenje / admin

- Prikaz narudžbe (kupac, `/narudzba/[token]`, admin kartica) pokazuje „Prima: …" kad postoji.
- Admin narudžba: „Naručio" (postojeći kupac) i „Prima" (novi red).

## Ograničenja

- Pravila iz `CLAUDE.md` važe: `POST /orders` vezuje `CreateOrderRequest`, nikad komandu; null
  polja se ne serijalizuju (`?:` u TS, nikad `| null`); poruke na srpskom; validatori počinju sa
  `RuleFor(x => x.Input).NotNull()` gdje je primjenjivo.
- Dva mjesta prave `Order` (`CreateOrder.cs`, `GuestOrderRequests.cs`). Primalac postoji samo u
  prvom (gost ga nema) — namjerno; zabilježiti to u `CLAUDE.md`.
- Frontend provjera: `npx tsc --noEmit`, `npm run lint`, `npm run build`. Backend: `dotnet test`
  — testovi za: narudžbu sa primaocem (snimak na `Order`), par-validaciju, `RouteStopDto` vraća
  primaoca, adresar čuva primaoca.

## Odluke iz provjere dokumenta (moje, ne vlasnikove)

- **Unaprijed izabrana adresa** u checkoutu: podrazumijevana adresa iz adresara ako **nema
  primaoca**; inače prva adresa iz adresara bez primaoca; inače prva ranija adresa (kao danas).
  Adresa sa primaocem se nikad ne bira sama — kupac koji naručuje za sebe ne smije slučajno
  poslati korpicu roditeljima (anti-cilj).
- **Automatsko čuvanje** radi klijent, poslije uspješne `POST /orders` (`accountApi` create),
  bez čekanja i bez greške koja vraća narudžbu nazad. Samo za „Naruči za nekog drugog", ne za
  „Nova adresa". Ako u adresaru već postoji adresa sa istim tekstom, ne pravi se duplikat.
- **Telefon primaoca** se šalje u istom obliku kao telefon kupca (`+387 …`), sa istim poljem i
  prefiksom.
- **Viber rezerva** (naručilac bez e-maila) ide na telefon **naručioca**, ne primaoca — u
  skladu sa „potvrda samo naručiocu".
- Dispečerski i ekrani ruta u adminu koriste `RouteStopDto`, pa i oni pokazuju primaoca — to je
  osoba kojoj se nosi, i to je ispravno.

## Prepušteno graditelju

- Tačan raspored i tekstovi u UI-u, u postojećem stilu (farmit izgled).
- Naziv migracije, imena DTO polja u skladu sa postojećom konvencijom.
- Da li se automatsko čuvanje radi na klijentu poslije uspješne narudžbe (poziv `accountApi`)
  ili na serveru u `CreateOrder` — prednost ima ono što ne može oboriti narudžbu.
