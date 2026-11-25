# Narudžbe bez odobravanja, zaključavanje na roku i nabavka po gazdinstvima — dizajn

Datum: 2026-11-25 · Status: nacrt, čeka odobrenje vlasnika

## Zašto

Danas svaka narudžba stiže kao **Na čekanju** (`Pending`) i admin je mora ručno potvrditi
(`GET /admin/korpice/potvrdjivanje`, ekran Narudžbe). Pozadinski posao
(`ZakljucavanjeSadrzajaService`, svakih 15 min) na roku naručivanja zaključava sadržaje korpica i
**otkazuje sve nepotvrđene narudžbe** (`OdbijNepotvrdjeneCommand`). Nabavka (`/admin/nabavka`)
broji samo potvrđene. Posljedica: ako se admin ne sjeti da potvrdi, kupac ostaje bez korpice.

Vlasnik (2026-11-24): *„Ne bih išao s tim da neko mora da odobri narudžbu. Sve narudžbe se
automatski prihvataju, ali su otključane za izmjenu. Kada prođe vrijeme predviđeno za izmjene,
narudžbe se automatski zaključavaju i roba ide da se nabavlja."*

Uz to traži:
- jednostavan **spisak svih narudžbi**,
- kad se narudžbe zaključaju, **spisak namirnica koje treba nabaviti**,
- nabavku **po dobavljaču**: zna se šta koje gazdinstvo proizvodi (proizvodi se unose po
  dobavljaču), pa se zna po šta se ide kod koga,
- **spiskove za štampu** koje vozač nosi u nabavku,
- **dokument koji dobavljač potpisuje** da je predao, a mi primili robu. Dobavljači su uglavnom
  individualni poljoprivrednici (registrovana gazdinstva), pa je to **otkupni list**.

## Ciljevi

1. Nova narudžba je odmah **prihvaćena**. Nema ručnog potvrđivanja i nema automatskog otkazivanja.
2. Narudžba je **otvorena** do roka naručivanja svoje sedmice (isti rok kao danas:
   `SedmicaHelper.UcitajRokAsync`, trenutno 48 h prije isporuke). Na roku je sistem sam
   **zaključava**.
3. Admin ima jedan spisak narudžbi na kojem se jasno vidi šta je otvoreno, a šta zaključano.
4. Nabavka za sedmicu se prikazuje **po gazdinstvu**: za svako gazdinstvo artikli, količine,
   nabavna cijena i iznos. Korpice su razložene na stvarni sadržaj narudžbi, a uz to idu artikli
   iz Špajza.
5. Za svako gazdinstvo postoji **otkupni list** u PDF-u za štampu: podaci gazdinstva, broj
   dokumenta, datum, tabela (artikal, jedinica, naručeno, **primljeno** prazno za upis rukom,
   cijena, iznos) i potpisi „Prodavac" i „Otkupljivač".
6. Poslije nabavke admin može unijeti **stvarno primljene količine** po otkupnom listu.

## Nije cilj (sada)

- Obračun poreza i doprinosa na otkupni list i plaćanje gazdinstvima. Obrazac je takav da ga
  knjigovođa može provjeriti i dopuniti, ali sistem ništa ne računa.
- Više dobavljača za isti proizvod. Proizvod pripada tačno jednom gazdinstvu
  (`Product.ProducerId`, već tako).
- Automatska reakcija na manjak („gazdinstvo dalo manje"): ne dira se ni stanje ni kupci, admin
  to rješava ručno kao i do sada.
- Izmjena artikala iz Špajza od strane kupca (vidi „Otvoreno pitanje").

## Anti-ciljevi (neuspjeh i kad tehnički radi)

- Narudžba koja **tiho nestane**: nijedna narudžba se više ne otkazuje sama.
- Spisak nabavke koji se razlikuje od onoga što će se pakovati. Nabavka i pakovanje čitaju iste
  redove (`StavkaKorpiceNarudzbe` + `OrderItem` iz Špajza).
- Kupac koji poslije roka i dalje vidi „možeš mijenjati".
- JMBG nosioca gazdinstva vidljiv bilo gdje van admina.
- Izmišljeni podaci: novi podaci gazdinstva se unose kroz admin, nikad iz koda ili migracije
  (pravilo vlasnika „no data in code").

## Kako (odluke)

Oznaka **[V]** znači da je to rekao ili potvrdio vlasnik. **[moja odluka]** znači da je to moj
izbor, koji vlasnik može oboriti.

### 1. Prihvatanje — narudžba se pravi kao `Confirmed` [V]

- Oba mjesta koja prave `Order` (`CreateOrder.cs` i `GuestOrderRequests.cs`, vidi CLAUDE.md
  „Two places construct an Order") postavljaju `Status = Confirmed`. Pretplate idu kroz
  `CreateOrder`, pa su pokrivene.
- `Pending` ostaje u enumu samo radi istorije. U tranzicijama ostaje `Pending → Confirmed/Cancelled`,
  da stari redovi ne zaglave. **[moja odluka]**
- Postojećih 5 `Pending` narudžbi: pozadinski posao ih pri prvom prolazu prebacuje u `Confirmed`
  (ne migracija sa podacima). **[moja odluka]**
- Mejl: kupac dobija samo postojeću potvrdu narudžbe. Poseban mejl „Narudžba je potvrđena"
  (`ObavjestenjaPravila`, `OrderStatus.Confirmed`) se za automatsko prihvatanje **ne šalje**,
  inače bi stizala dva mejla odjednom. **[moja odluka]**
- Briše se: `OdbijNepotvrdjeneCommand` (i njegov poziv u `ZakljucavanjeSadrzajaService`), endpoint
  i upit `potvrdjivanje`, `NaCekanjuNapomena` na nabavci, kartica „Na čekanju / čeka potvrdu" na
  početnom ekranu admina i zadatak „naCekanju" u `AdminZadaci`. Stanje se i dalje provjerava na
  checkoutu (`Zalihe`, Špajz stock), kao i do sada.

### 2. Zaključavanje — vremenska oznaka, ne novi status [moja odluka]

- Novo polje `Order.ZakljucanaU` (`DateTime?`, UTC). `null` znači otvorena.
- `ZakljucavanjeSadrzajaService` u istom prolazu u kojem zaključava sadržaje korpica postavlja
  `ZakljucanaU = sada` svim neotkazanim narudžbama čiji je rok prošao (isti izraz kao danas u
  `OdbijNepotvrdjene`: `DatumIsporuke(...) - cutoff`). Idempotentno: dira samo one sa `null`.
- Zašto ne novi status „Zaključena": status mašinu, vozačku aplikaciju (zasebni repo), mejlove i
  tablu narudžbi bi to sve pomjerilo, a zaključavanje nije korak isporuke nego činjenica „rok je
  prošao". `Confirmed → Packing → …` ostaje netaknuto.
- Pravilo „smije li se mijenjati" (`MojaKorpicaPravila.StatusiKojiDozvoljavaju`) postaje:
  status `Confirmed` **i** `ZakljucanaU == null`. Zaključavanje se ne oslanja samo na posao: ako
  posao kasni do 15 min, sam rok (već postojeća provjera u editoru) i dalje odbija izmjenu.
- DTO dobija `zakljucana: boolean` i `otvorenaDo` (rok), da frontend ne računa sam.
- Admin i poslije zaključavanja može mijenjati i otkazivati narudžbe kao i danas. Takva izmjena
  označava otkupni list te sedmice kao zastario (vidi 5).

### 3. Spisak narudžbi (admin) [V], izgled [moja odluka]

- `/admin/orders` zadržava tablu i listu, uz izbor sedmice, ali umjesto kolone/filtera „Na
  čekanju" ima **„Otvorene"** (do roka) i **„Zaključene"**. Na vrhu je traka: „Sedmica 48 —
  otvoreno do utorka u 16:00 · 37 narudžbi" odnosno „Zaključeno 24. 11. u 16:00 · 41 narudžba ·
  Nabavka →".
- Dugme za potvrđivanje i prelaz `Pending → Confirmed` nestaju iz kartice narudžbe.

### 4. Nabavka po gazdinstvima [V]

- Backend: novi upit `GET /admin/nabavka?godina=&sedmica=` vraća grupe po `Producer`, sa redom
  po artiklu: `productId`, naziv, jedinica, količina (zbir korpica: `Kolicina × OrderItem.Quantity`,
  plus Špajz `OrderItem.Quantity`), `nabavnaCijena?` i `iznos?`. Broje se sve neotkazane
  narudžbe te sedmice. Prije roka odgovor nosi `zakljucena: false` i ekran piše „predračun —
  količine se još mijenjaju" (isti obrazac kao postojeći PDF). Logika sabiranja se **izvlači**
  iz `GetPakerskaListaHandler` u zajedničku funkciju, da pakovanje i nabavka ne mogu da se
  raziđu. **[moja odluka]**
- Artikal bez nabavne cijene: red se prikazuje, a cijena i iznos ostaju prazni (u PDF-u prazno
  polje za upis rukom). Danas je to 15 artikala Porodice Kovačević.
- Frontend: `/admin/nabavka` prikazuje kartice po gazdinstvu (ime, selo, telefon, zbir), sa
  dugmadima „Otkupni list (PDF)" po gazdinstvu i „Svi otkupni listovi (PDF)" za jedan dokument
  sa prelomom strane po gazdinstvu. Postojeći prikaz po vrsti robe ostaje kao druga kartica
  („Po vrsti robe"), jer služi i za pakovanje. **[moja odluka]**
- „Koliko gazdinstvo može dati" je postojeće `Product.Stock`: proizvod ima jedno gazdinstvo, pa
  je stanje proizvoda isto što i kapacitet gazdinstva za njega. Novo polje se ne uvodi. **[moja odluka]**

### 5. Otkupni list [V: dokument koji se potpisuje], oblik [moja odluka]

- **Gazdinstvo dobija podatke za otkupni list** (sve opciono, unosi admin na
  `/admin/producers`): `NosilacIme`, `Bpg` (broj registrovanog poljoprivrednog gazdinstva),
  `Jmbg`, `Adresa`, `ZiroRacun`, `Banka`. JMBG i žiro račun idu samo u admin DTO, nikad u javni
  `ProducerDto`.
- **Entitet `OtkupniList`**: `Broj` (jedinstven, `OL-2026-0001`, brojač po godini), `ProducerId`,
  `Godina`, `Sedmica`, `Datum` (dan nabavke, podrazumijevano dan zaključavanja), `Zastario`
  (bool), `Napomena?` i stavke `OtkupniListStavka`: snimak naziva, jedinice, `Naruceno`,
  `Cijena?` i `Primljeno?`. Snimak, a ne referenca, iz istog razloga iz kojeg `OrderItem`
  snima cijenu: kasnija izmjena proizvoda ne smije promijeniti potpisan dokument.
- **Pravi se automatski pri zaključavanju sedmice**, jedan po gazdinstvu koje ima bar jedan red.
  Posao je idempotentan (jedinstveno `(ProducerId, Godina, Sedmica)`).
- Ako admin poslije zaključavanja izmijeni ili otkaže narudžbu, otkupni list te sedmice dobija
  `Zastario = true` i ekran nudi „Osvježi količine". Osvježavanje je dozvoljeno samo dok nijedna
  stavka nema unesenu primljenu količinu. Poslije toga se razlika samo prikazuje.
- **PDF** se pravi na frontendu sa `pdfmake` (već u projektu, `lib/admin/nabavka-pdf.ts`):
  zaglavlje „OTKUPNI LIST br. …", podaci otkupljivača (iz `SadrzajSajta`/kontakta, pa nema novih
  konstanti u kodu), podaci prodavca (gazdinstva), tabela, zbir i dva polja za potpis sa datumom.
  Polja koja gazdinstvo nema popunjena štampaju se kao prazne linije za upis rukom.
- **Unos primljenog**: na otkupnom listu admin upisuje `Primljeno` po stavci i vidi razliku i
  stvarni iznos. To samo evidentira prijem. Ne mijenja stanje, narudžbe ni kupce.

## Ograničenja i pravila projekta koja važe

- Bez seed podataka. Nova polja gazdinstva su prazna dok ih admin ne unese.
- Migracija samo mijenja šemu (nova polja i tabele), bez umetanja ili mijenjanja redova.
- Novi validatori počinju sa `RuleFor(x => x.Input).NotNull()`. Greške se šalju sa konkretnim kodom.
- Nullable polja su na frontendu `?:` (API izostavlja `null`).
- Svaki novi ekran sa `useSearchParams` ide u `<Suspense>`. Frontend se provjerava sa
  `npm run build`.
- Prije izmjene bilo čega oko pravljenja narudžbe: `grep -rn "new Order" backend/src` i odluka
  za svako mjesto.
- Testovi na backendu (xUnit + SQLite): pravljenje kao `Confirmed` (oba puta), zaključavanje
  (rok, idempotentnost, otkazane se ne diraju), editor odbija zaključanu, nabavka po gazdinstvu
  (korpica × količina + Špajz, bez otkazanih), otkupni list (jedan po gazdinstvu, broj,
  `Zastario` poslije admin izmjene, osvježavanje blokirano poslije unosa primljenog).

## Prepušteno izvođaču

Raspored i izgled ekrana u okviru postojećeg dizajn sistema, imena DTO polja, tačan izgled PDF-a,
redoslijed gazdinstava (po imenu), da li „Svi otkupni listovi" ide kao jedan PDF ili više njih.

## Otvoreno pitanje za vlasnika

**Šta kupac smije mijenjati dok je narudžba otvorena?** Danas samo sadržaj korpice. U ovom
dizajnu to ostaje tako, uz promjenu da se izmjena zatvara zaključavanjem, a ne statusom.
Proširenje (mijenjanje količina i brisanje ili dodavanje artikala iz Špajza, otkazivanje cijele
narudžbe do roka) je zaseban komad posla (stanje, poeni, promo kod, ponovni obračun dostave) i
ide kao sljedeća faza ako vlasnik to želi.
