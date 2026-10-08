# P2a — Moj nalog

**Datum:** 2026-10-08
**Grana:** `moj-nalog-p2a` (iz `redizajn-opcija3`)
**Mjesto u cjelini:** prva polovina P2; druga je P2b — kupčeva izmjena sadržaja korpice

## Šta se radi

Kupac dobija jedno mjesto za sve o sebi: `/moj-nalog`, sa četiri sekcije — Pregled, Lični
podaci, Adrese i Narudžbe.

Ovo je uglavnom **oživljavanje onoga što već postoji**. Adresar je na backendu kompletan od
septembra i nijedan ekran ga nikad nije pozvao. Istorija narudžbi ima svoj ekran koji se samo
seli. Jedino stvarno novo je jedan endpoint za izmjenu imena i telefona.

## Zašto

Danas u aplikaciji **ne postoji nijedan način da kupac promijeni išta o sebi.** Ime, telefon i
adresa upišu se pri registraciji i tu ostanu zauvijek. `GET /auth/me` je jedini korisnički
endpoint i samo čita.

Posljedica nije kozmetička. Telefon je broj koji vozač zove kad stoji pred zgradom. Kupac koji
ga promijeni nema gdje to da upiše, pa dostava ide na stari broj.

Adresar je gori slučaj: backend ima `Address` entitet sa labelom, pinom, zonom i glavnom
adresom, ima validaciju, testove i migraciju — i sve to nikad nije pozvano. Checkout umjesto
njega nudi spisak izveden iz dostavljenih narudžbi, koji **nema koordinate**, pa narudžba ide
bez pina na mapi i optimizacija rute pada na sortiranje po zonama.

## Šta se NE radi u P2a

| Odloženo | Gdje ide |
|---|---|
| Kupčeva izmjena sadržaja korpice, budžet 95–100% | P2b |
| Sedmica na `Order`, pakerska lista po narudžbama | P2b (preduslov editora) |
| Pretplate, pauziranje, promjena paketa | P3 |
| Promjena e-maila | zaseban posao — e-mail je korisničko ime i potvrđena adresa |
| Promjena lozinke | zaseban posao — dio kupaca ulazi preko Googlea i nema lozinku |
| Brisanje naloga (GDPR) | zaseban posao — istorija narudžbi se po zakonu čuva |

**Ne dira se:** checkout, `Order`, korpice, admin panel, autentifikacija, naručivanje bez naloga.

## Odluke korisnika

| Pitanje | Odluka |
|---|---|
| Podjela P2 | P2a nalog, pa P2b editor sa svojim preduslovima |
| Sekcije | Pregled · Lični podaci · Adrese · Narudžbe |
| `/orders` | seli se pod nalog; stara ruta preusmjerava |
| Lični podaci | mijenjaju se **samo ime i telefon** |
| Oblik ljuske | ugnježdene rute, po uzoru na `app/admin/layout.tsx` |

Posljednja odluka ima razlog koji nije estetski. `useSearchParams()` bez `<Suspense>` omotača
prolazi `tsc` i `eslint` čisto, pa obori `npm run build` u Next prerender prolazu. U P1 se to
desilo **dvaput**. Ugnježdene rute tu grešku ne mogu ni proizvesti.

## Model podataka

**Nema novih entiteta.** `User` već nosi `Name` i `Phone`; `Address` postoji u cjelosti.

## Backend — jedan endpoint

```
PUT /account/profile   body { ime, telefon }  →  AuthUserDto
```

`AzurirajProfilCommand(string Ime, string Telefon) : IRequest<AuthUserDto>` u
`Features/Account/`, uz postojeće `AddressCommands`/`AddressQueries`.

Handler uzima korisnika iz `ICurrentUser`, mijenja `Name` i `Phone`, i vraća **isti
`AuthUserDto`** koji `auth/me` već vraća — pa frontend osvježi svoj auth kontekst iz odgovora,
bez drugog poziva.

Validator preslikava pravila registracije (`AuthCommands.cs:27,29`): ime i telefon obavezni.

Uz to dodaje **granicu dužine imena na 160**, koju registracijski validator nema a kolona ima
(`AppDbContext.cs:135`). Bez nje predugo ime prođe validaciju i pukne tek na bazi — klijent
dobija 500 umjesto 400 sa porukom šta je pogrešno. To je postojeći propust koji se usput
zatvara, ne nov zahtjev.

`User.Phone` **nema ograničenje ni u validatoru ni u koloni** (`AppDbContext.cs:133-139`).
P2a mu daje granicu u validatoru; kolonu ne dira, jer migracija radi jednog polja nije srazmjerna
i `nvarchar(max)` ne pravi štetu dok validator stoji ispred njega.

Ne mijenja se `Email`, `PasswordHash`, `Role`, `Address` ni `DeliveryZoneId`. Zadnja dva su
ostatak iz registracije koji adresar zamjenjuje; dirati ih odavde značilo bi dva izvora istine
za istu stvar.

## Frontend

### Ljuska

```
app/(shop)/moj-nalog/
  layout.tsx          straža + sidebar + mobilna traka
  page.tsx            Pregled
  podaci/page.tsx     Lični podaci
  adrese/page.tsx     Adrese
  narudzbe/page.tsx   preseljeni /orders
```

Preslikava `app/admin/layout.tsx`, koji isti posao već radi za admin panel: jedan layout drži
navigaciju, djeca su prave rute.

**Straža ide u layout, jednom.** Neprijavljenom se prikazuje poziv na prijavu, kao što
`/orders` radi danas (`orders/page.tsx:26`) — ne tvrdo preusmjerenje, jer ono gubi odredište.

Navigacija nosi i **Korpica** i **Pretplate** kao vidljivo zaključane stavke. Kupac vidi šta
dolazi, a P2b i P3 dobijaju mjesto da se ukopčaju bez prekrajanja ljuske.

### Sekcije

**Pregled** — mreža pločica koje vode na radnje, ne forma. Četiri žive (izmijeni podatke,
adrese, narudžbe, korpice u ponudi) i dvije zaključane.

**Lični podaci** — dvije kartice. *Lični podaci*: ime i telefon, jedino što se mijenja, sa
helper tekstom uz telefon da je to broj koji vozač zove. *Podaci za prijavu*: e-mail i lozinka,
oboje samo za čitanje, sa jednom rečenicom zašto se ne mijenjaju ovdje. Prikazati ih sivo je
poštenije nego praviti se da ne postoje.

**Adrese** — `accountApi` u `endpoints.ts` plus ekran. Mreža kartica sa `+ Dodaj adresu`,
izmjenom, brisanjem i označavanjem glavne. Postojeća backend pravila koja UI mora poštovati:
najviše 10 adresa po kupcu, tačno jedna glavna (jedinstveni indeks sa filterom), zona mora
postojati.

**Narudžbe** — postojećih 101 liniju seliti **bez redizajna**. `/orders` ostaje kao
preusmjerenje da postojeći linkovi i bookmarkovi ne puknu.

## Testiranje

Backend, u `EPiljara.Application.Tests` (xUnit + SQLite in-memory, postojeći `TestDb` i
`FakeCurrentUser`):

- profil se mijenja i vraćeni DTO nosi nove vrijednosti
- prazno ime i prazan telefon odbijeni, svaki sa svojim kodom
- predugo ime odbijeno
- izmjena dira samo svog korisnika — drugi red u bazi ostaje netaknut
- `Email` i `Role` nepromijenjeni poslije izmjene

Frontend nema test setup. Provjera je `npx tsc --noEmit`, `npm run lint` i **obavezno
`npm run build`**, plus prolazak kroz tok uz pokrenut stek.

## Rizici

| Rizik | Odgovor |
|---|---|
| Adresar nikad nije pozvan iz UI-ja | Prvi poziv je prvi stvarni test tog koda — očekivati da nešto iskoči i tretirati to kao dio posla, ne kao iznenađenje |
| Seljenje `/orders` lomi linkove | Stara ruta ostaje kao preusmjerenje |
| Straža u layoutu propusti neku rutu | Jedna straža u `layout.tsx` pokriva svu djecu; test je pokušaj otvaranja svake sekcije odjavljen |
| Dvije zaključane stavke u navigaciji izgledaju kao greška | Nose jasan natpis da stižu kasnije, ne prazan link |

## Dekompozicija (P1…P7)

| | Potprojekat | Stanje |
|---|---|---|
| P1 | Korpice i sedmični ciklus | **gotovo**, spojeno `6906c26` |
| **P2a** | Moj nalog | **ovaj spec** |
| P2b | Sedmica na narudžbi, pakerska lista po narudžbama, editor sadržaja | čeka |
| P3 | Pretplate | čeka |
| P4 | Marketinška početna | čeka |
| P5 | Analize, Recepti, O nama, Postani partner | čeka |
| P6 | Poeni i referral, promo kodovi, ocjena isporuke | čeka |
| P7 | Višejezičnost | čeka |
