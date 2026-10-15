# P7 — Ćirilica (latinica + ćirilica za kupca)

**Datum:** 2026-10-15
**Grana:** `p7-cirilica` (iz `main`, `1a9d1fd`)
**Mjesto u cjelini:** sedmi i posljednji potprojekat (P1…P7); P1–P6c su spojeni u `main`

## Šta se radi

Kupac bira pismo — **latinica** (podrazumijevano) ili **ćirilica** — i cijeli kupčev dio sajta,
mejlovi i Viber poruke stižu mu na tom pismu. Jezik je jedan (srpski); mijenja se samo pismo.

## Odluke korisnika (2026-10-15)

| Pitanje | Odgovor |
|---|---|
| Jezici | Srpski, latinica + ćirilica. **Bez engleskog.** |
| Obim | Kupčev dio (`(shop)`), mejlovi, Viber, poruke grešaka koje kupac vidi. **Admin ostaje na latinici.** |

## Ključni uvid

Ćirilica se iz srpske latinice dobija **mašinski** (preslovljavanje), a obrnuto je jednoznačno.
Zato nema kataloga prevoda, nema novih polja za proizvode, recepte i proizvođače, i nema
ručnog prevođenja: sav tekst — i onaj u kodu i onaj iz baze — preslovljava se na izlazu.

## Pristupi (razmotreni)

| | Pristup | Zašto (ne) |
|---|---|---|
| **A** | **Preslovljavanje na izlazu** — u pregledaču nad DOM-om kupčevog dijela, na serveru nad gotovim HTML-om mejla | **Izabran.** Nijedan od 119 ekrana se ne dira; tekst iz baze dolazi besplatno; novi ekrani rade bez ikakve discipline. Cijena: kratko skrivanje strane dok prvi prolaz ne završi, i pretraživači vide latinicu. |
| B | Katalog poruka (`next-intl`), svaki string kroz `t()`, ćirilični katalog generisan | Ispravan SSR, ali prepravka svih ekrana (~18.800 linija) i tekst iz baze i dalje traži poseban put. Za dva pisma istog jezika je to cijena engleskog bez njegove koristi. |
| C | Preslovljavanje HTML toka u Next middleware-u | Next ne dozvoljava mijenjanje tijela RSC odgovora u middleware-u; lomljivo. |

## Arhitektura

```
frontend/src/lib/pismo/
  preslovi.ts        čista funkcija uCirilicu(), uLatinicu(); bez zavisnosti, relativni importi
  preslovi.test.ts   node --test (Node 24 skida tipove sam); čita zajedničke vektore
  dom.ts             obilazak i MutationObserver nad <body>, document.title
  pismo.ts           čitanje/pisanje kolačića `pismo`, tip Pismo = "lat" | "cir"
components/pismo/
  pismo-provider.tsx klijentska komponenta u (shop)/layout; pokreće dom.ts kad je pismo "cir"
  pismo-prekidac.tsx dugme „Ћирилица" / „Latinica" (navbar + footer)
backend:
  EPiljara.Application/Common/Cirilica.cs   UCirilicu(string), UCirilicuHtml(string)
  Domain/Enums: Pismo { Latinica, Cirilica }
  User.Pismo, Order.Pismo, GuestOrderRequest.Pismo  (migracija DodajPismo, default Latinica)
docs/pismo-vektori.json  zajednički test vektori (latinica → ćirilica) za oba jezika
```

### 1. Pravila preslovljavanja (`preslovi.ts` i `Cirilica.cs` — ista pravila, isti vektori)

- Slovo po slovo, sa digrafima **lj → љ, nj → њ, dž → џ** (i velika slova: `Lj`/`LJ` → `Љ`, `NJ` → `Њ`, `DŽ` → `Џ`).
- **Izuzeci digrafa** — riječi gdje je `nj`/`dž`/`lj` granica prefiksa: listom korijena
  (`nadživ`, `podžup`, `odživ`, `konjug`, `injek`, `tanjug` i slično, mala lista u kodu).
- **Riječ se NE preslovljava** ako:
  - sadrži `q`, `w`, `x` ili `y` (strana riječ: Viber, WhatsApp, Wolt),
  - je adresa e-pošte ili URL (`@`, `://`, `www.`),
  - je kod: sva velika slova i bar jedna cifra ili crtica (`EP-2610-0054`, `UITEST5`, `SMOKE39006`),
  - je na listi brendova: `E piljara`, `Google`, `Facebook`, `Viber`, `OSM`, `OpenStreetMap`.
- Brojevi, interpunkcija i razmaci se ne diraju. `KM` → `КМ`, `kg` → `кг`.
- `uLatinicu()` je obrnuto mapiranje bez izuzetaka (ćirilica je jednoznačna).
- Obje funkcije su **idempotentne**: tekst koji je već na ćirilici izlazi nepromijenjen.

### 2. Pregledač (`dom.ts`, `pismo-provider.tsx`)

- **Gdje:** samo `(shop)` layout. Admin ga ne montira, pa ostaje na latinici.
- **Šta se preslovljava:** tekstualni čvorovi ispod `<body>`; atributi `placeholder`, `title`,
  `aria-label`, `alt`; `document.title`.
- **Šta ne:** sadržaj `<input>`/`<textarea>`/`<select>` vrijednosti koje je kupac unio,
  `<script>`, `<style>`, `[contenteditable]`, i sve unutar `[translate="no"]`. Ovaj atribut
  se dodaje gdje heuristika nije dovoljna: broj narudžbe, primijenjeni promo kod, kod preporuke,
  e-mail i telefon u kontaktu, link za preporuku.
- **Kada:** prvi prolaz u `useEffect` provajdera — **poslije hidratacije**, da React ne vidi
  razliku servera i klijenta. Zatim `MutationObserver` (`childList`, `characterData`,
  `attributes`, `subtree`) preslovljava sve što React kasnije upiše. Pošto je preslovljavanje
  idempotentno i čvor se piše samo kad se vrijednost stvarno mijenja, posmatrač ne ulazi u petlju.
  Čvorovi se nikad ne zamjenjuju — mijenja se samo `nodeValue`, pa React zadržava svoje reference.
- **Bez bljeska latinice:** inline skripta u `<head>` root layouta, prije prvog iscrtavanja,
  čita kolačić i — ako je `cir` i putanja nije `/admin` — postavlja
  `<html data-pismo="cir" lang="sr-Cyrl">`. CSS tada drži `body` nevidljivim dok provajder
  ne postavi `data-pismo-spremno`. Rezervni izlaz bez JS-a: CSS animacija koja `body` pokaže
  poslije 1,5 s u svakom slučaju. Root layout **ne čita kolačić na serveru** — to bi sve
  statičke stranice (○) pretvorilo u dinamičke.
- **Prebacivanje pisma:** prekidač upiše kolačić (godinu dana, `SameSite=Lax`) i radi
  `location.reload()`. Povratak sa ćirilice na latinicu bez ponovnog učitavanja bi tražio
  čuvanje originala po čvoru; ponovno učitavanje je jednostavno i dovoljno za radnju koja se
  radi jednom. Prijavljenom kupcu se pismo i sačuva na nalogu (`PUT /account/pismo`).
- **Pretraga:** upit kucan ćirilicom se prije slanja vraća u latinicu (`uLatinicu`), jer su
  nazivi u bazi na latinici. Ostala polja (ime, adresa, napomena) šalju se kako su otkucana.
- **Fontovi:** DM Sans **nema ćirilicu** (provjereno u `next/font` podacima). Na ćirilici tijelo
  prelazi na **Onest** (ima `cyrillic`, sličnog je geometrijskog karaktera), učitan sa
  `preload: false` da latinica ne plaća njegovo preuzimanje. Playfair Display dobija subset
  `cyrillic`. `lang="sr-Cyrl"` uključuje srpske oblike slova (`б`, `г`, `д`, `п`, `т`) gdje ih font ima.

### 3. Server (mejlovi i Viber)

- Frontend šalje zaglavlje **`X-Pismo: cir`** na svaki zahtjev kad je pismo ćirilica
  (`lib/api/client.ts`, jedno mjesto). API ga čita kroz `IPismoZahtjeva` (API sloj,
  podrazumijevano `Latinica` za nepoznato/odsutno zaglavlje).
- **Gdje se pamti:**
  - `Order.Pismo` — pri nastanku narudžbe iz zaglavlja; za pretplatne narudžbe (pozadinski posao,
    bez zahtjeva) iz `User.Pismo` vlasnika pretplate.
  - `GuestOrderRequest.Pismo` — pri zahtjevu gosta; prenosi se na `Order` pri potvrdi.
  - `User.Pismo` — pri registraciji iz zaglavlja; mijenja se `PUT /account/pismo`
    (`{ pismo: "Latinica" | "Cirilica" }`, isti obrazac enuma na žici kao ostatak API-ja).
- **Mejlovi:** šabloni ostaju kakvi jesu. `SmtpEmailService` pošalje
  `Cirilica.UCirilicuHtml(html)` i `Cirilica.UCirilicu(naslov)` kad je pismo ćirilica.
  `UCirilicuHtml` preslovljava samo tekst između tagova; tagove, atribute (uključujući `href`)
  i HTML entitete ne dira. Pismo za mejl: narudžba → `Order.Pismo`; gost → `GuestOrderRequest.Pismo`;
  potvrda naloga → `User.Pismo`.
- **Viber:** `ViberMessage.Build` rezultat kroz `UCirilicu` po `Order.Pismo`.
- **Poruke grešaka sa API-ja** se na serveru ne diraju — kupac ih vidi u DOM-u, gdje ih
  preslovljava `dom.ts`.

## Greške i rubni slučajevi

| Slučaj | Ponašanje |
|---|---|
| JS isključen | Latinica (kolačić se ne čita, `body` se pokaže poslije 1,5 s). |
| Kolačić `pismo` ima nepoznatu vrijednost | Latinica. |
| `X-Pismo` sa nepoznatom vrijednošću | Latinica, bez greške. |
| Prelaz iz prodavnice u admin klijentskom navigacijom | Admin layout ne montira provajder; `data-pismo` se pri demontaži provajdera skida. |
| Strana riječ bez q/w/x/y (npr. „Leaflet" u atribuciji mape) | Atribucija mape je u `[translate="no"]`. Za ostalo se dodaje atribut kad se primijeti. |
| Korisnik kuca ćirilicom u formu | Šalje se kako je otkucano (osim pretrage). |

## Testiranje

- **Vektori** (`docs/pismo-vektori.json`): parovi ulaz → očekivano, uključujući digrafe, izuzetke
  digrafa, velika slova, kodove, e-poštu, URL, brendove, idempotentnost. Isti fajl čitaju
  `preslovi.test.ts` (`npm test` → `node --test`) i xUnit testovi `Cirilica`.
- **Backend xUnit:** `UCirilicuHtml` ne dira tagove/`href`/entitete; `X-Pismo` → `Order.Pismo`;
  gost → potvrda nosi pismo; pretplatna narudžba uzima `User.Pismo`; `PUT /account/pismo`
  (validacija enuma, `Input NotNull`); mejl narudžbe na ćirilici sadrži „Хвала" a ne „Hvala".
- **Uživo (pregledač):** svaka stranica kupčevog dijela na ćirilici — skripta koja izlista
  tekstualne čvorove sa latiničnim slovima izvan `[translate="no"]` mora vratiti samo dozvoljene
  (brend, kodovi, strane riječi); nema hidratacijskih grešaka u konzoli; checkout do kraja na
  ćirilici; prebacivanje nazad na latinicu; admin ostaje na latinici i kad je kolačić `cir`;
  375 px bez horizontalnog skrola (ćirilica je šira); mejl u Mailpitu na ćirilici.
- Uobičajeno: `dotnet test`, `tsc`, `lint`, `npm run build`.

## Van opsega

- Engleski i bilo koji drugi jezik.
- Admin panel i vozačka aplikacija.
- Posebni URL-ovi po pismu (`/cir/...`) i indeksiranje ćirilice kod pretraživača.
- Ćirilični slugovi (URL-ovi ostaju latinični).
- Preslovljavanje teksta koji je kupac sam unio (ime, adresa, napomena).
