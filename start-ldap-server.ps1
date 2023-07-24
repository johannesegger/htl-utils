docker run `
    --rm -it `
    -p 389:389 `
    -p 636:636 `
    -e "SAMBA_DOMAIN=schule" `
    -e "SAMBA_REALM=schule.intern" `
    -e "LDAP_ALLOW_INSECURE=true" `
    javanile/samba-ad-dc
