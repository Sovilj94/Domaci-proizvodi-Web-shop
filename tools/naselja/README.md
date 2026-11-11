# Početne granice banjalučkih naselja

Jednokratni alat. Pravi `backend/src/EPiljara.Infrastructure/Persistence/Podaci/naselja-2026-11-11.json`,
koji migracija `NaseljaIGranice` upisuje u `DeliveryZones`. Poslije toga granice žive samo u bazi i
mijenjaju se na admin stranici „Naselja". Alat se ponovo pokreće samo za novi početni skup.

## Izvor (preuzeto 11.11.2026.)

Podaci su iz OpenStreetMap-a, © OpenStreetMap contributors, pod licencom ODbL
(https://www.openstreetmap.org/copyright). Preuzeti su preko Overpass API-ja:

- `ulaz/granice-osm.json`: `rel(id:2528156,10079830,10546755);out geom;`
  - 2528156 je „Grad Banja Luka" (admin_level 6);
  - 10079830 je MZ Srpske toplice, a 10546755 MZ Kočićev vijenac. To su jedine MZ sa granicom u OSM-u.
- `ulaz/mjesta-osm.json`: tačke naselja, četvrti i sela (`place=*`) na području Grada.

Grad Banja Luka objavljuje spisak od 57 mjesnih zajednica, ali ne i njihove granice.

## Kako nastaju granice

1. Numerisane MZ se spajaju u jedno naselje (Borik 1 i 2 postaju Borik i slično), pa ostaje 51 naselje.
   Za svako se uzima OSM tačka. Za Gornju Piskavicu i Čokorska Polja tačke nema.
2. Iz svih centara se pravi Voronoi u lokalnoj projekciji (dužina × cos 44,77°), pa se presijeca
   granicom Grada.
3. Srpske toplice i Kočićev vijenac dobijaju pravu OSM granicu, koja se oduzima od susjeda.
4. Čuva se samo 24 naselja, po odluci vlasnika od 11.11.2026.: 17 gradskih plus Drakulić, Donja Kola,
   Debeljaci, Šargovac, Motike, Saračica i Zalužani. Područje okolnih sela ostaje van svih naselja.

Sve granice imaju oznaku `procjena`, dok ih admin ne potvrdi.

## Pokretanje

```bash
cd tools/naselja
dotnet run -- ../../backend/src/EPiljara.Infrastructure/Persistence/Podaci/naselja-2026-11-11.json
```
