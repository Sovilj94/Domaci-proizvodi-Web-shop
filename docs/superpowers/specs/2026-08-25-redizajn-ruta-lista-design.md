# Redizajn ekrana rute — lista stanica umjesto mape

**Datum:** 2026-08-25
**Status:** dizajn predstavljen, čeka odobrenje
**Prethodi:** `2026-08-21-optimizacija-rute-design.md` (Faza 2c — OSRM, linija rute, serijska navigacija)
**Prvi od tri:** ovaj spec pokriva tab shell i ekran rute. Istorija i profil dobijaju svoje specove.

## Problem

Ekran rute je danas mapa preko cijelog ekrana, sa panelom pri dnu koji pokazuje **samo jednu
stanicu** — trenutnu. Vozač ne vidi šta ga čeka dalje, koliko je stanica ostalo, ni koliko novca
treba da naplati. `RouteViewModel` već održava pune kolekcije `Aktivne` i `Zavrsene`, sortirane po
`Sequence`, ali **nijedna nije vezana u XAML-u** — podaci postoje i nikad ne stignu do očiju.

Uz to, dvije mape se otimaju o istog vozača. Naša Leaflet mapa pokazuje šta je tura; Google Maps
vozi. Vozač se prebacuje između njih, a naša mapa mu ne govori ništa što lista ne bi rekla bolje.

## Cilj

Vozač na jednom ekranu vidi **cijelu turu kao listu**: koja je stanica sljedeća, koje su gotove,
koliko ih je ostalo, i koliko se na svakoj naplaćuje pouzećem. Navigaciju u cjelosti preuzima
Google Maps.

Uspjeh: vozač otvori aplikaciju i bez skrolovanja zna gdje je stao i koliko mu je ostalo; kad
stigne na adresu, sazna to i onda kad gleda u Google Maps.

## Odluke i njihov razlog

| Odluka | Razlog |
|---|---|
| **Naša mapa se briše u cjelosti** | Izbor korisnika. Google Maps preuzima svu navigaciju. Lista nosi „šta je tura" jednako dobro, a mapa je koštala WebView, spakovani Leaflet i most u oba smjera. |
| **Uz brisanje mape ide notifikacija na dolazak** | Posljedica gornje odluke, ne dodatna funkcija. Spec od 21.08. je uklanjanje mape odbio baš zato što bi *„pretvorilo popup iz ekrana pred vozačem u notifikaciju u pozadini"*. Sad je to prihvaćeno — pa notifikacija mora stvarno da postoji, inače automatska detekcija dolaska javlja samo onome ko već gleda u aplikaciju. |
| **Jedna ruta dnevno ostaje** | Izbor korisnika. Mockupi crtaju tri rute dnevno; model, ugovor i ekran ostaju na jednoj. Ništa se ne priprema „za svaki slučaj". |
| **Kilometraža po dionici se ne prikazuje** | Mockup ima „1.1 km" po stanici. Čuva se samo `DuzinaMetara` za cijelu turu; po dionici bi trebalo novo polje i upis pri optimizaciji. Natpis „Cijela tura" je već jednom mijenjan baš zato što je prikaz preostalog bio pogrešan. |
| **Pouzeće je `Order.TotalAmount`, bezuslovno** | Platforma nema pojam načina plaćanja — „Plaćanje pouzećem pri dostavi" je zakucano u `OrderEmailTemplate` i `ViberMessage`. Svaka narudžba se naplaćuje pri dostavi. **Spona koju treba zapamtiti:** ako se ikad doda online plaćanje, ova kartica će tiho pokazivati pogrešan iznos za naplatu. |
| **Kartica ima pet stanja, ne dva** | Mockup crta „sljedeća" i „isporučena". `Zavrsene` prima i `FailedDelivery` i `Cancelled` — komentar u `Primijeni` to izričito kaže. Bez zasebnih stanja neuspjela dostava bi izgledala kao uspješna. |
| **Stringovi po konvenciji projekta, ne po mockupu** | Mockupi su ekavica bez dijakritike („Sledeca", „Podesavanja", „Isporuceno"), uz grešku „Podrazumjevana". Uzima se raspored i vizuelni jezik; tekst se piše ijekavicom sa dijakritikom. |
| **Tabovi Istorija i Profil su u ovom specu prazni** | Da bi se tab bar mogao testirati na uređaju odmah, a da se sadržaj ne gura u isti spec. Oba dobijaju ekran „Uskoro" koji Spec 2 i Spec 3 zamjenjuju. |

---

## Dio A — Tab shell

`AppShell` dobija `TabBar` sa tri taba: **Rute**, **Istorija**, **Profil**. `login` ostaje izvan
tabova kao zaseban `ShellContent`; `stop` ostaje registrovana ruta koja se gura na stek iznad taba.

### Ograničenje koje nosi cio dio

`RoutePage` je keširan `ShellContent` i njegov konstruktor radi **tačno jednom** — postoji jedan
`RouteViewModel` za život aplikacije. Na tome vise pretplate na događaje i zastavice „jednom po
sesiji". Uz to stoji odluka da **`OnDisappearing` teardown-a nema**, jer `Disappearing` puca i kad
se otvori ekran stanice, pa je gašenje praćenja tamo ubijalo foreground servis pri svakom otvaranju
stanice.

**Tabovi to pogoršavaju: prebacivanje taba takođe diže `Disappearing`.** Obje odluke zato ostaju i
postaju nosivije nego prije. Tab shell mora sačuvati keširanje `RoutePage`; ako se stranica po tabu
presozdaje, praćenje i detekcija dolaska se tiho lome — bez greške, bez pada, samo prestanu.

Ovo ide u testove (Dio F), ne samo u komentar.

---

## Dio B — Ekran rute

Odozgo nadolje, mapa nigdje:

```
┌─────────────────────────────┐
│ Utorak, 25. avgust          │  zaglavlje
│ Moja ruta                   │
│ 3 od 7 završeno             │
├─────────────────────────────┤
│   7        15,3 km   27 min │  traka: ukupna tura
│ stanica   cijela tura       │
├─────────────────────────────┤
│  ▸ Navigiraj cijelu rutu    │  primarno dugme
│    Preračunaj od moje lok.  │  sporedno, kad praćenje radi
├─────────────────────────────┤
│  ┌───────────────────────┐  │
│  │ ① Test Koordinate     │  │  lista kartica,
│  │   Reljina 18 [Sljedeća]│ │  skroluje preko
│  │   Borik · 24,50 KM   ›│  │  ostatka ekrana
│  └───────────────────────┘  │
│  ┌───────────────────────┐  │
│  │ ② Jelena Kovačević    │  │
│  └───────────────────────┘  │
└─────────────────────────────┘
   [rute]  istorija   profil
```

**Zaglavlje** nosi datum, naslov i **`Napredak`** („3 od 7 završeno") kao treći red. Napredak ne ide
u traku sa brojevima: traka govori **koliko je tura velika**, napredak **gdje si u njoj**, i kad se
pomiješaju vozač mora da čita dvaput da bi razlikovao „7 stanica" od „7 preostalih".

Nazivi dana i mjeseca se **ispisuju iz ugrađene tabele na srpskom**, ne iz `CurrentCulture` —
vozačev telefon može biti podešen na engleski, isti razlog zbog kojeg `Sazetak` već eksplicitno
postavlja kulturu za decimalni zarez.

**Traka sa brojevima** pokazuje **ukupnu** turu, ne preostalu: `Aktivne.Count + Zavrsene.Count`
stanica, pa `DuzinaMetara` i `TrajanjeSekundi` — oba se ionako odnose na cijelu turu, uključujući
povratak u magacin. Kad je `DuzinaMetara` `null` — a to znači da je optimizacija pala na rezervu po
zonama — piše se **`—`, nikako `0`**. `null` znači „nije izračunato"; `0 km` bi bila tvrdnja, i to
netačna.

**Primarno dugme** je postojeća `NavigacijaSerijaCommand` sa natpisom iz `NavigacijaNatpis`.
**„Preračunaj od moje lokacije"** ostaje sporedno dugme, vidljivo dok `PracenjeRadi`.

**Lista** je `Aktivne` pa `Zavrsene`, obje već sortirane po `Sequence` u `Primijeni`.

**Banneri** (`Error`, nema dozvole, nema interneta) ostaju iznad liste. Žuta `#FFF8E1` ostaje boja
meke napomene, crvena za grešku.

Banner o internetu se **ne briše nego preformuliše**: „Mapa nije dostupna bez interneta" →
**„Nema veze sa internetom — ruta se ne može osvježiti."** Činjenica je i dalje istinita i korisna,
samo joj se promijenila posljedica. `NemaInterneta` i `ProvjeriMrezu` ostaju netaknuti — ta metoda
nosi namjerno uzak filter izuzetka (`NotImplementedInReferenceAssemblyException` po imenu tipa, jer
tip nije javan u referentnom sklopu) i ne treba je dirati.

**Popup na dolasku ostaje nepromijenjen** i dalje prekriva cio ekran.

---

## Dio C — Kartica stanice

| Stanje | Izgled |
|---|---|
| **Sljedeća** — `Aktivne[0]` | deblji okvir u akcentu, broj u akcentnom krugu, oznaka „Sljedeća" |
| **Na čekanju** — ostale iz `Aktivne` | tanak okvir, broj u sivom krugu |
| **Isporučena** — `Delivered` | prigušena, ime precrtano, zelen krug sa kvačicom |
| **Neuspjela** — `FailedDelivery` | crven krug sa `×`, ime precrtano, natpis „Neuspjela dostava" |
| **Otkazana** — `Cancelled` | siv krug sa `×`, natpis „Otkazana" |

Sadržaj: broj, ime kupca, adresa, pa prigušen treći red **„Borik · Pouzeće 24,50 KM"**. Iznos se
formatira sa **eksplicitnom kulturom** — decimalni zarez bez obzira na jezik uređaja, isto pravilo
kao za `Sazetak`.

Dodir na karticu otvara ekran stanice (`OtvoriStanicuCommand`, već postoji).

**Bez koordinata** nije šesto stanje nego **modifikator** preko aktivne kartice: žućkasta podloga i
napomena da navigacija i automatski dolazak tu ne rade. Time postojeći žuti panel „Bez tačne
lokacije — nemaju pin na mapi" nestaje kao zaseban blok; njegov sadržaj se seli na same kartice, a
razlog mu se mijenja — nije više „nema pin", nego „ne može se navigirati i dolazak se ne detektuje".
`BezLokacije` i `ImaBezLokacije` ostaju u ViewModel-u kao izvor za taj modifikator.

---

## Dio D — Notifikacija na dolazak

Danas je jedina notifikacija ona foreground servisa („Tura u toku") sa `NotificationImportance.Low`
— kanal koji namjerno ne iskače. Dolazak je isključivo `PopupVidljiv`, overlay unutar aplikacije.

Dodaje se **drugi kanal**, „Dolazak na stanicu", sa `NotificationImportance.High` da probije preko
Google Mapa. Kad `ArrivalDetector` javi stanicu:

- postavi se notifikacija „Stigli ste: *adresa*"
- dodir na nju vraća u aplikaciju, na tu stanicu
- **popup se otvara kao i do sad**, bez provjere da li je aplikacija u prvom planu

Namjerno se **ne pokušava otkriti da li je aplikacija u prvom planu**. Takva provjera je krhka i
lako pogrešna pri prelasku iz pozadine, a šteta od pogrešnog zaključka je nesimetrična: suvišna
notifikacija je smetnja, izostala notifikacija je propuštena dostava. Kad je aplikacija u prvom
planu, vozač vidi popup, a notifikacija je uz njega bezopasna.

`POST_NOTIFICATIONS` je već deklarisan i traži se pri pokretanju praćenja
(`ZatraziObavjestenjaAsync`) — nema novih dozvola. Ta metoda hvata široko **namjerno**, jer
neuspjelo obavještenje ne smije zaustaviti praćenje; to ostaje tako.

Postavljanje notifikacije je platformski poziv na `async void` lancu. Ide **u `try/catch`** —
review je već našao da je `StartForegroundService` jedini nezaštićen takav poziv, i aplikacija nema
globalni hvatač neuhvaćenih izuzetaka.

---

## Dio E — Pouzeće kroz ugovor

Jedina izmjena backenda u ovom specu.

`RouteStopDto` (`EPiljara.Application/DTOs`) dobija `decimal TotalAmount`, popunjen iz
`Order.TotalAmount` u mapiranju. Isto polje se dodaje u `Models/Dtos.cs` u aplikaciji vozača —
pravilo je da su ti DTO-ovi ručno preslikani **1:1** sa backendom.

Bez migracije: `Order.TotalAmount` već postoji i popunjen je.

---

## Dio F — Testovi

Ulaze:

- razvrstavanje stanica po stanjima, **uključujući `FailedDelivery` i `Cancelled`** — da neuspjela
  ne izgleda kao uspješna
- `Aktivne[0]` je „Sljedeća", i to preživi osvježavanje rute
- traka sa brojevima ispisuje `—` kad je `DuzinaMetara` `null`, ne `0 km`
- pouzeće se formatira sa decimalnim zarezom i pod engleskom kulturom uređaja
- **serijska navigacija i `NavigacijaNatpis`** — review je našao da nema nijedan test i da je već
  jednom otišao pokvaren na uređaj; ovom odlukom postaje kritični put, jer druge navigacije nema
- `RoutePage` preživi prebacivanje tabova bez presozdavanja, i praćenje se ne ugasi

Brišu se, zajedno sa svojim predmetom:

- provjera imena polja mosta ka mapi (`id`, `redni`, `lat`, `lon`, `gotova`)
- sve što testira `OsvjeziMapu`

Pravila test projekta ostaju: cilja `net10.0`, povlači izvore kao `Compile Include/Link`, pa sve
platformsko ide iza `#if ANDROID`; testovi grade `new RouteViewModel(null!, null!, null!)`, pa svaki
nov poziv na parametar konstruktora iz `Primijeni` ili `UcitajAsync` mora trpjeti `null`.

---

## Šta se briše

- `Resources/Raw/map.html`, spakovani `leaflet.js` i `leaflet.css`
- `WebView` iz `RoutePage.xaml` i `Navigating` handler koji presreće `app://stop/<id>`
- most C#→JS: `postaviStanice`, `postaviPoziciju`, `fokusiraj`, i base64-UTF8 kodiranje za č/ć/š/ž
- `OsvjeziMapu` i poziv iz `Primijeni`
- banner „Mapa nije dostupna bez interneta"
- žuti panel „Bez tačne lokacije — nemaju pin na mapi" (sadržaj se seli na kartice)

`DeliveryRouteDto.Geometrija` **ostaje** u oba DTO-a, jer backend je i dalje šalje a pravilo je 1:1.
Aplikacija je prestaje koristiti. Praktična posljedica: `feat: draw the route line and show distance
and duration` se odbacuje na strani aplikacije; kilometraža i trajanje preživljavaju u traci.

## Šta se ne mijenja

Sve invarijante `RouteViewModel`-a ostaju, i nijedna nije posljedica mape:

- `NemaStaDaSePrati = _rutaJosNijeUcitana || TuraGotova || NemaRute` — jedan uslov na tri mjesta
- `IsRefreshing` nikad kao gard protiv ponovnog ulaska
- `Error` i `PopupError` ostaju razdvojeni
- `ZatvoriPopup` **ne** zove `Zaboravi`; grana odbijanja u `PrikaziPopup` **zove** `Zaboravi`
- `ProvjeriDolazak` se poziva sa UI niti
- pozicija vozača ide na server **samo** na izričit dodir „Preračunaj od moje lokacije"

**Dobitak za privatnost:** brisanjem mape prestaje povlačenje pločica sa `tile.openstreetmap.org` sa
telefonovog IP-a. CLAUDE.md to vodi kao otvorenu stavku („zabilježeno, ali neriješeno") — ovim se
zatvara. Nema više ni OSM atribucije koju je politika korišćenja zahtijevala.

## Van obima

**Idu u Spec 2 (istorija):** lista prošlih ruta, agregati („142 ovaj mjesec", „96% uspješnost",
„312 km pređeno"), vrijeme zatvaranja stanice (traži migraciju — `Entity` ima samo `Id`, `Order`
samo `CreatedAt`, pa „Isporučeno u 08:42" danas nema izvor), nov endpoint za istoriju. Otvoreno
pitanje za taj spec: šta pisati u „pređeno" kad dio ruta ima `DuzinaMetara = null`.

**Idu u Spec 3 (profil):** podaci vozača, odjava (`LogoutAsync` je napisan i testiran, samo nema
dugme), stanje dozvola. Traže odluku: **„Na smjeni"** (pojam smjene ne postoji u sistemu) i
**„Kombi 1 · BL-234-AA"** (`Driver` nema ni vozilo ni registraciju — vozilo je danas ugurano u
`Name`, „Mladen (kombi 1)").

**Predlaže se da ispadnu iz svih specova:** birač jezika (aplikacija je jednojezična po dizajnu),
birač podrazumijevane navigacije (implementiran je samo Google Maps), „Prijavi problem" i „Kontakt
dispečer" (nema endpointa ni zapisa o dispečeru). Sve četiri traže podsisteme kojih nema, a nijedna
ne pomaže vozaču da isporuči paket.
