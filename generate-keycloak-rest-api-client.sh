#!/bin/bash

dotnet tool restore
dotnet kiota generate \
  --openapi https://www.keycloak.org/docs-api/latest/rest-api/openapi.yaml \
  --language CSharp \
  --output ./src/Keycloak.AdminApi \
  --namespace-name Keycloak.AdminApi \
  --class-name KeycloakAdminApiClient
