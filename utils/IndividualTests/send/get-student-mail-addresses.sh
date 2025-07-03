#!/bin/bash

ACCESS_TOKEN=$(az account get-access-token --resource https://graph.microsoft.com --query accessToken -o tsv)

GROUP_ID=$(az ad group list --filter "displayName eq 'GrpSchueler'" --query "[].id" -o tsv)
curl -s -H "Authorization: Bearer $ACCESS_TOKEN" -H "ConsistencyLevel: eventual" \
  "https://graph.microsoft.com/v1.0/groups/$GROUP_ID/members?\$top=999&\$select=department,surname,givenName,mail,extension_0b429365529a4f1ea9337bdcd9346b84_htlvbSokratesId" \
  | jq -r '
    ["ClassName", "LastName", "FirstName", "Mail", "SokratesId"], (.value | sort_by(.department,.surname,.givenName) | .[] | [.department, .surname, .givenName, .mail, .extension_0b429365529a4f1ea9337bdcd9346b84_htlvbSokratesId]) | @csv
  ' > students.csv
