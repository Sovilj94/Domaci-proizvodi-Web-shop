# R2–R4 — korpice, Špajz, ostale stranice — plan (sažet)

> Izvršava se u istoj sesiji poslije R1 (superpowers:executing-plans). Spec: `specs/2026-10-16-farmit-izgled-design.md`.

## R2 — Korpice i planovi (grana `redizajn-r2`)
- [x] Backend: `PlanPretplate`, `Pretplata.Plan`/`ObavezaIsporuka`, `PretplatnaCijena.Izracunaj(puna, plan)`, `Obaveza`, `PreostaloObaveze`; opcije 10/15/20 %, obaveza 4/13; otkaz blokiran dok traje obaveza (`obaveza_aktivna`), `DELETE /admin/pretplate/{id}`; `KorpicaDto` cijene po planu; javni sadržaj prvo za sedmicu koja se prodaje; migracija `DodajPlanPretplate` (+ sakriva domaćinsku/mliječnu/suhomesnatu). Testovi prvo.
- [x] Frontend: `/korpice` → `/korpice/povrce`; `/korpice/{vrsta}?plan=` (segmenti, kartice desktop/mobilni, modal sadržaja u boji); `/korpice/{vrsta}/{velicina}?plan=` (veličina, osobine, pretplata/jednokratno, sadržaj u karticama); stari slug preusmjerava; plan u formi pretplate, kartici naloga i FAQ; admin `/admin/pretplate` sa otkazom.

## R3 — Špajz (grana `redizajn-r3`)
- [x] `/spajz` (kategorije u krugovima, izdvojeni, svi, pretraga u URL-u, „Zato što znaš šta vrijedi", „Zašto kod nas?"); `/products` → `/spajz`; nova `ProductCard`; „Dodaj iz Špajza" na stranici korpice i u korpi.

## R4 — Ostale stranice (grana `redizajn-r4`)
- [ ] `PageHeader` (centriran naslov 60/32px + opis) na listama: analize, recepti, domaćini, o nama, postani partner.
- [ ] Korpa, checkout, potvrda/praćenje, prijava/registracija, moj nalog, stranica proizvoda: kartice 26px sa mekom sjenkom, pill dugmad, naslovi bez serifa.
- [ ] 404 pod `(shop)`.
- [ ] Provjera: desktop + 390 px po stranici, ćirilica, `tsc`/`lint`/`test`/`build`, `dotnet test`; dokumentacija (STANJE, CLAUDE, PRODUCTION, ZA-URADITI).
