namespace KnowName.Server

open Keycloak.AdminApi
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Http
open Microsoft.Kiota.Abstractions.Authentication
open Microsoft.Kiota.Http.HttpClientLibrary

type KeycloakAccessTokenProvider(hostName: string, httpContextAccessor: IHttpContextAccessor) =
    interface IAccessTokenProvider with
        member _.AllowedHostsValidator = AllowedHostsValidator([hostName])
        member _.GetAuthorizationTokenAsync(uri, additionalAuthenticationContext, cancellationToken) =
            task {
                match Option.ofObj httpContextAccessor.HttpContext with
                | Some context ->
                    let! token = context.GetTokenAsync("access_token")
                    match Option.ofObj token with
                    | Some token -> return token
                    | None -> return failwith "Access token not found."
                | None -> return failwith "HTTP context not available."
            }

type KeycloakAdminApiClientFactory(baseUrl: string, accessTokenProvider: IAccessTokenProvider) =
    member _.CreateClient() =
        async {
            let authProvider = BaseBearerTokenAuthenticationProvider(accessTokenProvider) :> IAuthenticationProvider
            let adapter = new HttpClientRequestAdapter(authProvider, BaseUrl = baseUrl)
            return KeycloakAdminApiClient(adapter)
        }
