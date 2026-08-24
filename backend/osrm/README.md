# Sopstveni OSRM

Ovaj folder sadrži konfiguraciju za lokalni OSRM (Open Source Routing Machine)
server koji koristi `OsrmRouteOptimizer` za računanje optimalnog redoslijeda
obilaska stanica na dostavnoj turi.

## Zašto sopstvena instanca umjesto javnog demo servera

Ranije se koristio javni demo server `router.project-osrm.org`. Svaka
optimizacija mu je slala tačne koordinate magacina i svih kupaca na turi, a
ručni ponovni račun i **živu poziciju vozača** — bez ikakve kontrole nad tim
šta oni loguju ili čuvaju. Uz to, njihova pravila korišćenja demo server
ograničavaju samo na razvoj i testiranje, ne na produkciju.

Pokretanjem OSRM-a u sopstvenoj infrastrukturi, koordinate kupaca i pozicija
vozača nikad ne napuštaju naš sistem.

Iz istog razloga, `docker-compose.yml` pokreće `osrm-routed` sa
`--verbosity WARNING`. Ovo nije podešavanje protiv "buke" u logu — bez njega
OSRM sam upisuje svaki zahtjev (pa i koordinate iz njega) u sopstveni access
log, što bi premjestilo isto curenje jedan sloj niže umjesto da ga zatvori.

`data/` se namjerno **ne commituje** (vidi `backend/.gitignore`) — izvod mape
je 152 MB, a obrađeni fajlovi dodaju još nekoliko stotina megabajta. Svako ko
radi na projektu ga preuzima i obrađuje lokalno, jednokratno, po uputstvu
ispod.

## Preuzimanje i obrada mape (jednokratno)

Traje otprilike 5-15 minuta za Bosnu i Hercegovinu.

```bash
cd backend
mkdir -p osrm/data && cd osrm/data
curl -L -o bosnia-herzegovina-latest.osm.pbf \
  https://download.geofabrik.de/europe/bosnia-herzegovina-latest.osm.pbf
ls -lh bosnia-herzegovina-latest.osm.pbf
```

Očekivano: oko 152 MB. Ako je bitno manje, preuzimanje je prekinuto — ponoviti
umjesto nastaviti sa nepotpunim fajlom.

Zatim tri koraka MLD (Multi-Level Dijkstra) pipeline-a, svaki u kontejneru nad
istim folderom:

```bash
cd ..   # iz backend/osrm/data (gdje je prethodni blok ostavio terminal) u backend/osrm
docker run --rm -v "$(pwd)/data:/data" osrm/osrm-backend:latest \
  osrm-extract -p /opt/car.lua /data/bosnia-herzegovina-latest.osm.pbf
docker run --rm -v "$(pwd)/data:/data" osrm/osrm-backend:latest \
  osrm-partition /data/bosnia-herzegovina-latest.osrm
docker run --rm -v "$(pwd)/data:/data" osrm/osrm-backend:latest \
  osrm-customize /data/bosnia-herzegovina-latest.osrm
```

`osrm-extract` je najzahtjevniji korak po memoriji. Ako padne sa porukom o
memoriji, to je limit dodijeljen Docker Desktopu, ne greška u komandi —
povećati memoriju u Docker Desktop podešavanjima, ne mijenjati region na manji.

Provjeriti slobodan prostor na disku prije početka — obrada iznad samog
izvoda (152 MB) traži još nekoliko stotina megabajta.

## Pokretanje

```bash
cd backend
docker compose up -d osrm
```

Server sluša na portu 5000. `EPiljara.Infrastructure` mu se obraća preko
`Osrm:BaseUrl` iz konfiguracije (`http://localhost:5000/` kad se API vrti sa
hosta, `http://osrm:5000/` kad se vrti kao `api` servis u istom compose-u).

## Osvježavanje mape kad OSM podaci zastare

Kad podaci u `data/` zastare (nove ulice, izmjene u saobraćaju), ponoviti
kompletan postupak nad novim izvodom — isti tri koraka:

```bash
cd backend/osrm/data   # apsolutno od korijena repozitorija
curl -L -o bosnia-herzegovina-latest.osm.pbf \
  https://download.geofabrik.de/europe/bosnia-herzegovina-latest.osm.pbf

cd ..   # sada u backend/osrm
docker run --rm -v "$(pwd)/data:/data" osrm/osrm-backend:latest \
  osrm-extract -p /opt/car.lua /data/bosnia-herzegovina-latest.osm.pbf
docker run --rm -v "$(pwd)/data:/data" osrm/osrm-backend:latest \
  osrm-partition /data/bosnia-herzegovina-latest.osrm
docker run --rm -v "$(pwd)/data:/data" osrm/osrm-backend:latest \
  osrm-customize /data/bosnia-herzegovina-latest.osrm

cd ..   # sada u backend, gdje je docker-compose.yml
docker compose restart osrm
```

Novi `.osrm*` fajlovi prepisuju stare pri ponovnom pokretanju `osrm-extract`
nad istim imenom fajla; nije potrebno ništa ručno brisati.
