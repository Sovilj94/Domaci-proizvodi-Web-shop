# farmit.rs — snimak izgleda i rada (2026-10-16)

Uzor za redizajn E piljare (spec `specs/2026-10-16-farmit-izgled-design.md`). Preuzima se
**izgled, raspored, komponente i model rada**. Ne preuzimaju se njihovi tekstovi, fotografije,
logo ni naziv — na tim mjestima idu E piljara sadržaji.

## Dizajn tokeni (izvučeni iz njihovog CSS-a)

| Token | Vrijednost | Upotreba |
|---|---|---|
| primary | `#228B22` | linkovi navigacije, zelena dugmad („istaknuto"), aktivni plan, okvir slike farmera |
| primary-200/300/400/500/600/700 | `#E6F2E6` `#CEE5CE` `#B5D8B5` `#9DCB9D` `#84BF84` `#6CB26C` | zelene pozadine (info kutija „Biraš dan dostave", tab Povrće) |
| secondary | `#FE7E0F` | glavna CTA dugmad, „Prijavi se", aktivni link (podvlaka), cijene na akciji |
| secondary-200 (beige) | `#FFF1E4` | krem pojasevi sekcija, traka roka, kutija odbrojavanja |
| secondary-400/500/600/700 | `#FFD4AF` `#FFC695` `#FEB87A` `#FEAA60` | |
| technical-h1 | `#394236` | svi naslovi i tamni tekst |
| technical-placeholder | `#6C757D` | sekundarni tekst |
| technical-border | `#D9DBD8` | okviri |
| font | **Poppins** (naslovi 700, tekst 400/500/600) | |
| dugme (CTA) | pozadina secondary, bijeli tekst, 16px/600, `padding 12px 24px`, **pill** (`rounded-full`) | |
| dugme okvir | bijelo, okvir 0.8px secondary, tekst secondary | „Registruj se", „Pogledaj sve proizvode" |
| kartica | bijela, `rounded-[26px]`, sjenka `0 10px 20px rgba(64,74,61,.3)`, hover `scale(1.02)` | gajbice, farmeri |
| h1 stranice | 60px / 700 / technical-h1, centrirano | „Izaberi gajbicu" |
| naslov sekcije | ~48px / 700 / technical-h1, centrirano | „Ovonedeljna dostava" |
| navigacija | linkovi 18px/600 primary, aktivni sa narandžastom podvlakom; na početnoj bijeli preko hero slike | |

## Zaglavlje

- **Desktop:** logo lijevo; linkovi Početna · Gajbice · Špajz · Analize · Recepti; „Prijavi se"
  (puno narandžasto) i „Registruj se" (okvir); izbor jezika; ikona korpe. Na početnoj je prozirno
  preko hero fotografije (bijeli tekst), na ostalim stranicama bijelo sa zelenim linkovima.
- Ispod zaglavlja (osim početne): **krem traka** „🚚 Poruči do četvrtka u 12h i stiže ti prva gajbica u
  petak ili subotu" — na mobilnom se kreće (marquee).
- **Mobilni:** hamburger lijevo, logo, „Prijavi se" (okvir) desno. Meni preko cijelog ekrana: X gore
  desno, „Jezik" sa izborom, stavke sa ikonom lijevo i strelicom desno (Početna, Gajbice, Špajz,
  Analize, Recepti), dole puno „Prijavi se" i okvir „Registruj se".
- Plutajuće zeleno dugme chata dole desno (Crisp) — kod nas opciono (Viber/telefon).

## Početna (redom)

1. **Hero karusel** preko cijelog ekrana: fotografija, tamni preliv, veliki bijeli naslov („Znam šta
   jedem."), podnaslov, bijelo pill dugme sa emoji („🥦 Pogledaj gajbice"), sitan tekst ispod
   („Pridruži se! 2000 porodica…"), strelice lijevo/desno, traka napretka slajdova dole. Slajdovi po
   vrsti (povrće, jaja…).
2. **Ovonedeljna dostava** (bijela): naslov, „🚚 Biraš dan dostave u petak 23.10. ili subotu 24.10.",
   krem kutija „⏱ ROK PORUČIVANJA OVE NEDELJE" sa odbrojavanjem DANA : SATI : MINUTA, ispod bijela
   kartica „🥕 Gajbica povrća / Sadržaj ovonedeljne gajbice" sa **obojenim pločicama namirnica** (boja
   po namirnici, ime gore lijevo, slika dole) i pločicom „+5", pa narandžasto „Naruči gajbicu".
3. **Rekli su o farmitu** (bijela): karusel — velika fotografija lijevo, desno „#1", ime, tekst, i
   red manjih fotografija; strelice; traka napretka.
4. **Sa njive do tvog stola** (krem): podnaslov, tri velika obojena dugmeta-pločice (🥦 Povrće zeleno,
   🥚 Jaja breskva, 🏪 Špajz smeđe) sa strelicom.
5. **Kako znam šta jedem?** (bijela): tekst o analizama, karusel obojenih kartica analiza (slika
   namirnice, bedž „Povrće | PG ime", „Izveštaj o ispitivanju – Mladi kupus"), tačkice, „Vidi sve analize".
6. **Kako funkcioniše?** (krem, zaobljena gornja ivica): tri koraka — bijeli krug sa emoji, isprekidana
   linija između, naslov, tekst. Mobilni: vertikalno, ikona lijevo.
7. **Puna fotografija** sa preklopom: „Rukuj se sa rukom koja te hrani" + tekst.
8. **Ovo su tvoji farmeri 🧑‍🌾** (krem): karusel bijelih kartica sa „ušicom" gore desno, okrugla fotografija
   sa zelenim okvirom, MJESTO (malim slovima), „Gazdinstvo Prezime"; tačkice.
9. **Hoću moju gajbicu! 🥕** (bijela): fotografija gajbice lijevo, veliki zeleni naslov, „Pogledaj gajbice".
10. **Najčešća pitanja 🙋** (krem, zaobljena gornja ivica): podnaslov, harmonika pitanja.
11. **Footer:** bijela kartica sa sjenkom „Prijavi se i budi u toku" + polje e-maila sa narandžastim
    dugmetom unutra; linkovi O nama · Gajbice · Postani partner; društvene mreže; Opšti uslovi · Polisa
    privatnosti; logotipi plaćanja; logo; kontakt, adresa, PIB.

## Gajbice `/gajbice/{vrsta}?plan=`

- H1 „Izaberi gajbicu", podnaslov („**Direktno** sa gazdinstava. **Testirano.** …").
- Segment sa tri taba (🥦 povrće · 🍑 voće · 🥚 jaja) u okviru, aktivni zeleno popunjen.
- Pill segment plana: **NEDELJNA · MESEČNA · 3 MESECA** (aktivni zelen).
- Tri kartice (Mala / Srednja / Velika): fotografija, naziv, „Gajbica za 1 do 2 člana", četiri reda sa
  emoji (🚚 1 dostava do tvojih vrata · ⚖️ oko 3–5 kg svežeg povrća · 🌱 8+ testiranih i sezonskih kultura ·
  ✨ Izmeni sadržaj nakon kupovine), cijena „RSD 2390 /gajbica", dugme „Odaberi gajbicu" (srednja
  **zeleno** + bedž „Popularno"), link „Pogledaj sadržaj gajbice" → modal sa obojenim redovima namirnica
  (ime, količina, 📍 selo, 🧑‍🌾 proizvođač, slika).
- Napomena „*Slike su informativnog karaktera…", pa „Rekli su o farmitu".
- **Mobilni:** kartice postaju horizontalne (slika lijevo, naziv, opis, cijena, link „Sadržaj gajbice",
  zeleno okruglo dugme sa strelicom desno).

## Stranica gajbice `/gajbice/{vrsta}?plan=…/{velicina}`

- Lijevo: strelica nazad, velika fotografija sa strelicom karusela, red sličica.
- Desno kartica: naziv, opis, prekidač veličine MALA/SREDNJA/VELIKA (aktivna narandžasta okvir +
  krem), četiri reda sa emoji, zelena info kutija („🚚 Biraš dan dostave u pet 23.10. ili sub 24.10." ·
  „✓ Besplatna dostava uz gajbicu povrća").
- Druga kartica — izbor kupovine (radio):
  - **Nedeljna pretplata** — narandžasta traka „300 RSD UŠTEDE + POKLON JAJA", precrtana stara cijena,
    nova, „384 RSD/dan", krem kutija „UZ PRVU DOSTAVU DOBIJAŠ" (jaja za 1 RSD), „Nisi zadovoljan?
    Vraćamo novac." · „Pauziraj ili otkaži".
  - **Jednokratna kupovina** — cijena.
  - Narandžasto „Dodaj u korpu" preko cijele širine.
- Ispod: „Ove nedelje u srednjoj gajbici…" — mreža obojenih kartica namirnica.

## Špajz `/proizvodi`

- H1 „Proizvodi čije poreklo znaš", podnaslov.
- Kategorije u krugovima (svijetlosiva pozadina, slika, naziv ispod): Svi proizvodi · Povrće · Jaja ·
  Ajvar · Džem · Med · Paketi — na mobilnom bočni skrol.
- „Zavolećeš ove proizvode…" + „Svi proizvodi" desno; red kartica (siva pozadina slike, naziv, cijena
  „RSD 450" lijevo i pakovanje „10 kom" desno, narandžasto „Dodaj u korpu"). Mobilni: 2 kolone.
- „Zato što znaš šta vredi" (krem): tri narandžaste ilustracije sa naslovom i tekstom.
- „Zašto kod nas?".
