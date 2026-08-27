# Više ruta po danu, razvrstanih po naseljima

**Datum:** 2026-08-27
**Status:** dizajn odobren, ide u implementaciju
**Mijenja:** `2026-08-25-redizajn-ruta-lista-design.md`, koji je izričito stao na **jednu rutu dnevno**

## Problem

Vozač danas dobija **jednu** rutu po danu, sa proizvoljnim brojem stanica. Dvije posljedice:

1. **Google Maps prima najviše 10 koordinata po linku.** Ruta sa 18 stanica se ne može otvoriti u
   navigaciji odjednom; višak se tiho odsiječe.
2. **Stanice iz raznih naselja se miješaju** u jednoj listi, pa vozač skače po gradu iako
   optimizacija radi najbolje što može sa zadatim skupom.

Uz to, `AssignRoute` traži postojeću rutu po **(vozač, datum)** i dopunjava je — druga ruta za isti
dan se ne stvara ni kad admin to želi. `DeliveryRoute` nema ni naziv ni zonu, pa se ruta ne može ni
imenovati.

## Cilj

Vozač otvori tab **Rute** i vidi **rute za danas** — po jednu za svako naselje, svaka najviše 10
stanica. Dodirom ulazi u rutu i tamo vidi listu narudžbi (ekran koji danas postoji). Dugme
navigacije u ruti zakači **sve njene stanice** na Google Maps.

Uspjeh: vozač nikad ne dobije rutu koju Google Maps ne može otvoriti u cjelini, i ne miješa naselja.

## Odluke i njihov razlog

| Odluka | Razlog |
|---|---|
| **Sistem dijeli automatski, pri dodjeli** | Izbor korisnika. Zona već stoji na narudžbi (`Order.DeliveryZoneId`), pa nema šta da se upisuje ručno. Admin bira narudžbe i vozača; podjela je posljedica, ne još jedan korak u kojem se griješi. |
| **Najviše 10 stanica po ruti** | Google Maps prima toliko koordinata po linku. Ovo nije estetika nego tvrda granica spoljnog alata — ista ona koju je stari `NavigacijaLink.MaxStanica` već kodirao. |
| **Naselje sa više od 10 se dijeli na „Centar 1", „Centar 2"** | Izbor korisnika. Nijedna narudžba ne ostaje neraspoređena, a vozač vozi jednu pa drugu. Alternativa (višak spojiti sa susjednim naseljem) poništava razlog zbog kojeg se uopšte razvrstava po naseljima. |
| **Bez vremena polaska na kartici** | Izbor korisnika. Mockup pokazuje 09:00/12:30/16:00, ali toga nema u bazi. Rute se prikazuju redom; sat bi tražio migraciju i još jedno polje na admin ekranu, a vozač ionako vozi redom. |
| **Navigacija u ruti hvata sve njene stanice** | Izbor korisnika, i obrće odluku od prije nekoliko sati („otvori samo jednu"). Sa rutom ograničenom na 10, serija je sad bezbjedna — ranije nije bila, jer je ruta mogla imati 18 stanica. |
| **Optimizacija se računa po ruti** | `Optimizacija.PrimijeniAsync` radi nad jednom rutom i njenim stanicama. Sa više ruta poziva se više puta — jednom po ruti, svaka iz magacina. |

---

## Dio A — Model

`DeliveryRoute` dobija:

- `string Naziv` — „Centar", odnosno „Centar 1" / „Centar 2" kad se naselje dijeli. Ovo je ono što
  vozač vidi na kartici.
- `Guid? DeliveryZoneId` + navigacija na zonu — nullable jer ruta sastavljena od narudžbi bez zone
  (moguće, zona je obavezna na narudžbi ali podaci su stariji od tog pravila) mora i dalje postojati.

**Ne uvodi se jedinstveni indeks na `(DriverId, Date)`** — upravo suprotno, više ruta po danu je sad
očekivano stanje. Postojeći indeks, ako ga ima, mora otpasti.

## Dio B — Dodjela se mijenja iz „jedna ruta" u „onoliko koliko treba"

`AssignRouteHandler` danas radi `Where(r => r.DriverId == driver.Id && r.Date == date)` i dopunjava
nađenu rutu. Mijenja se u:

1. Učitaj narudžbe iz `cmd.OrderIds`, sa zonom.
2. Grupiši po `DeliveryZoneId`; grupu bez zone tretiraj kao zasebnu, pod nazivom „Bez zone".
3. Svaku grupu isjeci na komade od **najviše 10** stanica, redom po `Order.CreatedAt` (stabilno i
   predvidivo — bez toga bi dva uzastopna poziva mogla razdvojiti iste narudžbe drugačije).
4. Za svaki komad napravi `DeliveryRoute` sa `Naziv` = naziv zone, uz redni broj **samo kad grupa ima
   više od jednog komada** („Centar" ako je jedan, „Centar 1"/„Centar 2" ako ih je više).
5. Pokreni `Optimizacija.PrimijeniAsync` **po ruti**.

**Ponovna dodjela za isti dan zamjenjuje sve rute tog dana** za tog vozača. Djelimično spajanje sa
zatečenim rutama je zamka: stanica bi mogla ostati u staroj ruti a pojaviti se i u novoj. Zatvorene
stanice (`Delivered`, `FailedDelivery`, `Cancelled`) se **ne premještaju i ne brišu** — ostaju gdje
jesu, jer istorija mora ostati tačna.

## Dio C — Vozački ugovor

Nov endpoint:

```
GET /driver/routes?date=YYYY-MM-DD   →   DeliveryRouteDto[]
```

Vraća sve rute vozača za taj dan, poređane po nazivu. Prazna lista je uredan odgovor, ne greška.

`GET /driver/route?date=` **ostaje** i vraća prvu rutu tog dana — koristi ga popup na dolasku i
detekcija dolaska preko svih stanica. Uklanjanje bi bilo šire od ovog posla.

`DeliveryRouteDto` dobija `Naziv` i `ZonaNaziv`. Kao i uvijek, ista izmjena ide u
`EPiljara.Driver/Models/Dtos.cs` — ti DTO-ovi su ručno preslikani **1:1**.

## Dio D — Ekrani

**Tab „Rute" postaje pregled.** Zaglavlje (datum, „Moje rute danas"), traka sa tri broja (broj ruta,
ukupno stanica, ukupno km), pa kartica po ruti: naziv („Ruta 1 · Centar"), i drugi red
„4 stanice · 9,2 km · ~35 min". Dodir otvara detalj.

**Detalj rute je ekran koji danas postoji** — lista kartica stanica, traka brojeva, banneri, popup.
Prima `routeId` kroz navigaciju i prikazuje samo tu rutu.

**Dugme navigacije u detalju hvata sve stanice te rute**, do 10. Vraća se `NavigacijaLink.Napravi`
nad listom, sa posljednjom stanicom kao odredištem i prethodnima kao međustanicama — kod obrisan
danas, vraća se iz istorije.

## Šta se ne mijenja

- Popup na dolasku, detekcija dolaska, foreground servis i pravila oko njih.
- `Optimizacija.PrimijeniAsync` i njegova pravila o prenumeraciji i `Status = Optimized`.
- Paleta iz mockupa (`Tokens.xaml`) i izgled kartice stanice.
- Istorija i profil.

## Van obima

Vrijeme polaska rute, ručno prepravljanje podjele od strane admina, i spajanje viška iz jednog
naselja u susjedno — sve tri su razmatrane i odbijene u tabeli odluka.
