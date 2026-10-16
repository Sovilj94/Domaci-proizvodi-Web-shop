# R1 — Osnova i početna (farmit izgled) — plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Koraci su `- [ ]`.

**Goal:** Dizajn sistem farmit-a (tokeni, Poppins, dugmad, kartice), zaglavlje, traka roka, mobilni meni, footer sa prijavom na novosti i početna sa deset sekcija.

**Architecture:** Vrijednosti postojećih Tailwind tokena se zamijene farmit vrijednostima (većina ekrana se preboji sama); nove sekcije početne su zasebne komponente u `components/pocetna/`; jedan lagani `Karusel` (scroll-snap, strelice, tačkice) služi utiscima, analizama i domaćinima. Backend dobija `Product.Boja` sa rezervnom bojom (`BojaProizvoda`) i `PrijavaNovosti`.

**Tech Stack:** Next 15 / React 19 / Tailwind v4, ASP.NET Core 9 + EF Core, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-16-farmit-izgled-design.md` · **Uzor:** `docs/superpowers/farmit-snimak-2026-10-16.md`

## Global Constraints

- Boje: primary `#228B22` (+ `#E6F2E6 #CEE5CE #B5D8B5 #9DCB9D #84BF84 #6CB26C`), secondary `#FE7E0F` (+ `#FFF1E4 #FFD4AF #FFC695 #FEB87A #FEAA60`), beige `#FFF1E4`, ink `#394236`, muted `#6C757D`, line `#D9DBD8`.
- Font: Poppins (latin, latin-ext); na ćirilici Montserrat preko `html[data-pismo="cir"]`.
- Dugme: pill, `px-6 py-3`, 16px/600. Kartica: `rounded-[26px]`, sjenka `0 10px 20px rgba(64,74,61,.3)`.
- Ne kopirati farmit tekstove, fotografije ni logo.
- Ijekavica; srpska imena u demo podacima; enum na žici PascalCase; validator počinje `Input NotNull` gdje je ulaz omotan.
- Frontend gotov tek poslije `tsc`, `lint`, `npm test` i `npm run build`.

## Review Focus

1. Karusel sa jednim ili nijednim elementom → bez strelica i tačkica, ne puca.
2. Nema objavljenog sadržaja korpice za sedmicu → sekcija „Ova sedmica" pokazuje odbrojavanje i poruku, bez praznih pločica.
3. Prozirno zaglavlje na početnoj poslije skrola → postaje bijelo (tekst čitljiv preko bijelih sekcija).
4. Mobilni meni otvoren pa navigacija → meni se zatvara, skrol tijela se vraća.
5. Ćirilica: Montserrat se primjenjuje, logotip i kodovi ostaju latinicom.

---

### Task 1: Backend — boja proizvoda i prijava na novosti

**Files:** `Domain/Entities/Product.cs` (+`Boja`), `Domain/Entities/PrijavaNovosti.cs` (novo), `AppDbContext` (mapiranje, jedinstven e-mail), migracija `DodajBojuINovosti`, `Application/Common/BojaProizvoda.cs` (novo: `Za(string? boja, string naziv)` → zadana ili pastelna iz palete po hešu naziva), DTO proizvoda (`Boja` uvijek popunjena), `StavkaSadrzajaDto` (`Boja`), `AnalizaDto` (`Slika`, `Boja` iz vezanog proizvoda), admin `ProductInput` (`Boja` opciono, validacija `^#[0-9A-Fa-f]{6}$`), `Features/Novosti/PrijavaNovosti.cs` (`PrijaviNaNovostiCommand(Email, Website)` honeypot; duplikat = uspjeh bez reda), `GetAdminNovostiQuery`, `NovostiController` `POST /novosti/prijava` (politika `auth`), `AdminController` `GET /admin/novosti`, seed boje za postojeće proizvode.

- [ ] Testovi (`BojaProizvodaTests`, `NovostiTests`): zadana boja ima prednost; bez boje — ista boja za isti naziv, iz palete; nevalidan hex odbijen; prijava upisuje normalizovan e-mail; duplikat ne pravi drugi red; honeypot ne upisuje; neispravan e-mail 400. FAIL → implementacija → `dotnet test` PASS → migracija → commit.

### Task 2: Dizajn sistem

**Files:** `app/globals.css` (tokeni: `brand-*` → primary skala, `honey-*` → secondary skala, `cream` → `#FFF1E4` beige, `ink`, `muted`, `line`, `sand` → `#F5F5F5` siva površina; novi `--color-primary`, `--color-secondary`, `--radius-card: 26px`, `--shadow-card`; Montserrat na ćirilici), `app/layout.tsx` (Poppins umjesto DM Sans/Playfair/Onest, Montserrat `preload:false`), `components/ui/button.tsx` (pill; `primary` narandžasto, `istaknuto` zeleno, `secondary` okvir narandžast, `bijelo`), `components/ui/section-header.tsx` (centriran naslov 48/28px), `components/pocetna/sekcija.tsx` (prop `ton: "bijela" | "krem"`, `luk` za zaobljenu gornju ivicu).

- [ ] `tsc` + `lint` čisto → commit.

### Task 3: Zaglavlje, traka roka, mobilni meni

**Files:** `components/layout/shop-navbar.tsx` (prepis: logo, linkovi Početna/Korpice/Špajz/Analize/Recepti sa aktivnom narandžastom podvlakom, „Prijavi se"/„Registruj se" ili ime + odjava, prekidač pisma, korpa; na `/` prozirno dok je `scrollY < 40`, bijeli tekst), `components/layout/traka-roka.tsx` (novo: krem traka sa tekstom iz `useSedmica` + `lib/sedmica.ts`, marquee na `<md`), `components/layout/mobilni-meni.tsx` (novo: preko cijelog ekrana, zaključava skrol, zatvara se na navigaciju i Escape), `app/(shop)/layout.tsx` (traka roka osim na `/`; ukloniti `CategoryRail` i `MobileTabBar`), `components/layout/logo.tsx` (ton bijeli).

- [ ] `tsc` + `lint` → commit.

### Task 4: Footer i prijava na novosti

**Files:** `components/layout/shop-footer.tsx` (prepis po uzoru: bijela kartica „Prijavi se i budi u toku" sa poljem i narandžastim dugmetom unutra, linkovi O nama · Korpice · Špajz · Postani partner · Recepti · Analize, kontakt, rok (`FooterRok`), prekidač pisma, ©), `components/layout/prijava-novosti.tsx` (novo; honeypot `website`), `lib/api/endpoints.ts` (`novostiApi.prijavi`, `adminApi.novosti`), `app/admin/novosti/page.tsx` + stavka u admin navigaciji (lista + „Kopiraj sve e-mailove").

- [ ] `tsc` + `lint` → commit.

### Task 5: Početna — deset sekcija

**Files (novo u `components/pocetna/`):** `karusel.tsx` (scroll-snap, strelice na `md+`, tačkice; 0–1 element bez kontrola), `hero-karusel.tsx` (slajdovi: korpica povrća, korpica voća, Špajz; fotografija preko cijelog ekrana, preliv, bijeli naslov, bijelo pill dugme sa emoji, sitan tekst, strelice, traka napretka, auto 6 s, pauza na hover), `ova-sedmica.tsx` (naslov, dan isporuke, krem kutija odbrojavanja DANA:SATI:MINUTA iz postojećeg `Odbrojavanje` računa, kartica sa pločicama sadržaja srednje korpice povrća: `boja`, ime, slika; prvih 5 + „+N"; „Naruči korpicu"), `utisci-karusel.tsx`, `njiva-do-stola.tsx` (tri pločice Povrće/Voće/Špajz), `analize-karusel.tsx` (obojene kartice: slika, bedž „Povrće | domaćin", „Izvještaj o ispitivanju – {uzorak}", „Vidi sve analize"), `kako-funkcionise.tsx` (prepis: bijeli krugovi sa emoji, isprekidana linija, mobilni vertikalno), `rukuj-se.tsx` (fotografija + preklop + tekst), `domacini-karusel.tsx` (kartica sa „ušicom", okrugla fotografija sa zelenim okvirom, MJESTO, „Domaćinstvo X"), `hocu-korpicu.tsx`, `cesta-pitanja.tsx` (prepis po uzoru); `app/(shop)/page.tsx` (redoslijed sekcija); `public/izgled/README.md` (koje fotografije vlasnik zamjenjuje). Stare `hero-korpica.tsx`, `utisci.tsx`, `domacini.tsx`, `domacin-kartica.tsx` brisati kad više nemaju pozivalaca (`domacin-kartica` koristi i `/proizvodjaci` — provjeriti).

- [ ] `tsc` + `lint` + `npm test` → commit.

### Task 6: Provjera

- [ ] API + dev server; početna na 1440 i 390 px uporedo sa snimkom farmit-a (svaka sekcija), mobilni meni, traka roka, prijava na novosti (201, duplikat 201 bez reda, admin lista), ćirilica (Montserrat, bez zaostale latinice), admin i dalje radi (boje se promijenile, raspored isti).
- [ ] `dotnet test`, `tsc`, `lint`, `npm test`, `npm run build`; `STANJE.md`/`CLAUDE.md`; commit.
