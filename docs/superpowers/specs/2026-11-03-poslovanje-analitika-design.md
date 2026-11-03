# Poslovanje — analitika po mjesecima

**Datum:** 2026-11-03
**Grana:** `poslovanje` (iz `nabavka-potvrdjene`, HEAD `32a6708`, koja još nije spojena u `main`)

## Šta se radi

Nova admin stranica **„Poslovanje"** (`/admin/poslovanje`) sa analitikom poslovanja za izabrani
mjesec i pregledom cijele godine. Uz to, svaka nova narudžba od sada pamti nabavnu cijenu artikla, da
zarada za prošle mjesece ne zavisi od današnjih cijena.

## Zašto

Vlasnik danas nema pregled koliko je zaradio u mjesecu, šta se prodaje, da li se kupci vraćaju i od
koga se najviše kupuje. Dashboard pokazuje samo današnji dan.

## Šta se NE radi sada

| Odloženo | Zašto |
|---|---|
| Promocije i poeni (promo kodovi, dodijeljeni i iskorišteni poeni, dug u poenima) | Drugi krug, dogovoreno 2026-11-03. |
| Signali iz korpica (izbacivani i izbjegavani artikli) | Drugi krug. |
| Poređenje sa istim mjesecom prošle godine | Podaci postoje tek od 37. sedmice 2026. |
| Trošak dostave (gorivo, vozač) | Nije u bazi; prihod od dostave se prikazuje, trošak ne. |
| Istorija pauza pretplata | Pretplata pamti samo trenutni status; pauzirane se zato vide samo za tekući mjesec. |
| Izvoz u Excel | Štampa i PDF su dovoljni za početak. |
| Posebne tabele za statistiku ili keš | Stotine narudžbi mjesečno se računaju direktno. |

## Pravila računanja

Ova pravila važe za svaki broj na stranici.

1. **Mjesec narudžbe** je mjesec datuma isporuke njene sedmice
   (`SedmicaHelper.DatumIsporuke(Godina, Sedmica, dan, vrijemeOd)`, u zoni `Europe/Sarajevo`).
   Datum kreiranja narudžbe se ne koristi.
2. **Prihod se računa samo od isporučenih narudžbi** (`OrderStatus.Delivered`). Otkazane i
   neuspjele ne ulaze u prihod; prikazuju se u odjeljku Dostava.
3. **Promet robe** = `Subtotal`. **Neto prihod** = `Subtotal + DeliveryFee − Popust − PoeniPopust`
   (to je `TotalAmount`; računa se iz delova da bi se delovi mogli prikazati).
4. **Nabavni trošak:**
   - stavka korpice (`StavkaKorpiceNarudzbe`): `Kolicina × OrderItem.Quantity × nabavna`;
   - stavka Špajza (OrderItem bez stavki korpice, a artikal nije korpica): `Quantity × nabavna`;
   - red same korpice ne nosi nabavni trošak, jer se nabavlja njen sadržaj;
   - `nabavna` = snimak `NabavnaCijena` na stavci. Ako snimka nema, uzima se današnja
     `Product.NabavnaCijena` i mjesec se označava kao **procjena**. Ako ni nje nema, stavka se
     broji u „bez nabavne cijene" i ne ulazi u trošak.
5. **Zarada** = promet robe − nabavni trošak. **Marža** = zarada / promet robe. Dostava i popusti
   se ne miješaju u maržu robe.
6. **Kupac** je `CustomerId` za registrovane, a za goste normalizovani email (mala slova, bez
   razmaka), pa telefon ako emaila nema.
   - **Aktivan** u mjesecu: ima bar jednu isporučenu narudžbu u tom mjesecu.
   - **Novi**: njegova prva isporučena narudžba ikad je u tom mjesecu.
   - **Povratni**: aktivan, a nije novi.
   - **Zadržavanje**: od kupaca aktivnih u prethodnom mjesecu, procenat koji je aktivan i u ovom.
7. **Pretplate:**
   - **aktivne na kraju mjeseca**: `CreatedAt ≤ kraj` i (`OtkazanaU` prazno ili `> kraj`);
   - **nove**: `CreatedAt` u mjesecu;
   - **otkazane**: `OtkazanaU` u mjesecu;
   - **stopa otkazivanja** = otkazane / aktivne na početku mjeseca;
   - **pauzirane**: trenutni broj, prikazuje se samo za tekući mjesec;
   - **prihod iz pretplata**: neto isporučenih narudžbi sa `PretplataId`.
8. **Uspješnost isporuke** = isporučene / (isporučene + neuspjele) u mjesecu.

## Izmjena podataka: snimak nabavne cijene

- `OrderItem.NabavnaCijena` (`decimal?`) i `StavkaKorpiceNarudzbe.NabavnaCijena` (`decimal?`), jedna
  EF migracija. Postojeći redovi ostaju `null`.
- Popunjava se iz `Product.NabavnaCijena` na jednom mjestu: `AppDbContext.SaveChangesAsync` upisuje
  je u svaki novi red (`OrderItem`, `StavkaKorpiceNarudzbe`) koji je još nema. Tako je dobijaju sve
  putanje — kupac, gost, pretplata, admin ručna narudžba, izmjena i zamjena u korpici — i svaka
  buduća. Red koji kupac ili admin samo zadrži pri izmjeni korpice nosi svoj stari snimak
  (`MojaKorpica.cs`, `AdminKorpicaNarudzbe.cs`).
- Nabavna cijena se nikad ne šalje kupcu; ostaje u admin DTO-ima.

## Backend

Novi fajl `Features/Admin/AdminPoslovanje.cs`, admin endpointi u `AdminController`:

- `GET /admin/poslovanje?godina=&mjesec=` → `GetPoslovanjeQuery` → `PoslovanjeMjesecaDto`
- `GET /admin/poslovanje/godina?godina=` → `GetPoslovanjeGodinaQuery` → `PoslovanjeGodineDto`

Oba upita učitavaju narudžbe sedmica koje padaju u traženi period (sedmice se prevode u mjesece u
memoriji, po pravilu 1), sa stavkama i stavkama korpica, pa računaju u C#. Mjesečni upit računa i
prethodni mjesec, za ▲/▼ na karticama i za zadržavanje.

`PoslovanjeMjesecaDto`:

| Odjeljak | Polja |
|---|---|
| Zaglavlje | `Godina`, `Mjesec`, `Procjena` (bool), `BezNabavneCijene` (broj stavki) |
| 1. Prihod i zarada | `PrometRobe`, `Dostava`, `PromoPopust`, `PoeniPopust`, `Neto`, `Nabavno`, `Zarada`, `Marza`, `BrojNarudzbi`, `ProsjecnaNarudzba`, `UdioKorpica`, `UdioSpajza`; isti skup za `Prethodni` |
| 2. Prodaja | `Korpice[]` (tip, veličina, komada, prihod, nabavno, marža), `Spajz[]` (kategorija, prihod, nabavno, marža), `TopArtikli[]` i `NajslabijiArtikli[]` (naziv, količina, jedinica, prihod, zarada) |
| 3. Kupci | `Aktivni`, `Novi`, `Povratni`, `Zadrzavanje` (null bez prethodnog mjeseca), `PotrosnjaPoKupcu`, `NarudzbiPoKupcu`, `Gosti`, `Registrovani`, `NoviPrekoPreporuke`, `TopKupci[]` (ime, narudžbi, neto) |
| 4. Pretplate | `AktivneNaKraju`, `Nove`, `Otkazane`, `StopaOtkazivanja`, `Pauzirane` (null osim za tekući mjesec), `PoPlanu[]`, `UdioPrihoda` |
| 5. Dostava | `PoZoni[]` (zona, narudžbi, neto), `Isporucene`, `Neuspjele`, `Uspjesnost`, `Otkazane`, `OtkazaneVrijednost`, `OtkazaneUdio`, `ProsjecnaOcjena`, `BrojOcjena` |
| 6. Gazdinstva | `Gazdinstva[]` (naziv, selo, nabavno, artikli[] sa količinom, jedinicom i nabavnim) |

Artikli u prodaji i gazdinstvima se sabiraju iz stavki korpica i Špajza zajedno.

`PoslovanjeGodineDto`: `Mjeseci[12]` (mjesec, neto, zarada, marža, narudžbi, aktivni kupci,
aktivne pretplate, procjena) i `Sezona[]` (kategorija, prihod po mjesecima[12]).

## Frontend

- Stavka **„Poslovanje"** u admin meniju, odmah ispod „Pregled".
- `/admin/poslovanje?godina=&mjesec=`: izbor mjeseca strelicama, bez parametara otvara tekući mjesec.
  - **Kartice**: neto prihod, zarada (KM i %), narudžbe, prosječna narudžba, aktivni kupci, svaka sa
    ▲/▼ % prema prethodnom mjesecu.
  - Traka **„Procjena"** kad mjesec ima stavke bez snimka nabavne cijene.
  - Šest odjeljaka kao sklopivi blokovi sa tabelama.
- `/admin/poslovanje/godina?godina=`: stubičasti grafikon neto i zarade po mjesecima (ručno
  nacrtan SVG, bez nove biblioteke), tabela 12 mjeseci, sezonska tabela kategorija × mjeseci.
- **Štampaj** i **PDF** (pdfmake, kao nabavka) za mjesečni pregled.
- Čista logika (promjena u %, skaliranje grafikona) ide u `lib/admin/poslovanje.ts`, sa testovima.

## Testovi

Backend (`PoslovanjeTests.cs`, preko `Scenario`):

- narudžba se broji u mjesecu isporuke, ne kreiranja (sedmica na prelazu mjeseca);
- prihod broji samo isporučene, a otkazane i neuspjele idu u Dostavu;
- zarada se računa iz snimka; bez snimka se računa iz današnje cijene i mjesec je `Procjena`;
- korpica se računa po svom sadržaju, Špajz po artiklu;
- novi i povratni kupci, zadržavanje, gost prepoznat po emailu;
- stopa otkazivanja pretplata;
- uspješnost isporuke;
- zbir po gazdinstvu;
- nova narudžba (kupac i gost) i zamjena u korpici snimaju nabavnu cijenu.

Frontend: test za promjenu u % i skaliranje grafikona, pa `npm run build`.
