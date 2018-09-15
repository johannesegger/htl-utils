# htl-utils
Web-based collection of utilities for teaching at HTL.

## Run in Docker
```
docker run -d `
    -e SISDB_CONNECTION_STRING="Server=localhost;Database=default-db;User=root;Password=1234" `
    -e CREATE_DIRECTORIES_BASE_DIRECTORIES="X:/base-dirs/x;Y:;/base-dirs/y" `
    -e LDAP_HOST="..." `
    -e LDAP_PORT="389" `
    -e LDAP_DN_TEMPLATE="CN={0},DC=..." `
    -v /home/pi/x:/base-dirs/x \
    -v /home/pi/y:/base-dirs/y \
    -p 80:8085 `
    --name htl-utils
    johannesegger/htl-utils

```

## Acknowledgements
Hammer icon by George Patterson from the Noun Project