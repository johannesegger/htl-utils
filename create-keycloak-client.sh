#!/bin/bash

KCADM='docker compose exec app /opt/keycloak/bin/kcadm.sh'

read -p "Keycloak admin user name: " ADMIN_USER_NAME
read -r -s -p "Keycloak admin user password: " ADMIN_USER_PASSWORD
$KCADM config credentials --server http://localhost:8080 --realm master --user $ADMIN_USER_NAME --password "$ADMIN_USER_PASSWORD"

$KCADM create clients --target-realm htlvb \
    --set 'name=HTLVB HTLUtils' \
    --set clientId=htl-utils \
    --set enabled=true \
    --set rootUrl=https://bro.htlvb.at/ \
    --set baseUrl=https://bro.htlvb.at/ \
    --set "redirectUris=[\"http://localhost:5173/*\", \"https://bro.htlvb.at/*\"]" \
    --set "webOrigins=[\"+\"]" \
    --set publicClient=true \
    --set frontchannelLogout=true \
    --set "attributes.\"frontchannel.logout.url\"=https://bro.htlvb.at/" \
    --set "attributes.\"post.logout.redirect.uris\"=http://localhost:5173/##https://bro.htlvb.at/" \
    --set "attributes.\"pkce.code.challenge.method\"=S256"
CLIENT_ID=`$KCADM get clients --target-realm htlvb --fields id,clientId | jq '.[] | select(.clientId == "htl-utils") | .id' --raw-output`

# Client must be added explicitely as audience because it is skipped by default (see https://github.com/keycloak/keycloak/issues/12415#issuecomment-1571690295)
# HTLUtils instead skips audience validation
# $KCADM create client-scopes --target-realm htlvb \
#     --set name=htlutils-aud \
#     --set "description=Add htl-utils as audience because client isn't added by default." \
#     --set protocol=openid-connect \
#     --set "attributes.\"display.on.consent.screen\"=false" \
#     --set "attributes.\"include.in.token.scope\"=false"
# CLIENT_SCOPE_ID=`$KCADM get client-scopes --target-realm htlvb | jq '.[] | select(.name == "htlutils-aud") | .id' --raw-output`
# $KCADM create \
#     client-scopes/$CLIENT_SCOPE_ID/protocol-mappers/models \
#     --target-realm htlvb

$KCADM create clients/$CLIENT_ID/roles --target-realm htlvb --set name=knowname-user
$KCADM create clients/$CLIENT_ID/roles --target-realm htlvb --set name=knowname-admin

$KCADM add-roles --target-realm htlvb --uusername eggj --cclientid htl-utils --rolename knowname-admin
$KCADM add-roles --target-realm htlvb --gname Lehrer --cclientid htl-utils --rolename knowname-user
