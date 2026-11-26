# Automatske korpice i sezona voća — design

## Zašto

Vlasnik danas svake sedmice ručno pokreće generator (`GenerisiSedmicniSadrzaj`) i objavljuje sadržaj
(`ObjaviSadrzaj`). Kad zaboravi, nova sedmica nema korpica. Tako je danas 2026-11-26: sedmica 48 je
zaključana, za sedmicu 49 nema ništa, pa pretplate preskaču, a kupci nemaju šta naručiti. Vlasnik
kaže: *„hoću da se korpice automatski postavljaju na sajt kad im istekne period, a ne da ja moram
objavljivati svaki put."*

Generator već bira po sezoni (`KalendarKorpica`: mjeseci u sezoni i glavne kulture mjeseca), po
prioritetu (osnova trpeze) i po stvarnoj potražnji zadnjih 12 sedmica. Ne treba ga mijenjati. Treba ga
pokretati automatski i odmah objavljivati.

Za voće vlasnik hoće pristup kao na farmit.rs: voće se nudi samo u sezoni (**maj–oktobar**), a van nje
stranica voćnih korpica kaže da voća nema, kada se vraća, i nudi prijavu na obavještenje. Od Farmita se
uzima pristup, ne tekstovi (CLAUDE.md: „Never copy farmit texts, photos or logo").

## Ciljevi

1. Kad prođe rok naručivanja, sistem u roku od jednog prolaza (≤ 15 min) **sastavi i objavi** sadržaj
   za sljedeću sedmicu, bez admina.
2. Objavljeno ide **odmah na sajt**. Vlasnik pregleda kasnije i, ako treba, ispravi. Kupci ionako sami
   mijenjaju sadržaj (zamjene, veće količine). Bitno je da su „na prvu" unesene glavne namirnice.
3. Voće van sezone: nema generisanja, nema objave, nema kupovine, a postoji jasna poruka sa
   mjesecima sezone, po uzoru na Farmit.
4. „Javi mi kad stigne voće": kupac ostavi e-mail i dobije **jedan** mejl kad se objavi prva
   voćna korpica nove sezone.

## Ne-ciljevi

- Pravila sastavljanja u generatoru se ne mijenjaju (bazen, redoslijed, količine, marža, težina).
- Admin ekran za sadržaj korpica ostaje kakav jeste: ručno generisanje, „Ponovo", izmjena i objava rade
  kao do sad.
- Sezona se ne uvodi za druge vrste (mliječna, suhomesnata, domaćinska).

## Anti-ciljevi (neuspjeh i kad tehnički radi)

- Automatika **pregazi** sadržaj koji je admin već uredio ili objavio.
- Kupac van sezone ipak naruči ili plati voćnu korpicu (pa je niko ne može napuniti).
- Mjeseci sezone ili tekst poruke upisani u kod (pravilo vlasnika iz CLAUDE.md: nema podataka u kodu).
- Kupac koji je ostavio e-mail dobija više od jednog mejla po sezoni ili mejl za koji se nije prijavio.

## Odluke

### 1. Automatsko objavljivanje

- Nova komanda **`AutoObjaviSadrzajeCommand(DateTime? Sada = null)`** (Application, `Features/Korpice`).
  Poziva se u `ZakljucavanjeSadrzajaService`, odmah **poslije** `ZakljucajSadrzajeCommand` u istom
  prolazu. Tako se sedmica prvo zaključa, pa se odmah otvori sljedeća. Pad ovog koraka loguje grešku i ne
  prekida ostatak prolaza (isti `try/catch` obrazac kao kod otkupnih listova).
- **Ciljna sedmica** je `SedmicaHelper.SedmicaZaNarudzbu(...)` (prva sedmica kojoj rok još nije prošao),
  sa rokom iz `UcitajRokAsync`. To je ista sedmica koju naručuju kupci i pretplate, pa automatika
  sama pokriva i današnji slučaj (sedmica 49 bez sadržaja).
- **Koje korpice:** sve `Vrsta == Korpica && IsAvailable`, osim voćnih kad je ciljna sedmica van sezone
  (odluka 2). *Moja odluka:* sve dostupne vrste, ne samo povrće i voće. Ostale su danas
  `IsAvailable = false`, a kad ih vlasnik uključi, logično je da rade isto.
- **Po korpici:**
  - nema sadržaja za sedmicu → `GenerisiSedmicniSadrzajCommand(g, s, korpicaId)`, pa `ObjaviSadrzajCommand`;
  - postoji **Nacrt** sa stavkama → objavi ga takvog kakav jeste (admin ga je počeo, poštuje se njegov rad);
  - postoji **prazan Nacrt** → `Ponovo = true`, generiši, pa objavi;
  - **Objavljen / Zakljucan** → ne dira se;
  - generator vrati 0 stavki (prazan bazen) → ostaje Nacrt, log `Warning`. Postojeći alarm adminu
    (`UpozoriAdminaAsync`, `AlarmSatiPrijeRoka`) javlja da sadržaj nije objavljen.
  - `DomainException` iz objave (npr. obrisan artikal) se uhvati po korpici, loguje i ostavi Nacrt.
    Ostale korpice idu dalje.
- **Idempotentno:** drugi prolaz iste sedmice ne radi ništa. Jedinstveni ključ `(KorpicaId, Godina, Sedmica)`
  već postoji. Ako dva prolaza ipak udare istovremeno (više instanci API-ja), `DbUpdateException` na
  duplikat se tretira kao „već postoji" za tu korpicu.
- **Upozorenja generatora** (`RezultatGenerisanjaDto.Upozorenje`) se loguju (`Information`). U adminu se
  vide kao i do sad, kad vlasnik otvori sadržaj.
- **Log**: `Automatski objavljeno sadržaja korpica za {S}/{G}: {Broj}`, samo kad je `Broj > 0`.

### 2. Sezona voća u podešavanjima

- Dva nova polja na `Podesavanja` (sekcija „Korpice"): **`SezonaVocaOd`** i **`SezonaVocaDo`**,
  `int?` (mjesec 1–12). Oba prazna znače voće cijele godine (dosadašnje ponašanje). Migracija ih dodaje kao
  nullable, bez podrazumijevane vrijednosti u kodu. Vrijednosti maj (5) i oktobar (10) vlasnik unosi u
  `/admin/settings`. Lokalno ih unosi izvođač preko istog ekrana ili API-ja, ne migracijom.
- Validacija: oba prazna ili oba 1–12. Raspon smije prelaziti preko nove godine (npr. 11–3), pa
  „u sezoni" znači `Od <= Do ? Od <= m <= Do : m >= Od || m <= Do`.
- Polja prolaze kroz `KorpiceOpcije`, isto kao ostala pravila (`IzvorPodesavanja` puni opcije po
  zahtjevu; `TestOpcije` u testovima).
- **Mjerodavan mjesec** je mjesec **dana isporuke** ciljne sedmice (`SedmicaHelper.DatumIsporuke`),
  ne današnji datum. Kupac 30. aprila naručuje za isporuku u maju i tada voće već ima.
- Jedna pomoćna funkcija, npr. `SezonaVoca.UTijeku(opcije, mjesec)`, koristi se svuda (automatika,
  DTO, checkout, pretplate, alarm), da pravilo postoji samo na jednom mjestu.

### 3. Voće van sezone: backend

- `KorpicaDto` za voćne korpice dobija **`vanSezone: bool`**, **`sezonaOd: int?`** i **`sezonaDo: int?`**.
  Kad je `vanSezone`, `dostupna` je `false`.
- **Checkout** (oba mjesta koja prave `Order`: `CreateOrder.cs` i `GuestOrderRequests.cs`, vidi CLAUDE.md)
  odbija voćnu korpicu van sezone porukom „Voćne korpice slažemo od {mjeseca} do {mjeseca}." i kodom
  `voce_van_sezone`. Postojeći `korpica_bez_sadrzaja` to uglavnom već hvata, jer automatika van sezone ništa
  ne objavljuje. Provjera sezone stoji ispred njega zbog jasnije poruke i za slučaj da admin ručno objavi
  voće van sezone.
- **Ručno generisanje/objava u adminu** za voće van sezone ostaje dozvoljeno (ne-cilj: admin ekran se ne
  mijenja), ali na sajtu i u checkoutu važi sezona.
- **Pretplate:** pogledati odluku 6 (otvoreno pitanje za vlasnika).
- **Alarm adminu** (`UpozoriAdminaAsync`) preskače voćne korpice van sezone. Inače bi svake zimske
  sedmice javljao „nema sadržaja".

### 4. Voće van sezone: stranica `/korpice/voce`

Po uzoru na **pristup** farmit.rs/gajbice/voce: ista struktura (naslov o završenoj sezoni, mjeseci sezone,
prijava na obavještenje, linkovi dalje). **Tekstovi se ne preuzimaju**, jer CLAUDE.md kaže: „Never copy
farmit texts, photos or logo." Stranica ostaje dostupna (bez preusmjeravanja, zbog SEO-a i linkova).
Kad je `vanSezone`, umjesto izbora veličine prikazuje se blok:

- naslov (sadržaj, primjer naše formulacije): **„Voće se vraća u {od}"**. Oznaka `{godina}`, godina
  posljednje završene sezone, stoji na raspolaganju ako je admin hoće u tekstu (u januaru 2027. je to 2026.);
- tekst (sadržaj, primjer): *„Voće beremo samo kad ga voćnjaci na Manjači daju, pa voćne korpice
  slažemo od {od} do {do}. Do tada je tu korpica povrća."* (mjeseci u odgovarajućem padežu: „od maja",
  „u maju");
- forma **„Javi mi kad stigne voće"** (interfejs): polje „E-mail" i dugme „Javi mi". Poslije slanja:
  „Javićemo ti čim voće stigne.";
- linkovi **„Pogledaj korpice povrća"** (`/korpice/povrce`) i na Špajz (`/spajz`).

Tekstovi su sadržaj, ne interfejs, pa idu u **`BlokoviSadrzaja`** kao nova sekcija (npr.
`VoceVanSezone`), dodata u `SekcijeSadrzaja` (backend) i `SEKCIJE_SADRZAJA` (`types/domain.ts`) i
uređuju se u `/admin/sadrzaj`. Mjesto za mjesec i godinu su oznake `{od}`, `{do}` i `{godina}`, koje
frontend zamjenjuje. Engleski tekst ide u isti red (`NaslovEn`…), kao kod drugih blokova. Ako sekcija
nema redova, prikazuju se samo forma i linkovi (pravilo: „sekcija bez redova ne prikazuje ništa").
Izvođač lokalno upisuje tekst iznad preko `/admin/sadrzaj`.

Nazivi mjeseci (genitiv i lokativ) su dio interfejsa, ne sadržaja, pa ostaju u komponenti.

**Izbor vrste** (`VrstaSegment`): tab „Voće" ostaje klikabilan, a ispod naziva piše „od {mjeseca}".

### 5. „Javi mi kad stigne voće"

- Nova tabela **`PrijaveSezone`**: `Id`, `Email` (normalizovan: trim, mala slova), `TipKorpice`,
  `CreatedAt`, `ObavijestenAt?`. Jedinstven `(Email, TipKorpice)` dok `ObavijestenAt` nije postavljen;
  ponovna prijava istog e-maila je uspjeh bez novog reda.
- **`POST /korpice/{tip}/javi-mi`** `{ email }`: anonimno, FluentValidation za e-mail, ograničenje
  zahtjeva (postojeći rate limiter za javne forme, ako postoji, inače fiksni prozor po IP). Dozvoljeno
  samo za tip koji je trenutno van sezone (`voce`). Odgovor je `204` bez otkrivanja da li je e-mail već
  prijavljen.
- **Slanje:** kad `AutoObjaviSadrzajeCommand` objavi **prvu voćnu korpicu** za sedmicu u sezoni, šalje se
  mejl svim prijavama tog tipa bez `ObavijestenAt`, a `ObavijestenAt` se upisuje po prijavi. Novi metod
  na `IEmailService` (npr. `SendSezonaKrenulaAsync(email, tip)`), napravljen po uzoru na postojeće mejlove
  (`SendPreskocenaIsporukaAsync`), sa linkom na `/korpice/voce`. Pad slanja jednog mejla ne zaustavlja
  ostale, a neposlati se pokušava u sljedećem prolazu.
- Obavještena prijava ostaje u tabeli (`ObavijestenAt`) radi evidencije. Za sljedeću sezonu kupac se
  prijavljuje ponovo.
- Izvođač dopunjava `PRODUCTION.md` (nova tabela sa e-mailovima) ako tamo postoji odjeljak o ličnim
  podacima.

### 6. Pretplate na voće van sezone: sezonska pauza (odluka vlasnika, 2026-11-26)

Danas postoji 1 aktivna pretplata na voćnu korpicu. Bez ove odluke bi `NapraviPretplatneNarudzbe` svake
sedmice do maja preskakao isporuku i slao mejl „preskočena isporuka".

- Za pretplatu na voćnu korpicu čija je ciljna sedmica van sezone, posao **ne pravi narudžbu i ne šalje
  sedmični mejl o preskočenoj isporuci**. Provjera stoji prije provjere sadržaja.
- Kupac dobija **jedan** mejl po sezoni: „Voće se vraća u {mjesec}, pretplata nastavlja sama." Marker na
  `Pretplata` (npr. `ObavijestSezoneGodina`, `int?`) sprječava ponavljanje. To je godina u kojoj se sezona
  vraća. Novi metod na `IEmailService` po uzoru na `SendPreskocenaIsporukaAsync`, kroz `PismoPoruke` (ćirilica).
- Ovo **nije** obična pauza: `Status` ostaje `Aktivna`, `PauzaDo*` se ne dira i ne troši se `MaksPauzaSedmica`.
  Kad sezona krene, prva sedmica u sezoni sa objavljenim sadržajem pravi narudžbu kao i do sad.
- Obaveza (`PreostaloObaveze`) se ne mijenja. Broje se isporuke, a van sezone ih nema.
- `/admin/pretplate` ne broji voćne pretplate van sezone kao „čekaju sadržaj".
- Moja odluka: nova pretplata na voće van sezone se ne zabranjuje posebno, jer stranica van sezone
  ionako nema dugme za kupovinu.

## Ostavljeno izvođaču

- Tačno mjesto `SezonaVoca` helpera i imena DTO polja.
- Da li se `AutoObjaviSadrzajeCommand` testira kroz postojeći test projekat za korpice. Testovi
  **moraju** pokriti: novu sedmicu bez sadržaja → objavljeno; postojeći objavljen sadržaj netaknut;
  Nacrt sa stavkama → objavljen bez izmjene; voće van sezone → ništa; prelaz sezone preko nove godine;
  drugi prolaz → 0; prva voćna objava u sezoni → mejlovi prijavama, drugi prolaz → bez mejlova.
- Zalihe: slobodna zaliha se po CLAUDE.md računa **po sedmici prodaje** (`Stock` minus ono što obećavaju
  narudžbe te sedmice), pa nova sedmica odmah poslije roka počinje sa punom zalihom. Izvođač to potvrđuje u
  `Zalihe.cs`/`KorpicaPodobnost` prije gradnje: ako bazen generatora gleda goli `Stock` umanjen narudžbama
  prethodne sedmice, javlja to prije nego što nastavi.
