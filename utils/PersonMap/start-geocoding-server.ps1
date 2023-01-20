docker run `
    -e PBF_URL=https://download.geofabrik.de/europe/austria-latest.osm.pbf `
    -e REPLICATION_URL=https://download.geofabrik.de/europe/austria-updates `
    -p 8080:8080 `
    mediagis/nominatim:4.2

