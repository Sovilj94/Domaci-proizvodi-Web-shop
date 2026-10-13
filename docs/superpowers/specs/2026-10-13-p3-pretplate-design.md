# P3 — Pretplate

**Datum:** 2026-10-13
**Grana:** `pretplate-p3` (iz `main`)
**Mjesto u cjelini:** treći potprojekat (P1…P7); P1, P2a i P2b su spojeni u `main`

## Šta se radi

Kupac se pretplati na korpicu i ona mu stiže svake sedmice bez ijednog novog checkouta.
Pretplatu može **pauzirati**, **promijeniti paket** (vrstu i veličinu) i **otkazati**, sve
sa ekrana `/moj-nalog/pretplate` — stavke koju je P2a postavila u navigaciju kao zaključanu.

Narudžbe pravi pozadinski posao, jednom sedmično, prije roka za tu sedmicu. Posao je jedino
mjesto u sistemu koje pravi narudžbu **bez kupca pred ekranom**.

Cijena: prvih pet isporuka po punoj cijeni, od šeste niža. Popust se zarađuje unazad.

## Zašto

Korpica je od P1 artikal koji se kupuje svake sedmice iznova. Kupac koji je zadovoljan mora
svake sedmice da prođe isti checkout — izabere korpicu, adresu, termin, potvrdi. To je šest
klikova za odluku koju je donio prije mjesec dana.

Operativno je važnije isto što i kod korpice same: **predvidivost**. Pretplata je jedini
podatak u sistemu koji o narednoj sedmici govori unaprijed, a ne tek kad kupci naruče. Nabavka
sa Manjače prestaje da pogađa.

Uzor (farmit.rs) naplati šest mjeseci unaprijed karticom i zato smije dati popust odmah. Ovdje
se plaća **pouzećem** — duži period je samo obećanje, pa je popust koji se da unaprijed poklon
onome ko odustane poslije druge isporuke. Zato se zarađuje: prvih pet po punoj, šesta i dalje
po nižoj. Ko odustane ranije, nije koštao ništa.

## Odluke korisnika (iz P1 speca, ovdje se samo primjenjuju)

| Pitanje | Odluka | Gdje je donesena |
|---|---|---|
| Naplata | pouzećem, bez platne integracije | P1 spec, „Odluke korisnika" |
| Periodi pretplate | puna matrica kao kod farmita | isto |
| Popust | **zarađuje se unazad** — prvih pet isporuka puna cijena, od šeste niža | isto |

## Odluke koje donosim ovdje (nisu bile postavljene)

Sve niže su moje, donesene umjesto pitanja, i svaka nosi cijenu ako je pogrešna.

| # | Odluka | Cijena ako je pogrešna |
|---|---|---|
| O1 | Pretplata nosi **tačno jednu korpicu** (jedan `Product`, količina 1), ne proizvoljnu korpu | Kupac koji hoće dvije korpice sedmično pravi dvije pretplate. Ako ispadne čest slučaj, treba `KolicinaKorpica` na pretplati — aditivna izmjena, bez migracije postojećih redova. |
| O2 | Pretplata je **bez roka trajanja**; „period" iz matrice je samo cjenovni stepenik, ne obaveza | Ako korisnik hoće stvarnu obavezu („6 mjeseci, otkaz uz kaznu"), treba polje `ObavezaDo` i pravilo otkaza. Dodaje se bez diranja postojećeg ponašanja. |
| O3 | Adresa, zona i termin se na pretplatu **kopiraju kao vrijednosti**, ne kao veza na `Address` | Kupac koji izmijeni adresu u adresaru neće time pomjeriti pretplatu — mora je izmijeniti i na pretplati. Suprotno bi značilo da brisanje adrese tiho mijenja gdje ide sljedeća isporuka. Vidi R3. |
| O4 | Pauza se zadaje kao **broj sedmica unaprijed** (1–8), ne kao raspon datuma | Kupac koji hoće pauzu „od 3. do 20. jula" je zadaje u sedmicama. Jeftinije za model i za posao koji broji isporuke; ako zatreba raspon, polje `PauzaDo` već postoji kao datum. |
| O5 | Pozadinski posao pravi narudžbu **48h prije roka te sedmice**, ne odmah po objavi sadržaja | Kupac koji pauzira u posljednji čas (poslije tog trenutka, prije roka) dobija narudžbu koju mora otkazati ručno. Suprotno — praviti u trenutku roka — ne ostavlja nikome vremena da reaguje na grešku. |
| O6 | Ako sedmični sadržaj za tu korpicu **ne postoji ili nije objavljen**, pretplatna narudžba se **ne pravi** i pretplata se preskače za tu sedmicu, uz zapis | Kupac ostane bez isporuke u sedmici koju admin nije pripremio. Alternativa — napraviti narudžbu bez sadržaja — pakerskoj listi daje kutiju koju niko ne zna napuniti. Vidi R2. |
| O7 | Cijena se **ne zamrzava** pri pretplati: svaka narudžba uzima tekuću cijenu korpice, umanjenu za popust ako je zaslužen | Poskupljenje korpice odmah pogađa pretplatnike. Zamrznuta cijena bi značila da pretplata iz 2026. vrijedi 2028, što nijedna nabavka ne izdrži. Kupac poskupljenje vidi na ekranu pretplate i u mejlu narudžbe. |
| O8 | Pretplata nije dostupna gostu — traži nalog | Gost nema gdje da vidi ni pauzira pretplatu, a `GuestOrderRequest` živi 15 minuta. Bez naloga pretplata nema vlasnika. |

## Model podataka

### Novi enum (`Domain/Enums/Enums.cs`)

```csharp
public enum StatusPretplate { Aktivna, Pauzirana, Otkazana }
```

Prelazi idu u oba smjera osim iz `Otkazana`, koja je završna — isti obrazac kao
`SedmicniSadrzaj.Prelazi` i `Order.Transitions`.

### Nova tabela: `Pretplata`

| Polje | Tip | Napomena |
|---|---|---|
| `Id` | `Guid` | |
| `UserId` | `Guid` | vlasnik; FK `Restrict` kao svuda |
| `ProductId` | `Guid` | korpica (`Vrsta == Korpica`); FK `Restrict` |
| `Status` | `StatusPretplate` | |
| `DeliveryZoneId` | `Guid` | kopija izbora, ne veza na adresar (O3) |
| `DeliverySlotId` | `Guid` | isto |
| `DeliveryAddress` | `string(400)` | isto |
| `Latitude`, `Longitude` | `double?` | isto; nullable end-to-end kao svuda |
| `Napomena` | `string(1000)?` | ide na svaku narudžbu |
| `BrojIsporuka` | `int` | koliko je **isporučenih** narudžbi ova pretplata dala; osnov popusta |
| `PauzaDoGodina`, `PauzaDoSedmica` | `int?` | prva sedmica u kojoj pretplata ponovo radi; `null` kad nije pauzirana |
| `PoslednjaGodina`, `PoslednjaSedmica` | `int?` | sedmica za koju je posao posljednji put napravio narudžbu — ključ idempotentnosti |
| `CreatedAt`, `OtkazanaU` | `DateTime`, `DateTime?` | |

**Jedinstveni indeks** `(UserId, ProductId)` nad redovima koji nisu otkazani: isti kupac ne
može imati dvije aktivne pretplate na istu korpicu. Otkazane se ne broje — kupac se smije
vratiti na isti paket.

### `Order` dobija vezu na pretplatu

```csharp
public Guid? PretplataId { get; set; }
```

`null` za svaku narudžbu koju je napravio čovjek. Nije samo metapodatak: po njemu
`BrojIsporuka` broji isporuke, i po njemu ekran pretplate pokazuje istoriju.

### Cjenovna matrica

Ne tabela — **funkcija**, `PretplatnaCijena`, jedino mjesto koje zna pravilo:

```
cijena(korpica, brojIsporuka) =
    brojIsporuka < PragPopusta ? korpica.Price
                               : zaokruzi(korpica.Price * (1 - Popust), 2)
```

`PragPopusta` (5) i `Popust` (0.10) stoje u `PretplateOpcije`, istoj sekciji konfiguracije
kao `KorpiceOpcije`. Prag je **broj već isporučenih**, pa je šesta isporuka prva jeftinija —
to je doslovno „zarađuje se unazad".

## Kako narudžba nastaje

Pozadinski posao `PretplatneNarudzbeService` (isti obrazac kao
`ZakljucavanjeSadrzajaService`), svakih 15 minuta, šalje `NapraviPretplatneNarudzbeCommand`.

Komanda za svaku pretplatu:

1. **Preskoči** ako nije `Aktivna`.
2. Izračunaj ciljnu sedmicu — `SedmicaHelper.SedmicaZaNarudzbu`, isti izvor istine kao
   checkout. Nema drugog računanja sedmice u sistemu i neće ga biti.
3. **Preskoči** ako je `(Poslednja*) == ciljna` — narudžba za tu sedmicu već postoji.
   Idempotentnost je obavezna: posao se vrti svakih 15 minuta i mora smjeti da padne u sredini.
4. **Preskoči** ako je pauza aktivna (`ciljna < PauzaDo`), i očisti pauzu kad istekne.
5. **Preskoči** ako do roka te sedmice ima više od `SatiUnaprijed` (48) — rano je (O5).
6. **Preskoči i zapiši** ako sadržaj te sedmice za tu korpicu nije `Objavljen` ili
   `Zakljucan` (O6).
7. Napravi `Order` — kroz **isti put kao checkout**, ne kopijom. Vidi „Tri mjesta" ispod.
8. Upiši `Poslednja* = ciljna`, u **istom** `SaveChangesAsync` kao narudžbu.

Korak 8 u istoj transakciji sa korakom 7 je jedina odbrana od duple narudžbe; razdvojeno bi
pad između njih dao kupcu dvije korpice i dvije naplate pouzećem.

### Tri mjesta koja prave `Order`

`CLAUDE.md` traži da se prije svake invarijante na nivou narudžbe pokrene
`grep -rn "new Order" backend/src --include=*.cs` i svjesno odluči o svakom pogotku. Odluka
za P3, unaprijed, jer je P2b ovo naučio na teži način:

| Mjesto | Odluka |
|---|---|
| `CreateOrder.cs` | Pretplata ide **kroz njega**, kao `CreateOrderCommand` sa `PretplataId`. Snapshot korpice, popis sedmice, skidanje zaliha i mejl time ostaju jedan kod. |
| `GuestOrderRequests.cs` | Ne dira se — gost nema pretplatu (O8). |
| `DbSeeder.cs` | Dobija jednu aktivnu pretplatu za `mujo@example.com`, da ekran ima šta da pokaže na praznoj bazi. |

Da bi pretplata mogla kroz `CreateOrderHandler`, handler mora prestati da čita `ICurrentUser`
kao jedini izvor vlasnika — posao nema prijavljenog korisnika. `CustomerId` postaje ulaz
komande sa dosadašnjim ponašanjem kao rezervom. To je jedina izmjena postojećeg puta.

## Endpointi

Svi pod `AccountController`, svi `[Authorize]`, isti oblik kao `/account/korpice`:

```
GET    /account/pretplate                 moje pretplate + sljedeća isporuka i cijena
POST   /account/pretplate                 nova (korpica, adresa, zona, termin, napomena)
PUT    /account/pretplate/{id}            promjena paketa / adrese / termina / napomene
POST   /account/pretplate/{id}/pauza      tijelo: { sedmica: 1..8 }
POST   /account/pretplate/{id}/nastavi    gasi pauzu odmah
DELETE /account/pretplate/{id}            otkazuje (ne briše red — istorija narudžbi visi o njemu)
```

`DELETE` vraća 204 bez tijela, kao `DELETE /account/addresses/{id}` — jedino drugo takvo
mjesto u API-ju.

Admin dobija `GET /admin/pretplate` (spisak, filtriran po statusu) — nabavka mora moći da
vidi koliko je korpica obećano prije nego što narudžbe uopšte nastanu.

## Frontend

`/moj-nalog/pretplate`, po uzoru na `/moj-nalog/adrese`:

- kartica po pretplati: paket, sljedeća isporuka (sedmica i datum), cijena sa oznakom
  „puna" ili „pretplatnička", broj dosadašnjih isporuka, status
- dugmad: Pauziraj (biranje broja sedmica), Nastavi, Izmijeni, Otkaži (inline potvrda, ne
  `window.confirm` — vidi `adrese/page.tsx`)
- „Koliko još do niže cijene" kao traka, jer je to jedini razlog da kupac ostane
- prazan ekran vodi na `/korpice`

Navigaciona stavka prestaje da bude `uskoro` (`components/nalog/nalog-nav.tsx`).

Nova pretplata se pravi sa stranice korpice (`/korpice/[slug]`), dugmetom pored „Dodaj u
korpu" — ne kroz checkout. Checkout ostaje jednokratna kupovina i ne zna za pretplate.

## Testiranje

Prioritet je na onome što se ne vidi na ekranu:

| Šta se dokazuje | Zašto baš to |
|---|---|
| Dva uzastopna prolaza posla daju **jednu** narudžbu | Idempotentnost je jedina stvar koja stoji između kupca i duple naplate |
| Pad poslije `Order.Add` a prije upisa `Poslednja*` ne daje duplu narudžbu | Ista stvar, ali kroz transakciju |
| Šesta isporuka je prva po nižoj cijeni; peta je po punoj | Granica `BrojIsporuka >= PragPopusta` je lako pomjeriti za jedan |
| Pauza od N sedmica preskače tačno N sedmica i vrati se sama | Pauza koja se ne vrati je tiha otkazana pretplata |
| Sadržaj koji nije objavljen → nema narudžbe, pretplata ostaje aktivna | O6 |
| Otkazana pretplata ne pravi ništa, ni poslije | Završno stanje mora biti završno |
| `BrojIsporuka` raste na `Delivered`, ne na nastanak narudžbe | Popust se zarađuje isporukama, ne narudžbama |

## Rizici

**R1 — Posao pravi narudžbe bez ikoga pred ekranom.** Greška u uslovu ne pravi grešku na
ekranu nego kutije na stolu i račune pouzećem. Zato je idempotentnost u istoj transakciji, a
ne „provjeri pa upiši", i zato prvi test koji se piše nije sretan slučaj nego dupli prolaz.

**R2 — Sadržaj sedmice kasni, pretplatnik ostaje bez isporuke.** O6 bira tišinu umjesto
prazne kutije. Ublažava se admin ekranom koji pokazuje koliko pretplata čeka sadržaj za
narednu sedmicu; alarm (mejl adminu) nije u P3.

**R3 — Adresa na pretplati se razilazi sa adresarom.** Svjesno (O3). Ekran pokazuje adresu
pretplate doslovno, da razlika bude vidljiva, a ne skrivena iza imena adrese.

**R4 — `BrojIsporuka` i stvarni broj `Order`-a sa `PretplataId` se mogu raziću** ako se
status narudžbe mijenja ručno kroz admin. Brojač je izveden podatak; ako iko ikad posumnja,
`select count(*) ... where Status = Delivered` je istina. Ne pravi se konzistentnim kroz
okidače — to bi značilo logiku u bazi, koje u ovom projektu nema nigdje.

## Šta P3 NE pokriva

| Izostavljeno | Gdje ide |
|---|---|
| Plaćanje karticom, pretplata unaprijed | nikad — pouzećem je odluka |
| Više korpica u jednoj pretplati | O1, aditivno kasnije |
| Pretplata na obične artikle | nije traženo; model to dozvoljava (`ProductId`), ekran ne |
| Promjena termina po pojedinoj isporuci | kupac mijenja pretplatu ili otkaže tu narudžbu |
| Poeni i referral za pretplatnike | P6 |
| Mejl „tvoja korpica je naručena" posebnog oblika | koristi se postojeća potvrda narudžbe |

## Dekompozicija (P1…P7)

| | Potprojekat | Stanje |
|---|---|---|
| P1 | Korpice i sedmični ciklus | **gotovo**, spojeno `6906c26` |
| P2a | Moj nalog | **gotovo**, spojeno `45595c7` |
| P2b | Sedmica na narudžbi, pakerska lista, editor sadržaja | **gotovo** |
| P2c | Birač adrese na mapi u adresaru | **gotovo**, spojeno `c731eba` |
| **P3** | Pretplate | **ovaj spec** |
| P4 | Marketinška početna | čeka |
| P5 | Analize, Recepti, O nama, Postani partner | čeka |
| P6 | Poeni i referral, promo kodovi, ocjena isporuke | čeka |
| P7 | Višejezičnost | čeka |
