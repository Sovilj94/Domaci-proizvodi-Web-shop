# Sopstveni Nominatim

Ovaj folder opisuje lokalni Nominatim — servis koji pretvara adresu u koordinate
i obrnuto. Koristi ga `NominatimGeocodingService`, a kroz njega i `/geocode`
endpointi koje zove map picker u pregledaču.

## Zašto sopstvena instanca umjesto javne

Isti razlog kao kod OSRM-a, samo sa osjetljivijim podatkom. Javnoj instanci
`nominatim.openstreetmap.org` išla je **bukvalna adresa kupca**: prvo direktno
iz pregledača svakog kupca, na svako kucanje u polju adrese, a serverski i u
serijama kroz „Geokodiraj adrese bez koordinata".

Prvi korak je već napravljen: pregledač više ne zove treću stranu, sve ide kroz
naš `/geocode`. Time adrese izlaze na **jednom** mjestu umjesto na jednom po
kupcu — ali još izlaze. Ovaj folder zatvara i to.

Uz privatnost, sopstvena instanca rješava i brzinu. Javna dopušta najviše jedan
zahtjev u sekundi, pa `NominatimGeocodingService` serijalizuje **sav** saobraćaj
kroz taj jedan zahtjev; pod više korisnika pretraga adrese osjetno kasni. Zato
`Nominatim:ThrottleMs` ide na `0` kad se koristi sopstvena instanca — i to je
jedina izmjena konfiguracije pored `Nominatim:BaseUrl`.

## Prije nego počneš

Uvoz je jednokratan, ali nije mali:

- **Disk:** računaj nekoliko desetina GB za bazu nad izvodom Bosne i
  Hercegovine, plus ~150 MB za sam izvod koji imidž preuzme. Provjeri slobodan
  prostor prije početka — uvoz koji stane na punom disku ostavlja bazu u pola
  posla i mora se ponavljati od nule.
- **Memorija:** `shm_size` je u `docker-compose.yml` postavljen na `1gb`, jer je
  podrazumijevanih 64 MB premalo za uvoz. Dokumentacija imidža savjetuje i do
  pola dostupne RAM memorije za veće izvode.
- **Vrijeme:** desetine minuta. **Prvo pokretanje kontejnera JE uvoz** — servis
  dotle ne odgovara na upite. Ne prekidati ga.
- `POSTGRES_*` vrijednosti u compose fajlu su spuštene sa podrazumijevanih
  (`maintenance_work_mem` 10GB, `effective_cache_size` 24GB) na vrijednosti za
  razvojnu mašinu. Ako uvoz pada na memoriji, spusti ih dalje; ako mašina ima
  više RAM-a, podigni ih i uvoz će biti brži.

## Uvoz i pokretanje (jednokratno)

```bash
cd backend
docker compose up -d nominatim
docker compose logs -f nominatim    # prati uvoz; zavrsava linijom da je API spreman
```

Stanje uvoza živi u imenovanom volumenu `epiljara-nominatim-data`, pa se pri
sljedećim pokretanjima uvoz **ne** ponavlja.

Kad završi, provjeri direktno:

```bash
curl "http://localhost:8080/search?format=jsonv2&q=Kralja%20Petra%20I%2012,%20Banja%20Luka&countrycodes=ba&limit=1"
curl "http://localhost:8080/reverse?format=jsonv2&lat=44.577975&lon=17.005162"
```

Oba moraju vratiti JSON sa `lat`/`lon` i `display_name` — to su tačno oblici koje
`NominatimGeocodingService` čita.

### Ako izvod mape već imaš

OSRM koristi **isti** `bosnia-herzegovina-latest.osm.pbf`. Da izbjegneš drugo
preuzimanje od ~150 MB, zamijeni u `docker-compose.yml` `PBF_URL` sa `PBF_PATH`
i montiraj postojeći folder:

```yaml
    environment:
      PBF_PATH: /nominatim/data/bosnia-herzegovina-latest.osm.pbf
    volumes:
      - epiljara-nominatim-data:/var/lib/postgresql/16/main
      - ./osrm/data:/nominatim/data:ro
```

`PBF_URL` i `PBF_PATH` se **ne** smiju koristiti zajedno.

## Prebacivanje aplikacije na sopstvenu instancu

Dvije vrijednosti, ništa u kodu:

```jsonc
// backend/src/EPiljara.API/appsettings.Development.json
"Nominatim": {
  "BaseUrl": "http://localhost:8080/",
  "ThrottleMs": 0
}
```

U produkciji isto preko environment varijabli: `Nominatim__BaseUrl` i
`Nominatim__ThrottleMs` (vidi `PRODUCTION.md`). Za API koji se vrti kao `api`
servis u istom compose-u to je već podešeno na `http://nominatim:8080/` i `0`.

**`ThrottleMs` ostaviti na `1000` dok se koristi javna instanca.** To nije
podešavanje performansi nego uslov njihovih pravila korišćenja; kršenje vodi u
blokadu IP adrese.

`nominatim` namjerno **nije** u `depends_on` za `api`: prvo pokretanje bi držalo
API-ja da čeka uvoz. Geokodiranje koje ne uspije vraća `null` i ekran to
podnosi, pa je bolje da API radi bez njega nego da ne krene.

## Osvježavanje mape

Baza se uvozi sa `FREEZE=true`, što je manje na disku ali isključuje primanje
diffova. Osvježavanje je zato ponovni uvoz, isto kao kod OSRM-a:

```bash
cd backend
docker compose down nominatim
docker volume rm epiljara-nominatim-data
docker compose up -d nominatim
```

Ako ti treba osvježavanje bez ponovnog uvoza, stavi `FREEZE=false` i
`UPDATE_MODE=continuous` uz `REPLICATION_URL=https://download.geofabrik.de/europe/bosnia-herzegovina-updates/`
— po cijenu većeg zauzeća diska.

## Šta ostaje kod treće strane i posle ovoga

**Map tiles.** `AddressPicker` ih učitava sa `tile.openstreetmap.org`. To odaje
grubo koju oblast korisnik gleda, ne adresu koju je upisao, i neodvojivo je od
korišćenja OSM pločica. Ako i to treba zatvoriti, traži se sopstveni tile server
— zaseban posao, veći od ovoga.
