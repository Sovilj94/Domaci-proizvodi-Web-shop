# Korpica po budžetu — design

## Zašto

Korpica danas pravi „mrvice": bijeli luk 0,1 kg, batat 0,3 kg (staje u jedan krompir). Dva izvora:

1. **Generator** prvo stavi *pola* zadate količine svake kulture (0,3 → 0,15 → 0,1 kg).
2. **Zamjena** je „jedan za jedan po vrijednosti": izbaciš paprike od 1,50 KM, dobiješ 1,50 KM batata = 0,3 kg.

Kupac uz to ne može da kaže „ovo mi ne treba, daj mi više krompira". Korpica je tačan spisak, a ne budžet.

## Cilj

Korpica je **budžet**: vrijednost 95–100 % cilja i ispoštovana marža. Unutar tog okvira kupac slobodno
izbacuje kulture, dodaje nove i povećava postojeće. Nijedna namirnica nije ispod svoje smislene količine.

## Ne-ciljevi

- Ne mijenja se cijena korpice, cilj vrijednosti (`CiljnaVrijednost`), ni pravilo 95–100 %.
- Admin editor kupčeve korpice (`/admin/orders` → „Uredi sadržaj") ostaje kakav jeste.
- Kupcu se i dalje ne prikazuju pojedinačne cijene (samo količine i ukupna traka).

## Anti-ciljevi (neuspjeh i kad tehnički radi)

- Korpica ispod marže — marža je jedino tvrdo pravilo, sve ostalo popušta pred njom.
- Bilo koja količina ispod najmanje za tu namirnicu (mrvice).
- Kupac može upisati proizvoljnu količinu po cijeni manje (server uvijek sam čita cijene i provjerava budžet).

## Odluke

### 1. Najmanja količina po namirnici

- Novo polje na `Product`: **`NajmanjeUKorpici`** (decimal?, u jedinici artikla). Admin ga unosi u
  Postavkama korpica, kolona pored zadatih količina.
- Kad je prazno: **zadata količina za malu korpicu** (`KolicinaMala`); kad ni nje nema: korak jedinice
  (1 kom / 0,1 kg).
- Važi svuda gdje se namirnica stavlja u korpicu: generator, kupčevo dodavanje i smanjivanje, „popuni umjesto
  mene". Ako ne staje u najmanjoj količini, namirnica se **izostavlja**, ne smanjuje.
- Izuzetak: red iz objavljenog šablona koji kupac nije dirao (zatečeno stanje se ne kvari).

### 2. Kupčev editor: budžet umjesto zamjene

Ekran „Moja korpica" po stavci:

- **−** / **+**: smanji ili povećaj za jedan korak. Korak je **0,5 kg** za artikle na kg/l (ali ne veći
  od najmanje količine, npr. bijeli luk 0,3 → korak 0,3), **1 kom** za komade. Ispod najmanje se ne ide —
  sljedeće „−" je **Izbaci**.
- **Dodaj kulturu**: lista ponude (kalendar te vrste korpice + predložene zamjene, kao danas). Dodaje se u
  najmanjoj količini.
- **Traka budžeta**: „Iskorišteno 92 % korpice". Zelena između 95 i 100 %, inače poruka „dodaj još" ili
  „previše — smanji nešto".
- **Sačuvaj** šalje cijeli sadržaj; aktivno samo kad je traka u 95–100 % i ima dovoljno kultura.
- **Popuni umjesto mene**: dopuni ostatak budžeta sam — prvo povećava osnovu (prioritet 0), pa dodaje
  kulture redom važnosti — dok ne uđe u 95–100 % bez kršenja marže.
- **Vrati kako je bilo** ostaje.
- Proizvođač (`od: …`) ostaje uz svaku stavku i ponudu.

Izmjene su lokalne dok kupac ne klikne „Sačuvaj" (jedan PUT), da traka može ići ispod 95 % dok slaže.

Broj kultura nije tačan: ostaje samo donji prag `MinimalanBrojArtikala` iz postavki.

### 3. Server (`PUT /account/korpice/{orderItemId}`)

Prima **`stavke: [{ productId, kolicina }]`** umjesto samo `productIds`. Cijene i dalje čita sam server
(snimak reda → šablon → katalog), pa količina ne otvara rupu „10 kg po cijeni 1". Pravila redom:

vlasnik → status → duplikati → rok → podobnost (`KorpicaPodobnost.ZaKorpicu`) → količina (≥ najmanje,
višekratnik koraka zaokruživanja) → budžet 95–100 % → broj kultura → **marža** (nabavni trošak ≤ nabavni
budžet `cijena × (1 − MinimalnaMarza)`, ili ne veći nego prije izmjene) → zalihe.

Novi kod greške: `kolicina_premala`. Postojeći endpointi zamjene (`/zamijeni`) ostaju za kompatibilnost,
ali ih nov ekran ne koristi; „Popuni umjesto mene" je novi `POST /account/korpice/{id}/popuni`.

### 4. Generator

1. **Osnova prva**: kulture sa prioritetom 0 ulaze prve i to u **punoj zadatoj količini**. Admin u
   Postavkama korpica postavlja prioritet 0 samo za krompir, mrkvu, luk i papriku (i njihove vrste);
   ostalo na 1 ili 2. To je podatak, ne kod.
2. Ostale kulture redom važnosti (kao danas), svaka u zadatoj količini; ako ne staje — probaj najmanju; ako
   ni ona ne staje — preskoči kulturu.
3. Staje kad je vrijednost u 95–100 % cilja.
4. **Težina** smije preći gornju granicu korpice za **do 10 %** (mala 5 kg → do 5,5 kg); donja granica
   ostaje.
5. **Marža** se nikad ne krši.

## Šta je ostavljeno graditelju

- Tačan izgled − / + kontrola i trake (u postojećem stilu editora).
- Redoslijed provjera unutar „Popuni umjesto mene", dok poštuje pravila iznad.

## Moje odluke (provjeri)

- Prazno `NajmanjeUKorpici` = zadata količina za malu korpicu.
- Korak 0,5 kg (ili najmanja količina ako je manja), 1 kom.
- Težina do +10 % iznad gornje granice.
- Stari endpoint `/zamijeni` ostaje, ali ga ekran više ne koristi.
- Prioritete (samo krompir/mrkva/luk/paprika na 0) postavljaš ti u Postavkama korpica.
