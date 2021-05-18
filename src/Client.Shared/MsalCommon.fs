// ts2fable 0.7.1
module rec MsalCommon
open System
open Fable.Core
open Fable.Core.JS

type Array<'T> = System.Collections.Generic.IList<'T>
type Error = System.Exception
type Required<'T> = 'T
type Record<'T1, 'T2> = 'T1 * 'T2

module PackageMetadata =

    type [<AllowNullLiteral>] IExports =
        abstract name: obj
        abstract version: obj

[<AutoOpen>]
module __account_AccountInfo =

    type [<AllowNullLiteral>] AccountInfo =
        abstract homeAccountId: string with get, set
        abstract environment: string with get, set
        abstract tenantId: string with get, set
        abstract username: string with get, set
        abstract localAccountId: string with get, set
        abstract name: string option with get, set
        abstract idTokenClaims: obj option with get, set

[<AutoOpen>]
module __account_AuthToken =
    type TokenClaims = __account_TokenClaims.TokenClaims
    type ICrypto = __crypto_ICrypto.ICrypto

    type [<AllowNullLiteral>] IExports =
        abstract AuthToken: AuthTokenStatic

    /// JWT Token representation class. Parses token string and generates claims object.
    type [<AllowNullLiteral>] AuthToken =
        abstract rawToken: string with get, set
        abstract claims: TokenClaims with get, set

    /// JWT Token representation class. Parses token string and generates claims object.
    type [<AllowNullLiteral>] AuthTokenStatic =
        [<Emit "new $0($1...)">] abstract Create: rawToken: string * crypto: ICrypto -> AuthToken
        /// Extract token by decoding the rawToken
        abstract extractTokenClaims: encodedToken: string * crypto: ICrypto -> TokenClaims

[<AutoOpen>]
module __account_ClientInfo =
    type ICrypto = __crypto_ICrypto.ICrypto

    type [<AllowNullLiteral>] IExports =
        /// Function to build a client info object
        abstract buildClientInfo: rawClientInfo: string * crypto: ICrypto -> ClientInfo

    type [<AllowNullLiteral>] ClientInfo =
        abstract uid: string with get, set
        abstract utid: string with get, set

[<AutoOpen>]
module __account_DecodedAuthToken =

    /// Interface for Decoded JWT tokens.
    type [<AllowNullLiteral>] DecodedAuthToken =
        abstract header: string with get, set
        abstract JWSPayload: string with get, set
        abstract JWSSig: string with get, set

[<AutoOpen>]
module __account_TokenClaims =

    type [<AllowNullLiteral>] TokenClaims =
        abstract iss: string option with get, set
        abstract oid: string option with get, set
        abstract sub: string option with get, set
        abstract tid: string option with get, set
        abstract ver: string option with get, set
        abstract upn: string option with get, set
        abstract preferred_username: string option with get, set
        abstract emails: ResizeArray<string> option with get, set
        abstract name: string option with get, set
        abstract nonce: string option with get, set
        abstract exp: float option with get, set
        abstract home_oid: string option with get, set
        abstract sid: string option with get, set
        abstract cloud_instance_host_name: string option with get, set
        abstract cnf: TokenClaimsCnf option with get, set
        abstract x5c_ca: string option with get, set

    type [<AllowNullLiteral>] TokenClaimsCnf =
        abstract kid: string with get, set

[<AutoOpen>]
module __authority_Authority =
    type AuthorityType = __authority_AuthorityType.AuthorityType
    type IUri = __url_IUri.IUri
    type INetworkModule = __network_INetworkModule.INetworkModule
    type ProtocolMode = __authority_ProtocolMode.ProtocolMode
    type ICacheManager = __cache_interface_ICacheManager.ICacheManager
    type AuthorityOptions = __authority_AuthorityOptions.AuthorityOptions
    type CloudDiscoveryMetadata = __authority_CloudDiscoveryMetadata.CloudDiscoveryMetadata

    type [<AllowNullLiteral>] IExports =
        abstract Authority: AuthorityStatic

    /// The authority class validates the authority URIs used by the user, and retrieves the OpenID Configuration Data from the
    /// endpoint. It will store the pertinent config data in this object for use during token calls.
    type [<AllowNullLiteral>] Authority =
        abstract networkInterface: INetworkModule with get, set
        abstract cacheManager: ICacheManager with get, set
        // obj
        // obj
        // obj
        // obj
        // obj
        // obj
        // obj
        // obj
        // obj
        // obj
        // obj
        // obj
        // obj
        // obj
        /// Boolean that returns whethr or not tenant discovery has been completed.
        abstract discoveryComplete: unit -> bool
        /// Perform endpoint discovery to discover aliases, preferred_cache, preferred_network
        /// and the /authorize, /token and logout endpoints.
        abstract resolveEndpointsAsync: unit -> Promise<unit>
        /// helper function to generate environment from authority object
        abstract getPreferredCache: unit -> string
        /// Returns whether or not the provided host is an alias of this authority instance
        abstract isAlias: host: string -> bool

    /// The authority class validates the authority URIs used by the user, and retrieves the OpenID Configuration Data from the
    /// endpoint. It will store the pertinent config data in this object for use during token calls.
    type [<AllowNullLiteral>] AuthorityStatic =
        [<Emit "new $0($1...)">] abstract Create: authority: string * networkInterface: INetworkModule * cacheManager: ICacheManager * authorityOptions: AuthorityOptions -> Authority
        /// Creates cloud discovery metadata object from a given host
        abstract createCloudDiscoveryMetadataFromHost: host: string -> CloudDiscoveryMetadata
        /// Searches instance discovery network response for the entry that contains the host in the aliases list
        abstract getCloudDiscoveryMetadataFromNetworkResponse: response: ResizeArray<CloudDiscoveryMetadata> * authority: string -> CloudDiscoveryMetadata option

[<AutoOpen>]
module __authority_AuthorityFactory =
    type Authority = __authority_Authority.Authority
    type INetworkModule = __network_INetworkModule.INetworkModule
    type ICacheManager = __cache_interface_ICacheManager.ICacheManager
    type AuthorityOptions = __authority_AuthorityOptions.AuthorityOptions

    type [<AllowNullLiteral>] IExports =
        abstract AuthorityFactory: AuthorityFactoryStatic

    type [<AllowNullLiteral>] AuthorityFactory =
        interface end

    type [<AllowNullLiteral>] AuthorityFactoryStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> AuthorityFactory
        /// Create an authority object of the correct type based on the url
        /// Performs basic authority validation - checks to see if the authority is of a valid type (i.e. aad, b2c, adfs)
        ///
        /// Also performs endpoint discovery.
        abstract createDiscoveredInstance: authorityUri: string * networkClient: INetworkModule * cacheManager: ICacheManager * authorityOptions: AuthorityOptions -> Promise<Authority>
        /// Create an authority object of the correct type based on the url
        /// Performs basic authority validation - checks to see if the authority is of a valid type (i.e. aad, b2c, adfs)
        ///
        /// Does not perform endpoint discovery.
        abstract createInstance: authorityUrl: string * networkInterface: INetworkModule * cacheManager: ICacheManager * authorityOptions: AuthorityOptions -> Authority

[<AutoOpen>]
module __authority_AuthorityOptions =
    type ProtocolMode = __authority_ProtocolMode.ProtocolMode

    type [<AllowNullLiteral>] AuthorityOptions =
        abstract protocolMode: ProtocolMode with get, set
        abstract knownAuthorities: Array<string> with get, set
        abstract cloudDiscoveryMetadata: string with get, set
        abstract authorityMetadata: string with get, set

[<AutoOpen>]
module __authority_AuthorityType =

    type [<RequireQualifiedAccess>] AuthorityType =
        | Default = 0
        | Adfs = 1

[<AutoOpen>]
module __authority_CloudDiscoveryMetadata =

    type [<AllowNullLiteral>] CloudDiscoveryMetadata =
        abstract preferred_network: string with get, set
        abstract preferred_cache: string with get, set
        abstract aliases: Array<string> with get, set

[<AutoOpen>]
module __authority_CloudInstanceDiscoveryResponse =
    type CloudDiscoveryMetadata = __authority_CloudDiscoveryMetadata.CloudDiscoveryMetadata

    type [<AllowNullLiteral>] IExports =
        abstract isCloudInstanceDiscoveryResponse: response: obj -> bool

    type [<AllowNullLiteral>] CloudInstanceDiscoveryResponse =
        abstract tenant_discovery_endpoint: string with get, set
        abstract metadata: Array<CloudDiscoveryMetadata> with get, set

[<AutoOpen>]
module __authority_OpenIdConfigResponse =

    type [<AllowNullLiteral>] IExports =
        abstract isOpenIdConfigResponse: response: obj -> bool

    type [<AllowNullLiteral>] OpenIdConfigResponse =
        abstract authorization_endpoint: string with get, set
        abstract token_endpoint: string with get, set
        abstract end_session_endpoint: string with get, set
        abstract issuer: string with get, set

[<AutoOpen>]
module __authority_ProtocolMode =

    type [<StringEnum>] [<RequireQualifiedAccess>] ProtocolMode =
        | [<CompiledName "AAD">] AAD
        | [<CompiledName "OIDC">] OIDC

[<AutoOpen>]
module __cache_CacheManager =
    type AccountCache = __cache_utils_CacheTypes.AccountCache
    type AccountFilter = __cache_utils_CacheTypes.AccountFilter
    type CredentialFilter = __cache_utils_CacheTypes.CredentialFilter
    type CredentialCache = __cache_utils_CacheTypes.CredentialCache
    type AppMetadataFilter = __cache_utils_CacheTypes.AppMetadataFilter
    type AppMetadataCache = __cache_utils_CacheTypes.AppMetadataCache
    type CacheRecord = __cache_entities_CacheRecord.CacheRecord
    type AuthenticationScheme = __utils_Constants.AuthenticationScheme
    type CredentialEntity = __cache_entities_CredentialEntity.CredentialEntity
    type ScopeSet = __request_ScopeSet.ScopeSet
    type AccountEntity = __cache_entities_AccountEntity.AccountEntity
    type AccessTokenEntity = __cache_entities_AccessTokenEntity.AccessTokenEntity
    type IdTokenEntity = __cache_entities_IdTokenEntity.IdTokenEntity
    type RefreshTokenEntity = __cache_entities_RefreshTokenEntity.RefreshTokenEntity
    type ICacheManager = __cache_interface_ICacheManager.ICacheManager
    type AccountInfo = __account_AccountInfo.AccountInfo
    type AppMetadataEntity = __cache_entities_AppMetadataEntity.AppMetadataEntity
    type ServerTelemetryEntity = __cache_entities_ServerTelemetryEntity.ServerTelemetryEntity
    type ThrottlingEntity = __cache_entities_ThrottlingEntity.ThrottlingEntity
    type ICrypto = __crypto_ICrypto.ICrypto
    type AuthorityMetadataEntity = __cache_entities_AuthorityMetadataEntity.AuthorityMetadataEntity

    type [<AllowNullLiteral>] IExports =
        abstract CacheManager: CacheManagerStatic
        abstract DefaultStorageClass: DefaultStorageClassStatic

    /// Interface class which implement cache storage functions used by MSAL to perform validity checks, and store tokens.
    type [<AllowNullLiteral>] CacheManager =
        inherit ICacheManager
        abstract clientId: string with get, set
        abstract cryptoImpl: ICrypto with get, set
        /// fetch the account entity from the platform cache
        abstract getAccount: accountKey: string -> AccountEntity option
        /// set account entity in the platform cache
        abstract setAccount: account: AccountEntity -> unit
        /// fetch the idToken entity from the platform cache
        abstract getIdTokenCredential: idTokenKey: string -> IdTokenEntity option
        /// set idToken entity to the platform cache
        abstract setIdTokenCredential: idToken: IdTokenEntity -> unit
        /// fetch the idToken entity from the platform cache
        abstract getAccessTokenCredential: accessTokenKey: string -> AccessTokenEntity option
        /// set idToken entity to the platform cache
        abstract setAccessTokenCredential: accessToken: AccessTokenEntity -> unit
        /// fetch the idToken entity from the platform cache
        abstract getRefreshTokenCredential: refreshTokenKey: string -> RefreshTokenEntity option
        /// set idToken entity to the platform cache
        abstract setRefreshTokenCredential: refreshToken: RefreshTokenEntity -> unit
        /// fetch appMetadata entity from the platform cache
        abstract getAppMetadata: appMetadataKey: string -> AppMetadataEntity option
        /// set appMetadata entity to the platform cache
        abstract setAppMetadata: appMetadata: AppMetadataEntity -> unit
        /// fetch server telemetry entity from the platform cache
        abstract getServerTelemetry: serverTelemetryKey: string -> ServerTelemetryEntity option
        /// set server telemetry entity to the platform cache
        abstract setServerTelemetry: serverTelemetryKey: string * serverTelemetry: ServerTelemetryEntity -> unit
        /// fetch cloud discovery metadata entity from the platform cache
        abstract getAuthorityMetadata: key: string -> AuthorityMetadataEntity option
        abstract getAuthorityMetadataKeys: unit -> Array<string>
        /// set cloud discovery metadata entity to the platform cache
        abstract setAuthorityMetadata: key: string * value: AuthorityMetadataEntity -> unit
        /// fetch throttling entity from the platform cache
        abstract getThrottlingCache: throttlingCacheKey: string -> ThrottlingEntity option
        /// set throttling entity to the platform cache
        abstract setThrottlingCache: throttlingCacheKey: string * throttlingCache: ThrottlingEntity -> unit
        /// Function to remove an item from cache given its key.
        abstract removeItem: key: string * ?``type``: string -> bool
        /// Function which returns boolean whether cache contains a specific key.
        abstract containsKey: key: string * ?``type``: string -> bool
        /// Function which retrieves all current keys from the cache.
        abstract getKeys: unit -> ResizeArray<string>
        /// Function which clears cache.
        abstract clear: unit -> unit
        /// Returns all accounts in cache
        abstract getAllAccounts: unit -> ResizeArray<AccountInfo>
        /// saves a cache record
        abstract saveCacheRecord: cacheRecord: CacheRecord -> unit
        /// retrieve accounts matching all provided filters; if no filter is set, get all accounts
        /// not checking for casing as keys are all generated in lower case, remember to convert to lower case if object properties are compared
        abstract getAccountsFilteredBy: ?accountFilter: AccountFilter -> AccountCache
        /// retrieve credentails matching all provided filters; if no filter is set, get all credentials
        abstract getCredentialsFilteredBy: filter: CredentialFilter -> CredentialCache
        /// retrieve appMetadata matching all provided filters; if no filter is set, get all appMetadata
        abstract getAppMetadataFilteredBy: filter: AppMetadataFilter -> AppMetadataCache
        /// retrieve authorityMetadata that contains a matching alias
        abstract getAuthorityMetadataByAlias: host: string -> AuthorityMetadataEntity option
        /// Removes all accounts and related tokens from cache.
        abstract removeAllAccounts: unit -> bool
        /// returns a boolean if the given account is removed
        abstract removeAccount: accountKey: string -> bool
        /// returns a boolean if the given account is removed
        abstract removeAccountContext: account: AccountEntity -> bool
        /// returns a boolean if the given credential is removed
        abstract removeCredential: credential: CredentialEntity -> bool
        /// Removes all app metadata objects from cache.
        abstract removeAppMetadata: unit -> bool
        /// Retrieve the cached credentials into a cacherecord
        abstract readCacheRecord: account: AccountInfo * clientId: string * scopes: ScopeSet * environment: string * authScheme: AuthenticationScheme -> CacheRecord
        /// Retrieve AccountEntity from cache
        abstract readAccountFromCache: account: AccountInfo -> AccountEntity option
        /// Retrieve IdTokenEntity from cache
        abstract readIdTokenFromCache: clientId: string * account: AccountInfo -> IdTokenEntity option
        /// Retrieve AccessTokenEntity from cache
        abstract readAccessTokenFromCache: clientId: string * account: AccountInfo * scopes: ScopeSet * authScheme: AuthenticationScheme -> AccessTokenEntity option
        /// Helper to retrieve the appropriate refresh token from cache
        abstract readRefreshTokenFromCache: clientId: string * account: AccountInfo * familyRT: bool -> RefreshTokenEntity option
        /// Retrieve AppMetadataEntity from cache
        abstract readAppMetadataFromCache: environment: string * clientId: string -> AppMetadataEntity option
        /// Return the family_id value associated  with FOCI
        abstract isAppMetadataFOCI: environment: string * clientId: string -> bool
        /// returns if a given cache entity is of the type authoritymetadata
        abstract isAuthorityMetadata: key: string -> bool
        /// returns cache key used for cloud instance metadata
        abstract generateAuthorityMetadataCacheKey: authority: string -> string

    /// Interface class which implement cache storage functions used by MSAL to perform validity checks, and store tokens.
    type [<AllowNullLiteral>] CacheManagerStatic =
        [<Emit "new $0($1...)">] abstract Create: clientId: string * cryptoImpl: ICrypto -> CacheManager
        /// Helper to convert serialized data to object
        abstract toObject: obj: 'T * json: obj -> 'T

    type [<AllowNullLiteral>] DefaultStorageClass =
        inherit CacheManager
        /// set account entity in the platform cache
        abstract setAccount: unit -> unit
        /// fetch the account entity from the platform cache
        abstract getAccount: unit -> AccountEntity
        /// set idToken entity to the platform cache
        abstract setIdTokenCredential: unit -> unit
        /// fetch the idToken entity from the platform cache
        abstract getIdTokenCredential: unit -> IdTokenEntity
        /// set idToken entity to the platform cache
        abstract setAccessTokenCredential: unit -> unit
        /// fetch the idToken entity from the platform cache
        abstract getAccessTokenCredential: unit -> AccessTokenEntity
        /// set idToken entity to the platform cache
        abstract setRefreshTokenCredential: unit -> unit
        /// fetch the idToken entity from the platform cache
        abstract getRefreshTokenCredential: unit -> RefreshTokenEntity
        /// set appMetadata entity to the platform cache
        abstract setAppMetadata: unit -> unit
        /// fetch appMetadata entity from the platform cache
        abstract getAppMetadata: unit -> AppMetadataEntity
        /// set server telemetry entity to the platform cache
        abstract setServerTelemetry: unit -> unit
        /// fetch server telemetry entity from the platform cache
        abstract getServerTelemetry: unit -> ServerTelemetryEntity
        /// set cloud discovery metadata entity to the platform cache
        abstract setAuthorityMetadata: unit -> unit
        /// fetch cloud discovery metadata entity from the platform cache
        abstract getAuthorityMetadata: unit -> AuthorityMetadataEntity option
        abstract getAuthorityMetadataKeys: unit -> Array<string>
        /// set throttling entity to the platform cache
        abstract setThrottlingCache: unit -> unit
        /// fetch throttling entity from the platform cache
        abstract getThrottlingCache: unit -> ThrottlingEntity
        /// Function to remove an item from cache given its key.
        abstract removeItem: unit -> bool
        /// Function which returns boolean whether cache contains a specific key.
        abstract containsKey: unit -> bool
        /// Function which retrieves all current keys from the cache.
        abstract getKeys: unit -> ResizeArray<string>
        /// Function which clears cache.
        abstract clear: unit -> unit

    type [<AllowNullLiteral>] DefaultStorageClassStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> DefaultStorageClass

[<AutoOpen>]
module __client_AuthorizationCodeClient =
    type BaseClient = __client_BaseClient.BaseClient
    type CommonAuthorizationUrlRequest = __request_CommonAuthorizationUrlRequest.CommonAuthorizationUrlRequest
    type CommonAuthorizationCodeRequest = __request_CommonAuthorizationCodeRequest.CommonAuthorizationCodeRequest
    type ClientConfiguration = __config_ClientConfiguration.ClientConfiguration
    type AuthenticationResult = __response_AuthenticationResult.AuthenticationResult
    type CommonEndSessionRequest = __request_CommonEndSessionRequest.CommonEndSessionRequest
    type AuthorizationCodePayload = __response_AuthorizationCodePayload.AuthorizationCodePayload

    type [<AllowNullLiteral>] IExports =
        abstract AuthorizationCodeClient: AuthorizationCodeClientStatic

    /// Oauth2.0 Authorization Code client
    type [<AllowNullLiteral>] AuthorizationCodeClient =
        inherit BaseClient
        /// Creates the URL of the authorization request letting the user input credentials and consent to the
        /// application. The URL target the /authorize endpoint of the authority configured in the
        /// application object.
        ///
        /// Once the user inputs their credentials and consents, the authority will send a response to the redirect URI
        /// sent in the request and should contain an authorization code, which can then be used to acquire tokens via
        /// acquireToken(AuthorizationCodeRequest)
        abstract getAuthCodeUrl: request: CommonAuthorizationUrlRequest -> Promise<string>
        /// API to acquire a token in exchange of 'authorization_code` acquired by the user in the first leg of the
        /// authorization_code_grant
        abstract acquireToken: request: CommonAuthorizationCodeRequest * ?authCodePayload: AuthorizationCodePayload -> Promise<AuthenticationResult>
        /// Handles the hash fragment response from public client code request. Returns a code response used by
        /// the client to exchange for a token in acquireToken.
        abstract handleFragmentResponse: hashFragment: string * cachedState: string -> AuthorizationCodePayload
        /// Use to log out the current user, and redirect the user to the postLogoutRedirectUri.
        /// Default behaviour is to redirect the user to `window.location.href`.
        abstract getLogoutUri: logoutRequest: CommonEndSessionRequest -> string

    /// Oauth2.0 Authorization Code client
    type [<AllowNullLiteral>] AuthorizationCodeClientStatic =
        [<Emit "new $0($1...)">] abstract Create: configuration: ClientConfiguration -> AuthorizationCodeClient

[<AutoOpen>]
module __client_BaseClient =
    type ClientConfiguration = __config_ClientConfiguration.ClientConfiguration
    type CommonClientConfiguration = __config_ClientConfiguration.CommonClientConfiguration
    type INetworkModule = __network_INetworkModule.INetworkModule
    type NetworkManager = __network_NetworkManager.NetworkManager
    type NetworkResponse<'T> = __network_NetworkManager.NetworkResponse<'T>
    type ICrypto = __crypto_ICrypto.ICrypto
    type Authority = __authority_Authority.Authority
    type Logger = __logger_Logger.Logger
    type ServerAuthorizationTokenResponse = __response_ServerAuthorizationTokenResponse.ServerAuthorizationTokenResponse
    type CacheManager = __cache_CacheManager.CacheManager
    type ServerTelemetryManager = __telemetry_server_ServerTelemetryManager.ServerTelemetryManager
    type RequestThumbprint = __network_RequestThumbprint.RequestThumbprint

    type [<AllowNullLiteral>] IExports =
        abstract BaseClient: BaseClientStatic

    /// Base application class which will construct requests to send to and handle responses from the Microsoft STS using the authorization code flow.
    type [<AllowNullLiteral>] BaseClient =
        abstract logger: Logger with get, set
        abstract config: CommonClientConfiguration with get, set
        abstract cryptoUtils: ICrypto with get, set
        abstract cacheManager: CacheManager with get, set
        abstract networkClient: INetworkModule with get, set
        abstract serverTelemetryManager: ServerTelemetryManager option with get, set
        abstract networkManager: NetworkManager with get, set
        abstract authority: Authority with get, set
        /// Creates default headers for requests to token endpoint
        abstract createDefaultTokenRequestHeaders: unit -> Record<string, string>
        /// addLibraryData
        abstract createDefaultLibraryHeaders: unit -> Record<string, string>
        /// Http post to token endpoint
        abstract executePostToTokenEndpoint: tokenEndpoint: string * queryString: string * headers: Record<string, string> * thumbprint: RequestThumbprint -> Promise<NetworkResponse<ServerAuthorizationTokenResponse>>
        /// Updates the authority object of the client. Endpoint discovery must be completed.
        abstract updateAuthority: updatedAuthority: Authority -> unit

    /// Base application class which will construct requests to send to and handle responses from the Microsoft STS using the authorization code flow.
    type [<AllowNullLiteral>] BaseClientStatic =
        [<Emit "new $0($1...)">] abstract Create: configuration: ClientConfiguration -> BaseClient

[<AutoOpen>]
module __client_ClientCredentialClient =
    type ClientConfiguration = __config_ClientConfiguration.ClientConfiguration
    type BaseClient = __client_BaseClient.BaseClient
    type AuthenticationResult = __response_AuthenticationResult.AuthenticationResult
    type CommonClientCredentialRequest = __request_CommonClientCredentialRequest.CommonClientCredentialRequest

    type [<AllowNullLiteral>] IExports =
        abstract ClientCredentialClient: ClientCredentialClientStatic

    /// OAuth2.0 client credential grant
    type [<AllowNullLiteral>] ClientCredentialClient =
        inherit BaseClient
        /// Public API to acquire a token with ClientCredential Flow for Confidential clients
        abstract acquireToken: request: CommonClientCredentialRequest -> Promise<AuthenticationResult option>

    /// OAuth2.0 client credential grant
    type [<AllowNullLiteral>] ClientCredentialClientStatic =
        [<Emit "new $0($1...)">] abstract Create: configuration: ClientConfiguration -> ClientCredentialClient

[<AutoOpen>]
module __client_DeviceCodeClient =
    type BaseClient = __client_BaseClient.BaseClient
    type CommonDeviceCodeRequest = __request_CommonDeviceCodeRequest.CommonDeviceCodeRequest
    type ClientConfiguration = __config_ClientConfiguration.ClientConfiguration
    type AuthenticationResult = __response_AuthenticationResult.AuthenticationResult

    type [<AllowNullLiteral>] IExports =
        abstract DeviceCodeClient: DeviceCodeClientStatic

    /// OAuth2.0 Device code client
    type [<AllowNullLiteral>] DeviceCodeClient =
        inherit BaseClient
        /// Gets device code from device code endpoint, calls back to with device code response, and
        /// polls token endpoint to exchange device code for tokens
        abstract acquireToken: request: CommonDeviceCodeRequest -> Promise<AuthenticationResult option>

    /// OAuth2.0 Device code client
    type [<AllowNullLiteral>] DeviceCodeClientStatic =
        [<Emit "new $0($1...)">] abstract Create: configuration: ClientConfiguration -> DeviceCodeClient

[<AutoOpen>]
module __client_OnBehalfOfClient =
    type ClientConfiguration = __config_ClientConfiguration.ClientConfiguration
    type BaseClient = __client_BaseClient.BaseClient
    type AuthenticationResult = __response_AuthenticationResult.AuthenticationResult
    type CommonOnBehalfOfRequest = __request_CommonOnBehalfOfRequest.CommonOnBehalfOfRequest

    type [<AllowNullLiteral>] IExports =
        abstract OnBehalfOfClient: OnBehalfOfClientStatic

    /// On-Behalf-Of client
    type [<AllowNullLiteral>] OnBehalfOfClient =
        inherit BaseClient
        /// Public API to acquire tokens with on behalf of flow
        abstract acquireToken: request: CommonOnBehalfOfRequest -> Promise<AuthenticationResult option>

    /// On-Behalf-Of client
    type [<AllowNullLiteral>] OnBehalfOfClientStatic =
        [<Emit "new $0($1...)">] abstract Create: configuration: ClientConfiguration -> OnBehalfOfClient

[<AutoOpen>]
module __client_RefreshTokenClient =
    type ClientConfiguration = __config_ClientConfiguration.ClientConfiguration
    type BaseClient = __client_BaseClient.BaseClient
    type CommonRefreshTokenRequest = __request_CommonRefreshTokenRequest.CommonRefreshTokenRequest
    type AuthenticationResult = __response_AuthenticationResult.AuthenticationResult
    type CommonSilentFlowRequest = __request_CommonSilentFlowRequest.CommonSilentFlowRequest

    type [<AllowNullLiteral>] IExports =
        abstract RefreshTokenClient: RefreshTokenClientStatic

    /// OAuth2.0 refresh token client
    type [<AllowNullLiteral>] RefreshTokenClient =
        inherit BaseClient
        abstract acquireToken: request: CommonRefreshTokenRequest -> Promise<AuthenticationResult>
        /// Gets cached refresh token and attaches to request, then calls acquireToken API
        abstract acquireTokenByRefreshToken: request: CommonSilentFlowRequest -> Promise<AuthenticationResult>

    /// OAuth2.0 refresh token client
    type [<AllowNullLiteral>] RefreshTokenClientStatic =
        [<Emit "new $0($1...)">] abstract Create: configuration: ClientConfiguration -> RefreshTokenClient

[<AutoOpen>]
module __client_SilentFlowClient =
    type BaseClient = __client_BaseClient.BaseClient
    type ClientConfiguration = __config_ClientConfiguration.ClientConfiguration
    type CommonSilentFlowRequest = __request_CommonSilentFlowRequest.CommonSilentFlowRequest
    type AuthenticationResult = __response_AuthenticationResult.AuthenticationResult

    type [<AllowNullLiteral>] IExports =
        abstract SilentFlowClient: SilentFlowClientStatic

    type [<AllowNullLiteral>] SilentFlowClient =
        inherit BaseClient
        /// Retrieves a token from cache if it is still valid, or uses the cached refresh token to renew
        /// the given token and returns the renewed token
        abstract acquireToken: request: CommonSilentFlowRequest -> Promise<AuthenticationResult>
        /// Retrieves token from cache or throws an error if it must be refreshed.
        abstract acquireCachedToken: request: CommonSilentFlowRequest -> Promise<AuthenticationResult>

    type [<AllowNullLiteral>] SilentFlowClientStatic =
        [<Emit "new $0($1...)">] abstract Create: configuration: ClientConfiguration -> SilentFlowClient

[<AutoOpen>]
module __client_UsernamePasswordClient =
    type BaseClient = __client_BaseClient.BaseClient
    type ClientConfiguration = __config_ClientConfiguration.ClientConfiguration
    type CommonUsernamePasswordRequest = __request_CommonUsernamePasswordRequest.CommonUsernamePasswordRequest
    type AuthenticationResult = __response_AuthenticationResult.AuthenticationResult

    type [<AllowNullLiteral>] IExports =
        abstract UsernamePasswordClient: UsernamePasswordClientStatic

    /// Oauth2.0 Password grant client
    /// Note: We are only supporting public clients for password grant and for purely testing purposes
    type [<AllowNullLiteral>] UsernamePasswordClient =
        inherit BaseClient
        /// API to acquire a token by passing the username and password to the service in exchage of credentials
        /// password_grant
        abstract acquireToken: request: CommonUsernamePasswordRequest -> Promise<AuthenticationResult option>

    /// Oauth2.0 Password grant client
    /// Note: We are only supporting public clients for password grant and for purely testing purposes
    type [<AllowNullLiteral>] UsernamePasswordClientStatic =
        [<Emit "new $0($1...)">] abstract Create: configuration: ClientConfiguration -> UsernamePasswordClient

[<AutoOpen>]
module __config_ClientConfiguration =
    type INetworkModule = __network_INetworkModule.INetworkModule
    type ICrypto = __crypto_ICrypto.ICrypto
    type ILoggerCallback = __logger_Logger.ILoggerCallback
    type LogLevel = __logger_Logger.LogLevel
    type Authority = __authority_Authority.Authority
    type CacheManager = __cache_CacheManager.CacheManager
    type ServerTelemetryManager = __telemetry_server_ServerTelemetryManager.ServerTelemetryManager
    type ICachePlugin = __cache_interface_ICachePlugin.ICachePlugin
    type ISerializableTokenCache = __cache_interface_ISerializableTokenCache.ISerializableTokenCache

    type [<AllowNullLiteral>] IExports =
        abstract DEFAULT_SYSTEM_OPTIONS: Required<SystemOptions>
        /// Function that sets the default options when not explicitly configured from app developer
        abstract buildClientConfiguration: p0: ClientConfiguration -> CommonClientConfiguration

    type [<AllowNullLiteral>] ClientConfiguration =
        abstract authOptions: AuthOptions with get, set
        abstract systemOptions: SystemOptions option with get, set
        abstract loggerOptions: LoggerOptions option with get, set
        abstract storageInterface: CacheManager option with get, set
        abstract networkInterface: INetworkModule option with get, set
        abstract cryptoInterface: ICrypto option with get, set
        abstract clientCredentials: ClientCredentials option with get, set
        abstract libraryInfo: LibraryInfo option with get, set
        abstract serverTelemetryManager: ServerTelemetryManager option with get, set
        abstract persistencePlugin: ICachePlugin option with get, set
        abstract serializableCache: ISerializableTokenCache option with get, set

    type [<AllowNullLiteral>] CommonClientConfiguration =
        abstract authOptions: Required<AuthOptions> with get, set
        abstract systemOptions: Required<SystemOptions> with get, set
        abstract loggerOptions: Required<LoggerOptions> with get, set
        abstract storageInterface: CacheManager with get, set
        abstract networkInterface: INetworkModule with get, set
        abstract cryptoInterface: Required<ICrypto> with get, set
        abstract libraryInfo: LibraryInfo with get, set
        abstract serverTelemetryManager: ServerTelemetryManager option with get, set
        abstract clientCredentials: ClientCredentials with get, set
        abstract persistencePlugin: ICachePlugin option with get, set
        abstract serializableCache: ISerializableTokenCache option with get, set

    type [<AllowNullLiteral>] AuthOptions =
        abstract clientId: string with get, set
        abstract authority: Authority with get, set
        abstract clientCapabilities: Array<string> option with get, set

    type [<AllowNullLiteral>] SystemOptions =
        abstract tokenRenewalOffsetSeconds: float option with get, set

    type [<AllowNullLiteral>] LoggerOptions =
        abstract loggerCallback: ILoggerCallback option with get, set
        abstract piiLoggingEnabled: bool option with get, set
        abstract logLevel: LogLevel option with get, set

    type [<AllowNullLiteral>] LibraryInfo =
        abstract sku: string with get, set
        abstract version: string with get, set
        abstract cpu: string with get, set
        abstract os: string with get, set

    type [<AllowNullLiteral>] ClientCredentials =
        abstract clientSecret: string option with get, set
        abstract clientAssertion: ClientCredentialsClientAssertion option with get, set

    type [<AllowNullLiteral>] ClientCredentialsClientAssertion =
        abstract assertion: string with get, set
        abstract assertionType: string with get, set

[<AutoOpen>]
module __crypto_ICrypto =
    type BaseAuthRequest = __request_BaseAuthRequest.BaseAuthRequest
    type SignedHttpRequest = __crypto_SignedHttpRequest.SignedHttpRequest

    type [<AllowNullLiteral>] IExports =
        abstract DEFAULT_CRYPTO_IMPLEMENTATION: ICrypto

    type [<AllowNullLiteral>] PkceCodes =
        abstract verifier: string with get, set
        abstract challenge: string with get, set

    /// Interface for crypto functions used by library
    type [<AllowNullLiteral>] ICrypto =
        /// Creates a guid randomly.
        abstract createNewGuid: unit -> string
        /// base64 Encode string
        abstract base64Encode: input: string -> string
        /// base64 decode string
        abstract base64Decode: input: string -> string
        /// Generate PKCE codes for OAuth. See RFC here: https://tools.ietf.org/html/rfc7636
        abstract generatePkceCodes: unit -> Promise<PkceCodes>
        /// Generates an JWK RSA S256 Thumbprint
        abstract getPublicKeyThumbprint: request: BaseAuthRequest -> Promise<string>
        /// Returns a signed proof-of-possession token with a given acces token that contains a cnf claim with the required kid.
        abstract signJwt: payload: SignedHttpRequest * kid: string -> Promise<string>

[<AutoOpen>]
module __crypto_PopTokenGenerator =
    type ICrypto = __crypto_ICrypto.ICrypto
    type BaseAuthRequest = __request_BaseAuthRequest.BaseAuthRequest

    type [<AllowNullLiteral>] IExports =
        abstract PopTokenGenerator: PopTokenGeneratorStatic

    type [<AllowNullLiteral>] PopTokenGenerator =
        abstract generateCnf: request: BaseAuthRequest -> Promise<string>
        abstract signPopToken: accessToken: string * request: BaseAuthRequest -> Promise<string>

    type [<AllowNullLiteral>] PopTokenGeneratorStatic =
        [<Emit "new $0($1...)">] abstract Create: cryptoUtils: ICrypto -> PopTokenGenerator

[<AutoOpen>]
module __crypto_SignedHttpRequest =

    type [<AllowNullLiteral>] SignedHttpRequest =
        abstract at: string option with get, set
        abstract cnf: obj option with get, set
        abstract m: string option with get, set
        abstract u: string option with get, set
        abstract p: string option with get, set
        abstract q: Array<string> * string option with get, set
        abstract ts: string option with get, set
        abstract nonce: string option with get, set
        abstract client_claims: string option with get, set

[<AutoOpen>]
module __error_AuthError =

    type [<AllowNullLiteral>] IExports =
        abstract AuthErrorMessage: IExportsAuthErrorMessage
        abstract AuthError: AuthErrorStatic

    /// General error class thrown by the MSAL.js library.
    [<AbstractClass>]
    type [<AllowNullLiteral>] AuthError =
        inherit Error
        /// Short string denoting error
        abstract errorCode: string with get, set
        /// Detailed description of error
        abstract errorMessage: string with get, set
        /// Describes the subclass of an error
        abstract subError: string with get, set

    /// General error class thrown by the MSAL.js library.
    type [<AllowNullLiteral>] AuthErrorStatic =
        [<Emit "new $0($1...)">] abstract Create: ?errorCode: string * ?errorMessage: string * ?suberror: string -> AuthError
        /// Creates an error that is thrown when something unexpected happens in the library.
        abstract createUnexpectedError: errDesc: string -> AuthError

    type [<AllowNullLiteral>] IExportsAuthErrorMessageUnexpectedError =
        abstract code: string with get, set
        abstract desc: string with get, set

    type [<AllowNullLiteral>] IExportsAuthErrorMessage =
        abstract unexpectedError: IExportsAuthErrorMessageUnexpectedError with get, set

[<AutoOpen>]
module __error_ClientAuthError =
    type AuthError = __error_AuthError.AuthError
    type ScopeSet = __request_ScopeSet.ScopeSet

    type [<AllowNullLiteral>] IExports =
        abstract ClientAuthErrorMessage: IExportsClientAuthErrorMessage
        abstract ClientAuthError: ClientAuthErrorStatic

    /// Error thrown when there is an error in the client code running on the browser.
    [<AbstractClass>]
    type [<AllowNullLiteral>] ClientAuthError =
        inherit AuthError

    /// Error thrown when there is an error in the client code running on the browser.
    type [<AllowNullLiteral>] ClientAuthErrorStatic =
        [<Emit "new $0($1...)">] abstract Create: errorCode: string * ?errorMessage: string -> ClientAuthError
        /// Creates an error thrown when client info object doesn't decode correctly.
        abstract createClientInfoDecodingError: caughtError: string -> ClientAuthError
        /// Creates an error thrown if the client info is empty.
        abstract createClientInfoEmptyError: unit -> ClientAuthError
        /// Creates an error thrown when the id token extraction errors out.
        abstract createTokenParsingError: caughtExtractionError: string -> ClientAuthError
        /// Creates an error thrown when the id token string is null or empty.
        abstract createTokenNullOrEmptyError: invalidRawTokenString: string -> ClientAuthError
        /// Creates an error thrown when the endpoint discovery doesn't complete correctly.
        abstract createEndpointDiscoveryIncompleteError: errDetail: string -> ClientAuthError
        /// Creates an error thrown when the fetch client throws
        abstract createNetworkError: endpoint: string * errDetail: string -> ClientAuthError
        /// Creates an error thrown when the openid-configuration endpoint cannot be reached or does not contain the required data
        abstract createUnableToGetOpenidConfigError: errDetail: string -> ClientAuthError
        /// Creates an error thrown when the hash cannot be deserialized.
        abstract createHashNotDeserializedError: hashParamObj: string -> ClientAuthError
        /// Creates an error thrown when the state cannot be parsed.
        abstract createInvalidStateError: invalidState: string * ?errorString: string -> ClientAuthError
        /// Creates an error thrown when two states do not match.
        abstract createStateMismatchError: unit -> ClientAuthError
        /// Creates an error thrown when the state is not present
        abstract createStateNotFoundError: missingState: string -> ClientAuthError
        /// Creates an error thrown when the nonce does not match.
        abstract createNonceMismatchError: unit -> ClientAuthError
        /// Creates an error thrown when the mnonce is not present
        abstract createNonceNotFoundError: missingNonce: string -> ClientAuthError
        /// Creates an error thrown when the authorization code required for a token request is null or empty.
        abstract createNoTokensFoundError: unit -> ClientAuthError
        /// Throws error when multiple tokens are in cache.
        abstract createMultipleMatchingTokensInCacheError: unit -> ClientAuthError
        /// Throws error when multiple accounts are in cache for the given params
        abstract createMultipleMatchingAccountsInCacheError: unit -> ClientAuthError
        /// Throws error when multiple appMetada are in cache for the given clientId.
        abstract createMultipleMatchingAppMetadataInCacheError: unit -> ClientAuthError
        /// Throws error when no auth code or refresh token is given to ServerTokenRequestParameters.
        abstract createTokenRequestCannotBeMadeError: unit -> ClientAuthError
        /// Throws error when attempting to append a null, undefined or empty scope to a set
        abstract createAppendEmptyScopeToSetError: givenScope: string -> ClientAuthError
        /// Throws error when attempting to append a null, undefined or empty scope to a set
        abstract createRemoveEmptyScopeFromSetError: givenScope: string -> ClientAuthError
        /// Throws error when attempting to append null or empty ScopeSet.
        abstract createAppendScopeSetError: appendError: string -> ClientAuthError
        /// Throws error if ScopeSet is null or undefined.
        abstract createEmptyInputScopeSetError: givenScopeSet: ScopeSet -> ClientAuthError
        /// Throws error if user sets CancellationToken.cancel = true during polling of token endpoint during device code flow
        abstract createDeviceCodeCancelledError: unit -> ClientAuthError
        /// Throws error if device code is expired
        abstract createDeviceCodeExpiredError: unit -> ClientAuthError
        /// Throws error when silent requests are made without an account object
        abstract createNoAccountInSilentRequestError: unit -> ClientAuthError
        /// Throws error when cache record is null or undefined.
        abstract createNullOrUndefinedCacheRecord: unit -> ClientAuthError
        /// Throws error when provided environment is not part of the CloudDiscoveryMetadata object
        abstract createInvalidCacheEnvironmentError: unit -> ClientAuthError
        /// Throws error when account is not found in cache.
        abstract createNoAccountFoundError: unit -> ClientAuthError
        /// Throws error if ICachePlugin not set on CacheManager.
        abstract createCachePluginError: unit -> ClientAuthError
        /// Throws error if crypto object not found.
        abstract createNoCryptoObjectError: operationName: string -> ClientAuthError
        /// Throws error if cache type is invalid.
        abstract createInvalidCacheTypeError: unit -> ClientAuthError
        /// Throws error if unexpected account type.
        abstract createUnexpectedAccountTypeError: unit -> ClientAuthError
        /// Throws error if unexpected credential type.
        abstract createUnexpectedCredentialTypeError: unit -> ClientAuthError
        /// Throws error if client assertion is not valid.
        abstract createInvalidAssertionError: unit -> ClientAuthError
        /// Throws error if client assertion is not valid.
        abstract createInvalidCredentialError: unit -> ClientAuthError
        /// Throws error if token cannot be retrieved from cache due to refresh being required.
        abstract createRefreshRequiredError: unit -> ClientAuthError
        /// Throws error if the user defined timeout is reached.
        abstract createUserTimeoutReachedError: unit -> ClientAuthError
        abstract createTokenClaimsRequiredError: unit -> ClientAuthError
        /// Throws error when the authorization code is missing from the server response
        abstract createNoAuthCodeInServerResponseError: unit -> ClientAuthError

    type [<AllowNullLiteral>] IExportsClientAuthErrorMessageClientInfoDecodingError =
        abstract code: string with get, set
        abstract desc: string with get, set

    type [<AllowNullLiteral>] IExportsClientAuthErrorMessage =
        abstract clientInfoDecodingError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract clientInfoEmptyError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract tokenParsingError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract nullOrEmptyToken: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract endpointResolutionError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract networkError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract unableToGetOpenidConfigError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract hashNotDeserialized: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract blankGuidGenerated: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract invalidStateError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract stateMismatchError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract stateNotFoundError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract nonceMismatchError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract nonceNotFoundError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract noTokensFoundError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract multipleMatchingTokens: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract multipleMatchingAccounts: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract multipleMatchingAppMetadata: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract tokenRequestCannotBeMade: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract appendEmptyScopeError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract removeEmptyScopeError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract appendScopeSetError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract emptyInputScopeSetError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract DeviceCodePollingCancelled: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract DeviceCodeExpired: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract NoAccountInSilentRequest: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract invalidCacheRecord: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract invalidCacheEnvironment: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract noAccountFound: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract CachePluginError: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract noCryptoObj: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract invalidCacheType: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract unexpectedAccountType: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract unexpectedCredentialType: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract invalidAssertion: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract invalidClientCredential: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract tokenRefreshRequired: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract userTimeoutReached: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract tokenClaimsRequired: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set
        abstract noAuthorizationCodeFromServer: IExportsClientAuthErrorMessageClientInfoDecodingError with get, set

[<AutoOpen>]
module __error_ClientConfigurationError =
    type ClientAuthError = __error_ClientAuthError.ClientAuthError

    type [<AllowNullLiteral>] IExports =
        abstract ClientConfigurationErrorMessage: IExportsClientConfigurationErrorMessage
        abstract ClientConfigurationError: ClientConfigurationErrorStatic

    /// Error thrown when there is an error in configuration of the MSAL.js library.
    [<AbstractClass>]
    type [<AllowNullLiteral>] ClientConfigurationError =
        inherit ClientAuthError

    /// Error thrown when there is an error in configuration of the MSAL.js library.
    type [<AllowNullLiteral>] ClientConfigurationErrorStatic =
        [<Emit "new $0($1...)">] abstract Create: errorCode: string * ?errorMessage: string -> ClientConfigurationError
        /// Creates an error thrown when the redirect uri is empty (not set by caller)
        abstract createRedirectUriEmptyError: unit -> ClientConfigurationError
        /// Creates an error thrown when the post-logout redirect uri is empty (not set by caller)
        abstract createPostLogoutRedirectUriEmptyError: unit -> ClientConfigurationError
        /// Creates an error thrown when the claims request could not be successfully parsed
        abstract createClaimsRequestParsingError: claimsRequestParseError: string -> ClientConfigurationError
        /// Creates an error thrown if authority uri is given an insecure protocol.
        abstract createInsecureAuthorityUriError: urlString: string -> ClientConfigurationError
        /// Creates an error thrown if URL string does not parse into separate segments.
        abstract createUrlParseError: urlParseError: string -> ClientConfigurationError
        /// Creates an error thrown if URL string is empty or null.
        abstract createUrlEmptyError: unit -> ClientConfigurationError
        /// Error thrown when scopes are not an array
        abstract createScopesNonArrayError: inputScopes: Array<string> -> ClientConfigurationError
        /// Error thrown when scopes are empty.
        abstract createEmptyScopesArrayError: inputScopes: Array<string> -> ClientConfigurationError
        /// Error thrown when client id scope is not provided as single scope.
        abstract createClientIdSingleScopeError: inputScopes: Array<string> -> ClientConfigurationError
        /// Error thrown when prompt is not an allowed type.
        abstract createInvalidPromptError: promptValue: string -> ClientConfigurationError
        /// Creates error thrown when claims parameter is not a stringified JSON object
        abstract createInvalidClaimsRequestError: unit -> ClientConfigurationError
        /// Throws error when token request is empty and nothing cached in storage.
        abstract createEmptyLogoutRequestError: unit -> ClientConfigurationError
        /// Throws error when token request is empty and nothing cached in storage.
        abstract createEmptyTokenRequestError: unit -> ClientConfigurationError
        /// Throws error when an invalid code_challenge_method is passed by the user
        abstract createInvalidCodeChallengeMethodError: unit -> ClientConfigurationError
        /// Throws error when both params: code_challenge and code_challenge_method are not passed together
        abstract createInvalidCodeChallengeParamsError: unit -> ClientConfigurationError
        /// Throws an error when the user passes invalid cloudDiscoveryMetadata
        abstract createInvalidCloudDiscoveryMetadataError: unit -> ClientConfigurationError
        /// Throws an error when the user passes invalid cloudDiscoveryMetadata
        abstract createInvalidAuthorityMetadataError: unit -> ClientConfigurationError
        /// Throws error when provided authority is not a member of the trusted host list
        abstract createUntrustedAuthorityError: unit -> ClientConfigurationError

    type [<AllowNullLiteral>] IExportsClientConfigurationErrorMessageRedirectUriNotSet =
        abstract code: string with get, set
        abstract desc: string with get, set

    type [<AllowNullLiteral>] IExportsClientConfigurationErrorMessage =
        abstract redirectUriNotSet: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract postLogoutUriNotSet: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract claimsRequestParsingError: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract authorityUriInsecure: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract urlParseError: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract urlEmptyError: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract emptyScopesError: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract nonArrayScopesError: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract clientIdSingleScopeError: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract invalidPrompt: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract invalidClaimsRequest: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract tokenRequestEmptyError: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract logoutRequestEmptyError: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract invalidCodeChallengeMethod: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract invalidCodeChallengeParams: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract invalidCloudDiscoveryMetadata: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract invalidAuthorityMetadata: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set
        abstract untrustedAuthority: IExportsClientConfigurationErrorMessageRedirectUriNotSet with get, set

[<AutoOpen>]
module __error_InteractionRequiredAuthError =
    type ServerError = __error_ServerError.ServerError

    type [<AllowNullLiteral>] IExports =
        abstract InteractionRequiredAuthErrorMessage: ResizeArray<string>
        abstract InteractionRequiredAuthSubErrorMessage: ResizeArray<string>
        abstract InteractionRequiredAuthError: InteractionRequiredAuthErrorStatic

    /// Error thrown when user interaction is required at the auth server.
    [<AbstractClass>]
    type [<AllowNullLiteral>] InteractionRequiredAuthError =
        inherit ServerError

    /// Error thrown when user interaction is required at the auth server.
    type [<AllowNullLiteral>] InteractionRequiredAuthErrorStatic =
        [<Emit "new $0($1...)">] abstract Create: ?errorCode: string * ?errorMessage: string * ?subError: string -> InteractionRequiredAuthError
        abstract isInteractionRequiredError: ?errorCode: string * ?errorString: string * ?subError: string -> bool

[<AutoOpen>]
module __error_ServerError =
    type AuthError = __error_AuthError.AuthError

    type [<AllowNullLiteral>] IExports =
        abstract ServerError: ServerErrorStatic

    /// Error thrown when there is an error with the server code, for example, unavailability.
    [<AbstractClass>]
    type [<AllowNullLiteral>] ServerError =
        inherit AuthError

    /// Error thrown when there is an error with the server code, for example, unavailability.
    type [<AllowNullLiteral>] ServerErrorStatic =
        [<Emit "new $0($1...)">] abstract Create: ?errorCode: string * ?errorMessage: string * ?subError: string -> ServerError

[<AutoOpen>]
module __logger_Logger =
    type LoggerOptions = __config_ClientConfiguration.LoggerOptions

    type [<AllowNullLiteral>] IExports =
        abstract Logger: LoggerStatic

    type [<AllowNullLiteral>] LoggerMessageOptions =
        abstract logLevel: LogLevel with get, set
        abstract correlationId: string option with get, set
        abstract containsPii: bool option with get, set
        abstract context: string option with get, set

    type [<RequireQualifiedAccess>] LogLevel =
        | Error = 0
        | Warning = 1
        | Info = 2
        | Verbose = 3

    /// Callback to send the messages to.
    type [<AllowNullLiteral>] ILoggerCallback =
        [<Emit "$0($1...)">] abstract Invoke: level: LogLevel * message: string * containsPii: bool -> unit

    /// Class which facilitates logging of messages to a specific place.
    type [<AllowNullLiteral>] Logger =
        /// Create new Logger with existing configurations.
        abstract clone: packageName: string * packageVersion: string -> Logger
        /// Execute callback with message.
        abstract executeCallback: level: LogLevel * message: string * containsPii: bool -> unit
        /// Logs error messages.
        abstract error: message: string * ?correlationId: string -> unit
        /// Logs error messages with PII.
        abstract errorPii: message: string * ?correlationId: string -> unit
        /// Logs warning messages.
        abstract warning: message: string * ?correlationId: string -> unit
        /// Logs warning messages with PII.
        abstract warningPii: message: string * ?correlationId: string -> unit
        /// Logs info messages.
        abstract info: message: string * ?correlationId: string -> unit
        /// Logs info messages with PII.
        abstract infoPii: message: string * ?correlationId: string -> unit
        /// Logs verbose messages.
        abstract verbose: message: string * ?correlationId: string -> unit
        /// Logs verbose messages with PII.
        abstract verbosePii: message: string * ?correlationId: string -> unit
        /// Returns whether PII Logging is enabled or not.
        abstract isPiiLoggingEnabled: unit -> bool

    /// Class which facilitates logging of messages to a specific place.
    type [<AllowNullLiteral>] LoggerStatic =
        [<Emit "new $0($1...)">] abstract Create: loggerOptions: LoggerOptions * ?packageName: string * ?packageVersion: string -> Logger

[<AutoOpen>]
module __network_INetworkModule =
    type NetworkResponse<'T> = __network_NetworkManager.NetworkResponse<'T>

    type [<AllowNullLiteral>] IExports =
        abstract StubbedNetworkModule: INetworkModule

    type [<AllowNullLiteral>] NetworkRequestOptions =
        abstract headers: Record<string, string> option with get, set
        abstract body: string option with get, set

    /// Client network interface to send backend requests.
    type [<AllowNullLiteral>] INetworkModule =
        /// Interface function for async network "GET" requests. Based on the Fetch standard: https://fetch.spec.whatwg.org/
        abstract sendGetRequestAsync: url: string * ?options: NetworkRequestOptions -> Promise<NetworkResponse<'T>>
        /// Interface function for async network "POST" requests. Based on the Fetch standard: https://fetch.spec.whatwg.org/
        abstract sendPostRequestAsync: url: string * ?options: NetworkRequestOptions -> Promise<NetworkResponse<'T>>

[<AutoOpen>]
module __network_NetworkManager =
    type INetworkModule = __network_INetworkModule.INetworkModule
    type NetworkRequestOptions = __network_INetworkModule.NetworkRequestOptions
    type RequestThumbprint = __network_RequestThumbprint.RequestThumbprint
    type CacheManager = __cache_CacheManager.CacheManager

    type [<AllowNullLiteral>] IExports =
        abstract NetworkManager: NetworkManagerStatic

    type [<AllowNullLiteral>] NetworkResponse<'T> =
        abstract headers: Record<string, string> with get, set
        abstract body: 'T with get, set
        abstract status: float with get, set

    type [<AllowNullLiteral>] NetworkManager =
        /// Wraps sendPostRequestAsync with necessary preflight and postflight logic
        abstract sendPostRequest: thumbprint: RequestThumbprint * tokenEndpoint: string * options: NetworkRequestOptions -> Promise<NetworkResponse<'T>>

    type [<AllowNullLiteral>] NetworkManagerStatic =
        [<Emit "new $0($1...)">] abstract Create: networkClient: INetworkModule * cacheManager: CacheManager -> NetworkManager

[<AutoOpen>]
module __network_RequestThumbprint =

    type [<AllowNullLiteral>] RequestThumbprint =
        abstract clientId: string with get, set
        abstract authority: string with get, set
        abstract scopes: Array<string> with get, set
        abstract homeAccountIdentifier: string option with get, set

[<AutoOpen>]
module __network_ThrottlingUtils =
    type NetworkResponse<'T> = __network_NetworkManager.NetworkResponse<'T>
    type ServerAuthorizationTokenResponse = __response_ServerAuthorizationTokenResponse.ServerAuthorizationTokenResponse
    type CacheManager = __cache_CacheManager.CacheManager
    type RequestThumbprint = __network_RequestThumbprint.RequestThumbprint

    type [<AllowNullLiteral>] IExports =
        abstract ThrottlingUtils: ThrottlingUtilsStatic

    type [<AllowNullLiteral>] ThrottlingUtils =
        interface end

    type [<AllowNullLiteral>] ThrottlingUtilsStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> ThrottlingUtils
        /// Prepares a RequestThumbprint to be stored as a key.
        abstract generateThrottlingStorageKey: thumbprint: RequestThumbprint -> string
        /// Performs necessary throttling checks before a network request.
        abstract preProcess: cacheManager: CacheManager * thumbprint: RequestThumbprint -> unit
        /// Performs necessary throttling checks after a network request.
        abstract postProcess: cacheManager: CacheManager * thumbprint: RequestThumbprint * response: NetworkResponse<ServerAuthorizationTokenResponse> -> unit
        /// Checks a NetworkResponse object's status codes against 429 or 5xx
        abstract checkResponseStatus: response: NetworkResponse<ServerAuthorizationTokenResponse> -> bool
        /// Checks a NetworkResponse object's RetryAfter header
        abstract checkResponseForRetryAfter: response: NetworkResponse<ServerAuthorizationTokenResponse> -> bool
        /// Calculates the Unix-time value for a throttle to expire given throttleTime in seconds.
        abstract calculateThrottleTime: throttleTime: float -> float
        abstract removeThrottle: cacheManager: CacheManager * clientId: string * authority: string * scopes: Array<string> * ?homeAccountIdentifier: string -> bool

[<AutoOpen>]
module __request_BaseAuthRequest =
    type AuthenticationScheme = __utils_Constants.AuthenticationScheme

    type [<AllowNullLiteral>] BaseAuthRequest =
        abstract authority: string with get, set
        abstract correlationId: string with get, set
        abstract scopes: Array<string> with get, set
        abstract authenticationScheme: AuthenticationScheme option with get, set
        abstract claims: string option with get, set
        abstract shrClaims: string option with get, set
        abstract resourceRequestMethod: string option with get, set
        abstract resourceRequestUri: string option with get, set

[<AutoOpen>]
module __request_CommonAuthorizationCodeRequest =
    type BaseAuthRequest = __request_BaseAuthRequest.BaseAuthRequest
    type StringDict = __utils_MsalTypes.StringDict

    type [<AllowNullLiteral>] CommonAuthorizationCodeRequest =
        interface end

[<AutoOpen>]
module __request_CommonAuthorizationUrlRequest =
    type ResponseMode = __utils_Constants.ResponseMode
    type StringDict = __utils_MsalTypes.StringDict
    type BaseAuthRequest = __request_BaseAuthRequest.BaseAuthRequest
    type AccountInfo = __account_AccountInfo.AccountInfo

    type [<AllowNullLiteral>] CommonAuthorizationUrlRequest =
        interface end

[<AutoOpen>]
module __request_CommonClientCredentialRequest =
    type BaseAuthRequest = __request_BaseAuthRequest.BaseAuthRequest

    type [<AllowNullLiteral>] CommonClientCredentialRequest =
        interface end

[<AutoOpen>]
module __request_CommonDeviceCodeRequest =
    type DeviceCodeResponse = __response_DeviceCodeResponse.DeviceCodeResponse
    type BaseAuthRequest = __request_BaseAuthRequest.BaseAuthRequest

    type [<AllowNullLiteral>] CommonDeviceCodeRequest =
        interface end

[<AutoOpen>]
module __request_CommonEndSessionRequest =
    type AccountInfo = __account_AccountInfo.AccountInfo

    type [<AllowNullLiteral>] CommonEndSessionRequest =
        abstract correlationId: string with get, set
        abstract account: AccountInfo option with get, set
        abstract postLogoutRedirectUri: string option with get, set
        abstract idTokenHint: string option with get, set

[<AutoOpen>]
module __request_CommonOnBehalfOfRequest =
    type BaseAuthRequest = __request_BaseAuthRequest.BaseAuthRequest

    type [<AllowNullLiteral>] CommonOnBehalfOfRequest =
        interface end

[<AutoOpen>]
module __request_CommonRefreshTokenRequest =
    type BaseAuthRequest = __request_BaseAuthRequest.BaseAuthRequest
    type StringDict = __utils_MsalTypes.StringDict

    type [<AllowNullLiteral>] CommonRefreshTokenRequest =
        interface end

[<AutoOpen>]
module __request_CommonSilentFlowRequest =
    type AccountInfo = __account_AccountInfo.AccountInfo
    type BaseAuthRequest = __request_BaseAuthRequest.BaseAuthRequest
    type StringDict = __utils_MsalTypes.StringDict

    type [<AllowNullLiteral>] CommonSilentFlowRequest =
        interface end

[<AutoOpen>]
module __request_CommonUsernamePasswordRequest =
    type BaseAuthRequest = __request_BaseAuthRequest.BaseAuthRequest

    type [<AllowNullLiteral>] CommonUsernamePasswordRequest =
        interface end

[<AutoOpen>]
module __request_RequestParameterBuilder =
    type ResponseMode = __utils_Constants.ResponseMode
    type StringDict = __utils_MsalTypes.StringDict
    type LibraryInfo = __config_ClientConfiguration.LibraryInfo

    type [<AllowNullLiteral>] IExports =
        abstract RequestParameterBuilder: RequestParameterBuilderStatic

    type [<AllowNullLiteral>] RequestParameterBuilder =
        /// add response_type = code
        abstract addResponseTypeCode: unit -> unit
        /// add response_mode. defaults to query.
        abstract addResponseMode: ?responseMode: ResponseMode -> unit
        /// add scopes. set addOidcScopes to false to prevent default scopes in non-user scenarios
        abstract addScopes: scopes: ResizeArray<string> * ?addOidcScopes: bool -> unit
        /// add clientId
        abstract addClientId: clientId: string -> unit
        /// add redirect_uri
        abstract addRedirectUri: redirectUri: string -> unit
        /// add post logout redirectUri
        abstract addPostLogoutRedirectUri: redirectUri: string -> unit
        /// add id_token_hint to logout request
        abstract addIdTokenHint: idTokenHint: string -> unit
        /// add domain_hint
        abstract addDomainHint: domainHint: string -> unit
        /// add login_hint
        abstract addLoginHint: loginHint: string -> unit
        /// add sid
        abstract addSid: sid: string -> unit
        /// add claims
        abstract addClaims: ?claims: string * ?clientCapabilities: Array<string> -> unit
        /// add correlationId
        abstract addCorrelationId: correlationId: string -> unit
        /// add library info query params
        abstract addLibraryInfo: libraryInfo: LibraryInfo -> unit
        /// add prompt
        abstract addPrompt: prompt: string -> unit
        /// add state
        abstract addState: state: string -> unit
        /// add nonce
        abstract addNonce: nonce: string -> unit
        /// add code_challenge and code_challenge_method
        /// - throw if either of them are not passed
        abstract addCodeChallengeParams: codeChallenge: string * codeChallengeMethod: string -> unit
        /// add the `authorization_code` passed by the user to exchange for a token
        abstract addAuthorizationCode: code: string -> unit
        /// add the `authorization_code` passed by the user to exchange for a token
        abstract addDeviceCode: code: string -> unit
        /// add the `refreshToken` passed by the user
        abstract addRefreshToken: refreshToken: string -> unit
        /// add the `code_verifier` passed by the user to exchange for a token
        abstract addCodeVerifier: codeVerifier: string -> unit
        /// add client_secret
        abstract addClientSecret: clientSecret: string -> unit
        /// add clientAssertion for confidential client flows
        abstract addClientAssertion: clientAssertion: string -> unit
        /// add clientAssertionType for confidential client flows
        abstract addClientAssertionType: clientAssertionType: string -> unit
        /// add OBO assertion for confidential client flows
        abstract addOboAssertion: oboAssertion: string -> unit
        /// add grant type
        abstract addRequestTokenUse: tokenUse: string -> unit
        /// add grant type
        abstract addGrantType: grantType: string -> unit
        /// add client info
        abstract addClientInfo: unit -> unit
        /// add extraQueryParams
        abstract addExtraQueryParameters: eQparams: StringDict -> unit
        abstract addClientCapabilitiesToClaims: ?claims: string * ?clientCapabilities: Array<string> -> string
        /// adds `username` for Password Grant flow
        abstract addUsername: username: string -> unit
        /// adds `password` for Password Grant flow
        abstract addPassword: password: string -> unit
        /// add pop_jwk to query params
        abstract addPopToken: cnfString: string -> unit
        /// Utility to create a URL from the params map
        abstract createQueryString: unit -> string

    type [<AllowNullLiteral>] RequestParameterBuilderStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> RequestParameterBuilder

[<AutoOpen>]
module __request_RequestValidator =
    type StringDict = __utils_MsalTypes.StringDict

    type [<AllowNullLiteral>] IExports =
        abstract RequestValidator: RequestValidatorStatic

    /// Validates server consumable params from the "request" objects
    type [<AllowNullLiteral>] RequestValidator =
        interface end

    /// Validates server consumable params from the "request" objects
    type [<AllowNullLiteral>] RequestValidatorStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> RequestValidator
        /// Utility to check if the `redirectUri` in the request is a non-null value
        abstract validateRedirectUri: redirectUri: string -> unit
        /// Utility to validate prompt sent by the user in the request
        abstract validatePrompt: prompt: string -> unit
        abstract validateClaims: claims: string -> unit
        /// Utility to validate code_challenge and code_challenge_method
        abstract validateCodeChallengeParams: codeChallenge: string * codeChallengeMethod: string -> unit
        /// Utility to validate code_challenge_method
        abstract validateCodeChallengeMethod: codeChallengeMethod: string -> unit
        /// Removes unnecessary or duplicate query parameters from extraQueryParameters
        abstract sanitizeEQParams: eQParams: StringDict * queryParams: Map<string, string> -> StringDict

[<AutoOpen>]
module __request_ScopeSet =

    type [<AllowNullLiteral>] IExports =
        abstract ScopeSet: ScopeSetStatic

    /// The ScopeSet class creates a set of scopes. Scopes are case-insensitive, unique values, so the Set object in JS makes
    /// the most sense to implement for this class. All scopes are trimmed and converted to lower case strings in intersection and union functions
    /// to ensure uniqueness of strings.
    type [<AllowNullLiteral>] ScopeSet =
        /// Check if a given scope is present in this set of scopes.
        abstract containsScope: scope: string -> bool
        /// Check if a set of scopes is present in this set of scopes.
        abstract containsScopeSet: scopeSet: ScopeSet -> bool
        /// Check if set of scopes contains only the defaults
        abstract containsOnlyOIDCScopes: unit -> bool
        /// Appends single scope if passed
        abstract appendScope: newScope: string -> unit
        /// Appends multiple scopes if passed
        abstract appendScopes: newScopes: Array<string> -> unit
        /// Removes element from set of scopes.
        abstract removeScope: scope: string -> unit
        /// Removes default scopes from set of scopes
        /// Primarily used to prevent cache misses if the default scopes are not returned from the server
        abstract removeOIDCScopes: unit -> unit
        /// Combines an array of scopes with the current set of scopes.
        abstract unionScopeSets: otherScopes: ScopeSet -> Set<string>
        /// Check if scopes intersect between this set and another.
        abstract intersectingScopeSets: otherScopes: ScopeSet -> bool
        /// Returns size of set of scopes.
        abstract getScopeCount: unit -> float
        /// Returns the scopes as an array of string values
        abstract asArray: unit -> Array<string>
        /// Prints scopes into a space-delimited string
        abstract printScopes: unit -> string
        /// Prints scopes into a space-delimited lower-case string (used for caching)
        abstract printScopesLowerCase: unit -> string

    /// The ScopeSet class creates a set of scopes. Scopes are case-insensitive, unique values, so the Set object in JS makes
    /// the most sense to implement for this class. All scopes are trimmed and converted to lower case strings in intersection and union functions
    /// to ensure uniqueness of strings.
    type [<AllowNullLiteral>] ScopeSetStatic =
        [<Emit "new $0($1...)">] abstract Create: inputScopes: Array<string> -> ScopeSet
        /// Factory method to create ScopeSet from space-delimited string
        abstract fromString: inputScopeString: string -> ScopeSet

[<AutoOpen>]
module __response_AuthenticationResult =
    type AccountInfo = __account_AccountInfo.AccountInfo

    type [<AllowNullLiteral>] AuthenticationResult =
        abstract authority: string with get, set
        abstract uniqueId: string with get, set
        abstract tenantId: string with get, set
        abstract scopes: Array<string> with get, set
        abstract account: AccountInfo option with get, set
        abstract idToken: string with get, set
        abstract idTokenClaims: obj with get, set
        abstract accessToken: string with get, set
        abstract fromCache: bool with get, set
        abstract expiresOn: DateTime option with get, set
        abstract tokenType: string with get, set
        abstract extExpiresOn: DateTime option with get, set
        abstract state: string option with get, set
        abstract familyId: string option with get, set
        abstract cloudGraphHostName: string option with get, set
        abstract msGraphHost: string option with get, set

[<AutoOpen>]
module __response_AuthorizationCodePayload =

    type [<AllowNullLiteral>] AuthorizationCodePayload =
        abstract code: string with get, set
        abstract cloud_instance_name: string option with get, set
        abstract cloud_instance_host_name: string option with get, set
        abstract cloud_graph_host_name: string option with get, set
        abstract msgraph_host: string option with get, set
        abstract state: string option with get, set
        abstract nonce: string option with get, set

[<AutoOpen>]
module __response_DeviceCodeResponse =

    type [<AllowNullLiteral>] DeviceCodeResponse =
        abstract userCode: string with get, set
        abstract deviceCode: string with get, set
        abstract verificationUri: string with get, set
        abstract expiresIn: float with get, set
        abstract interval: float with get, set
        abstract message: string with get, set

    type [<AllowNullLiteral>] ServerDeviceCodeResponse =
        abstract user_code: string with get, set
        abstract device_code: string with get, set
        abstract verification_uri: string with get, set
        abstract expires_in: float with get, set
        abstract interval: float with get, set
        abstract message: string with get, set

[<AutoOpen>]
module __response_ResponseHandler =
    type ServerAuthorizationTokenResponse = __response_ServerAuthorizationTokenResponse.ServerAuthorizationTokenResponse
    type ICrypto = __crypto_ICrypto.ICrypto
    type ServerAuthorizationCodeResponse = __response_ServerAuthorizationCodeResponse.ServerAuthorizationCodeResponse
    type Logger = __logger_Logger.Logger
    type AuthToken = __account_AuthToken.AuthToken
    type AuthenticationResult = __response_AuthenticationResult.AuthenticationResult
    type Authority = __authority_Authority.Authority
    type CacheRecord = __cache_entities_CacheRecord.CacheRecord
    type CacheManager = __cache_CacheManager.CacheManager
    type RequestStateObject = __utils_ProtocolUtils.RequestStateObject
    type ICachePlugin = __cache_interface_ICachePlugin.ICachePlugin
    type ISerializableTokenCache = __cache_interface_ISerializableTokenCache.ISerializableTokenCache
    type AuthorizationCodePayload = __response_AuthorizationCodePayload.AuthorizationCodePayload
    type BaseAuthRequest = __request_BaseAuthRequest.BaseAuthRequest

    type [<AllowNullLiteral>] IExports =
        abstract ResponseHandler: ResponseHandlerStatic

    /// Class that handles response parsing.
    type [<AllowNullLiteral>] ResponseHandler =
        /// Function which validates server authorization code response.
        abstract validateServerAuthorizationCodeResponse: serverResponseHash: ServerAuthorizationCodeResponse * cachedState: string * cryptoObj: ICrypto -> unit
        /// Function which validates server authorization token response.
        abstract validateTokenResponse: serverResponse: ServerAuthorizationTokenResponse -> unit
        /// Returns a constructed token response based on given string. Also manages the cache updates and cleanups.
        abstract handleServerTokenResponse: serverTokenResponse: ServerAuthorizationTokenResponse * authority: Authority * reqTimestamp: float * request: BaseAuthRequest * ?authCodePayload: AuthorizationCodePayload * ?oboAssertion: string * ?handlingRefreshTokenResponse: bool -> Promise<AuthenticationResult>

    /// Class that handles response parsing.
    type [<AllowNullLiteral>] ResponseHandlerStatic =
        [<Emit "new $0($1...)">] abstract Create: clientId: string * cacheStorage: CacheManager * cryptoObj: ICrypto * logger: Logger * serializableCache: ISerializableTokenCache option * persistencePlugin: ICachePlugin option -> ResponseHandler
        /// Creates an @AuthenticationResult from @CacheRecord , @IdToken , and a boolean that states whether or not the result is from cache.
        ///
        /// Optionally takes a state string that is set as-is in the response.
        abstract generateAuthenticationResult: cryptoObj: ICrypto * authority: Authority * cacheRecord: CacheRecord * fromTokenCache: bool * request: BaseAuthRequest * ?idTokenObj: AuthToken * ?requestState: RequestStateObject -> Promise<AuthenticationResult>

[<AutoOpen>]
module __response_ServerAuthorizationCodeResponse =

    type [<AllowNullLiteral>] ServerAuthorizationCodeResponse =
        abstract code: string option with get, set
        abstract client_info: string option with get, set
        abstract state: string option with get, set
        abstract cloud_instance_name: string option with get, set
        abstract cloud_instance_host_name: string option with get, set
        abstract cloud_graph_host_name: string option with get, set
        abstract msgraph_host: string option with get, set
        abstract error: string option with get, set
        abstract error_description: string option with get, set
        abstract suberror: string option with get, set

[<AutoOpen>]
module __response_ServerAuthorizationTokenResponse =

    type [<AllowNullLiteral>] ServerAuthorizationTokenResponse =
        abstract token_type: string option with get, set
        abstract scope: string option with get, set
        abstract expires_in: float option with get, set
        abstract refresh_in: float option with get, set
        abstract ext_expires_in: float option with get, set
        abstract access_token: string option with get, set
        abstract refresh_token: string option with get, set
        abstract id_token: string option with get, set
        abstract client_info: string option with get, set
        abstract foci: string option with get, set
        abstract error: string option with get, set
        abstract error_description: string option with get, set
        abstract error_codes: Array<string> option with get, set
        abstract suberror: string option with get, set
        abstract timestamp: string option with get, set
        abstract trace_id: string option with get, set
        abstract correlation_id: string option with get, set

[<AutoOpen>]
module __url_IUri =

    /// Interface which describes URI components.
    type [<AllowNullLiteral>] IUri =
        abstract Protocol: string with get, set
        abstract HostNameAndPort: string with get, set
        abstract AbsolutePath: string with get, set
        abstract Search: string with get, set
        abstract Hash: string with get, set
        abstract PathSegments: ResizeArray<string> with get, set
        abstract QueryString: string with get, set

[<AutoOpen>]
module __url_UrlString =
    type ServerAuthorizationCodeResponse = __response_ServerAuthorizationCodeResponse.ServerAuthorizationCodeResponse
    type IUri = __url_IUri.IUri

    type [<AllowNullLiteral>] IExports =
        abstract UrlString: UrlStringStatic

    /// Url object class which can perform various transformations on url strings.
    type [<AllowNullLiteral>] UrlString =
        // obj
        /// Throws if urlString passed is not a valid authority URI string.
        abstract validateAsUri: unit -> unit
        /// Function to remove query string params from url. Returns the new url.
        abstract urlRemoveQueryStringParameter: name: string -> string
        /// <summary>Given a url like https://a:b/common/d?e=f#g, and a tenantId, returns https://a:b/tenantId/d</summary>
        /// <param name="tenantId">The tenant id to replace</param>
        abstract replaceTenantPath: tenantId: string -> UrlString
        /// Returns the anchor part(#) of the URL
        abstract getHash: unit -> string
        /// Parses out the components from a url string.
        abstract getUrlComponents: unit -> IUri

    /// Url object class which can perform various transformations on url strings.
    type [<AllowNullLiteral>] UrlStringStatic =
        [<Emit "new $0($1...)">] abstract Create: url: string -> UrlString
        /// Ensure urls are lower case and end with a / character.
        abstract canonicalizeUri: url: string -> string
        abstract removeHashFromUrl: url: string -> string
        abstract getDomainFromUrl: url: string -> string
        abstract getAbsoluteUrl: relativeUrl: string * baseUrl: string -> string
        /// Parses hash string from given string. Returns empty string if no hash symbol is found.
        abstract parseHash: hashString: string -> string
        abstract constructAuthorityUriFromObject: urlObject: IUri -> UrlString
        /// Returns URL hash as server auth code response object.
        abstract getDeserializedHash: hash: string -> ServerAuthorizationCodeResponse
        /// Check if the hash of the URL string contains known properties
        abstract hashContainsKnownProperties: hash: string -> bool

[<AutoOpen>]
module __utils_Constants =

    type [<AllowNullLiteral>] IExports =
        abstract Constants: IExportsConstants
        abstract OIDC_DEFAULT_SCOPES: ResizeArray<string>
        abstract OIDC_SCOPES: ResizeArray<string>
        abstract PromptValue: IExportsPromptValue
        abstract BlacklistedEQParams: ResizeArray<SSOTypes>
        abstract CodeChallengeMethodValues: IExportsCodeChallengeMethodValues
        abstract CodeChallengeMethodValuesArray: ResizeArray<string>
        abstract APP_METADATA: obj
        abstract ClientInfo: obj
        abstract THE_FAMILY_ID: obj
        abstract AUTHORITY_METADATA_CONSTANTS: IExportsAUTHORITY_METADATA_CONSTANTS
        abstract SERVER_TELEM_CONSTANTS: IExportsSERVER_TELEM_CONSTANTS
        abstract ThrottlingConstants: IExportsThrottlingConstants
        abstract Errors: IExportsErrors

    type [<StringEnum>] [<RequireQualifiedAccess>] HeaderNames =
        | [<CompiledName "Content-Type">] CONTENT_TYPE
        | [<CompiledName "x-client-current-telemetry">] X_CLIENT_CURR_TELEM
        | [<CompiledName "x-client-last-telemetry">] X_CLIENT_LAST_TELEM
        | [<CompiledName "Retry-After">] RETRY_AFTER
        | [<CompiledName "x-ms-lib-capability">] X_MS_LIB_CAPABILITY
        | [<CompiledName "retry-after, h429">] X_MS_LIB_CAPABILITY_VALUE

    type [<StringEnum>] [<RequireQualifiedAccess>] PersistentCacheKeys =
        | [<CompiledName "idtoken">] ID_TOKEN
        | [<CompiledName "client.info">] CLIENT_INFO
        | [<CompiledName "adal.idtoken">] ADAL_ID_TOKEN
        | [<CompiledName "error">] ERROR
        | [<CompiledName "error.description">] ERROR_DESC

    type [<StringEnum>] [<RequireQualifiedAccess>] AADAuthorityConstants =
        | [<CompiledName "common">] COMMON
        | [<CompiledName "organizations">] ORGANIZATIONS
        | [<CompiledName "consumers">] CONSUMERS

    type [<StringEnum>] [<RequireQualifiedAccess>] AADServerParamKeys =
        | [<CompiledName "client_id">] CLIENT_ID
        | [<CompiledName "redirect_uri">] REDIRECT_URI
        | [<CompiledName "response_type">] RESPONSE_TYPE
        | [<CompiledName "response_mode">] RESPONSE_MODE
        | [<CompiledName "grant_type">] GRANT_TYPE
        | [<CompiledName "claims">] CLAIMS
        | [<CompiledName "scope">] SCOPE
        | [<CompiledName "error">] ERROR
        | [<CompiledName "error_description">] ERROR_DESCRIPTION
        | [<CompiledName "access_token">] ACCESS_TOKEN
        | [<CompiledName "id_token">] ID_TOKEN
        | [<CompiledName "refresh_token">] REFRESH_TOKEN
        | [<CompiledName "expires_in">] EXPIRES_IN
        | [<CompiledName "state">] STATE
        | [<CompiledName "nonce">] NONCE
        | [<CompiledName "prompt">] PROMPT
        | [<CompiledName "session_state">] SESSION_STATE
        | [<CompiledName "client_info">] CLIENT_INFO
        | [<CompiledName "code">] CODE
        | [<CompiledName "code_challenge">] CODE_CHALLENGE
        | [<CompiledName "code_challenge_method">] CODE_CHALLENGE_METHOD
        | [<CompiledName "code_verifier">] CODE_VERIFIER
        | [<CompiledName "client-request-id">] CLIENT_REQUEST_ID
        | [<CompiledName "x-client-SKU">] X_CLIENT_SKU
        | [<CompiledName "x-client-VER">] X_CLIENT_VER
        | [<CompiledName "x-client-OS">] X_CLIENT_OS
        | [<CompiledName "x-client-CPU">] X_CLIENT_CPU
        | [<CompiledName "post_logout_redirect_uri">] POST_LOGOUT_URI
        | [<CompiledName "id_token_hint">] ID_TOKEN_HINT
        | [<CompiledName "device_code">] DEVICE_CODE
        | [<CompiledName "client_secret">] CLIENT_SECRET
        | [<CompiledName "client_assertion">] CLIENT_ASSERTION
        | [<CompiledName "client_assertion_type">] CLIENT_ASSERTION_TYPE
        | [<CompiledName "token_type">] TOKEN_TYPE
        | [<CompiledName "req_cnf">] REQ_CNF
        | [<CompiledName "assertion">] OBO_ASSERTION
        | [<CompiledName "requested_token_use">] REQUESTED_TOKEN_USE
        | [<CompiledName "on_behalf_of">] ON_BEHALF_OF
        | [<CompiledName "foci">] FOCI

    type [<StringEnum>] [<RequireQualifiedAccess>] ClaimsRequestKeys =
        | [<CompiledName "access_token">] ACCESS_TOKEN
        | [<CompiledName "xms_cc">] XMS_CC

    type [<StringEnum>] [<RequireQualifiedAccess>] SSOTypes =
        | [<CompiledName "account">] ACCOUNT
        | [<CompiledName "sid">] SID
        | [<CompiledName "login_hint">] LOGIN_HINT
        | [<CompiledName "id_token">] ID_TOKEN
        | [<CompiledName "domain_hint">] DOMAIN_HINT
        | [<CompiledName "organizations">] ORGANIZATIONS
        | [<CompiledName "consumers">] CONSUMERS
        | [<CompiledName "accountIdentifier">] ACCOUNT_ID
        | [<CompiledName "homeAccountIdentifier">] HOMEACCOUNT_ID

    type [<StringEnum>] [<RequireQualifiedAccess>] ResponseMode =
        | [<CompiledName "query">] QUERY
        | [<CompiledName "fragment">] FRAGMENT
        | [<CompiledName "form_post">] FORM_POST

    type [<StringEnum>] [<RequireQualifiedAccess>] GrantType =
        | [<CompiledName "implicit">] IMPLICIT_GRANT
        | [<CompiledName "authorization_code">] AUTHORIZATION_CODE_GRANT
        | [<CompiledName "client_credentials">] CLIENT_CREDENTIALS_GRANT
        | [<CompiledName "password">] RESOURCE_OWNER_PASSWORD_GRANT
        | [<CompiledName "refresh_token">] REFRESH_TOKEN_GRANT
        | [<CompiledName "device_code">] DEVICE_CODE_GRANT
        | [<CompiledName "urn:ietf:params:oauth:grant-type:jwt-bearer">] JWT_BEARER

    type [<StringEnum>] [<RequireQualifiedAccess>] CacheAccountType =
        | [<CompiledName "MSSTS">] MSSTS_ACCOUNT_TYPE
        | [<CompiledName "ADFS">] ADFS_ACCOUNT_TYPE
        | [<CompiledName "MSA">] MSAV1_ACCOUNT_TYPE
        | [<CompiledName "Generic">] GENERIC_ACCOUNT_TYPE

    type [<StringEnum>] [<RequireQualifiedAccess>] Separators =
        | [<CompiledName "-">] CACHE_KEY_SEPARATOR
        | [<CompiledName ".">] CLIENT_INFO_SEPARATOR

    type [<StringEnum>] [<RequireQualifiedAccess>] CredentialType =
        | [<CompiledName "IdToken">] ID_TOKEN
        | [<CompiledName "AccessToken">] ACCESS_TOKEN
        | [<CompiledName "AccessToken_With_AuthScheme">] ACCESS_TOKEN_WITH_AUTH_SCHEME
        | [<CompiledName "RefreshToken">] REFRESH_TOKEN

    type [<StringEnum>] [<RequireQualifiedAccess>] CacheSchemaType =
        | [<CompiledName "Account">] ACCOUNT
        | [<CompiledName "Credential">] CREDENTIAL
        | [<CompiledName "IdToken">] ID_TOKEN
        | [<CompiledName "AccessToken">] ACCESS_TOKEN
        | [<CompiledName "RefreshToken">] REFRESH_TOKEN
        | [<CompiledName "AppMetadata">] APP_METADATA
        | [<CompiledName "TempCache">] TEMPORARY
        | [<CompiledName "Telemetry">] TELEMETRY
        | [<CompiledName "Undefined">] UNDEFINED
        | [<CompiledName "Throttling">] THROTTLING

    type [<RequireQualifiedAccess>] CacheType =
        | ADFS = 1001
        | MSA = 1002
        | MSSTS = 1003
        | GENERIC = 1004
        | ACCESS_TOKEN = 2001
        | REFRESH_TOKEN = 2002
        | ID_TOKEN = 2003
        | APP_METADATA = 3001
        | UNDEFINED = 9999

    type [<StringEnum>] [<RequireQualifiedAccess>] AuthorityMetadataSource =
        | [<CompiledName "config">] CONFIG
        | [<CompiledName "cache">] CACHE
        | [<CompiledName "network">] NETWORK

    type [<StringEnum>] [<RequireQualifiedAccess>] AuthenticationScheme =
        | [<CompiledName "pop">] POP
        | [<CompiledName "Bearer">] BEARER

    type [<StringEnum>] [<RequireQualifiedAccess>] PasswordGrantConstants =
        | Username
        | Password

    type [<AllowNullLiteral>] IExportsConstants =
        abstract LIBRARY_NAME: string with get, set
        abstract SKU: string with get, set
        abstract CACHE_PREFIX: string with get, set
        abstract DEFAULT_AUTHORITY: string with get, set
        abstract DEFAULT_AUTHORITY_HOST: string with get, set
        abstract ADFS: string with get, set
        abstract AAD_INSTANCE_DISCOVERY_ENDPT: string with get, set
        abstract RESOURCE_DELIM: string with get, set
        abstract NO_ACCOUNT: string with get, set
        abstract CLAIMS: string with get, set
        abstract CONSUMER_UTID: string with get, set
        abstract OPENID_SCOPE: string with get, set
        abstract PROFILE_SCOPE: string with get, set
        abstract OFFLINE_ACCESS_SCOPE: string with get, set
        abstract EMAIL_SCOPE: string with get, set
        abstract CODE_RESPONSE_TYPE: string with get, set
        abstract CODE_GRANT_TYPE: string with get, set
        abstract RT_GRANT_TYPE: string with get, set
        abstract FRAGMENT_RESPONSE_MODE: string with get, set
        abstract S256_CODE_CHALLENGE_METHOD: string with get, set
        abstract URL_FORM_CONTENT_TYPE: string with get, set
        abstract AUTHORIZATION_PENDING: string with get, set
        abstract NOT_DEFINED: string with get, set
        abstract EMPTY_STRING: string with get, set
        abstract FORWARD_SLASH: string with get, set

    type [<AllowNullLiteral>] IExportsPromptValue =
        abstract LOGIN: string with get, set
        abstract SELECT_ACCOUNT: string with get, set
        abstract CONSENT: string with get, set
        abstract NONE: string with get, set

    type [<AllowNullLiteral>] IExportsCodeChallengeMethodValues =
        abstract PLAIN: string with get, set
        abstract S256: string with get, set

    type [<AllowNullLiteral>] IExportsAUTHORITY_METADATA_CONSTANTS =
        abstract CACHE_KEY: string with get, set
        abstract REFRESH_TIME_SECONDS: float with get, set

    type [<AllowNullLiteral>] IExportsSERVER_TELEM_CONSTANTS =
        abstract SCHEMA_VERSION: float with get, set
        abstract MAX_HEADER_BYTES: float with get, set
        abstract CACHE_KEY: string with get, set
        abstract CATEGORY_SEPARATOR: string with get, set
        abstract VALUE_SEPARATOR: string with get, set
        abstract OVERFLOW_TRUE: string with get, set
        abstract OVERFLOW_FALSE: string with get, set
        abstract UNKNOWN_ERROR: string with get, set

    type [<AllowNullLiteral>] IExportsThrottlingConstants =
        abstract DEFAULT_THROTTLE_TIME_SECONDS: float with get, set
        abstract DEFAULT_MAX_THROTTLE_TIME_SECONDS: float with get, set
        abstract THROTTLING_PREFIX: string with get, set

    type [<AllowNullLiteral>] IExportsErrors =
        abstract INVALID_GRANT_ERROR: string with get, set
        abstract CLIENT_MISMATCH_ERROR: string with get, set

[<AutoOpen>]
module __utils_MsalTypes =

    type [<AllowNullLiteral>] StringDict =
        [<Emit "$0[$1]{{=$2}}">] abstract Item: key: string -> string with get, set

[<AutoOpen>]
module __utils_ProtocolUtils =
    type ICrypto = __crypto_ICrypto.ICrypto

    type [<AllowNullLiteral>] IExports =
        abstract ProtocolUtils: ProtocolUtilsStatic

    type [<AllowNullLiteral>] LibraryStateObject =
        abstract id: string with get, set
        abstract meta: Record<string, string> option with get, set

    type [<AllowNullLiteral>] RequestStateObject =
        abstract userRequestState: string with get, set
        abstract libraryState: LibraryStateObject with get, set

    /// Class which provides helpers for OAuth 2.0 protocol specific values
    type [<AllowNullLiteral>] ProtocolUtils =
        interface end

    /// Class which provides helpers for OAuth 2.0 protocol specific values
    type [<AllowNullLiteral>] ProtocolUtilsStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> ProtocolUtils
        /// Appends user state with random guid, or returns random guid.
        abstract setRequestState: cryptoObj: ICrypto * ?userState: string * ?meta: Record<string, string> -> string
        /// Generates the state value used by the common library.
        abstract generateLibraryState: cryptoObj: ICrypto * ?meta: Record<string, string> -> string
        /// Parses the state into the RequestStateObject, which contains the LibraryState info and the state passed by the user.
        abstract parseRequestState: cryptoObj: ICrypto * state: string -> RequestStateObject

[<AutoOpen>]
module __utils_StringUtils =
    type DecodedAuthToken = __account_DecodedAuthToken.DecodedAuthToken

    type [<AllowNullLiteral>] IExports =
        abstract StringUtils: StringUtilsStatic

    type [<AllowNullLiteral>] StringUtils =
        interface end

    type [<AllowNullLiteral>] StringUtilsStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> StringUtils
        /// decode a JWT
        abstract decodeAuthToken: authToken: string -> DecodedAuthToken
        /// Check if a string is empty.
        abstract isEmpty: ?str: string -> bool
        abstract startsWith: str: string * search: string -> bool
        abstract endsWith: str: string * search: string -> bool
        /// Parses string into an object.
        abstract queryStringToObject: query: string -> 'T
        /// Trims entries in an array.
        abstract trimArrayEntries: arr: Array<string> -> Array<string>
        /// Removes empty strings from array
        abstract removeEmptyStringsFromArray: arr: Array<string> -> Array<string>
        /// Attempts to parse a string into JSON
        abstract jsonParseHelper: str: string -> 'T option
        /// <summary>Tests if a given string matches a given pattern, with support for wildcards and queries.</summary>
        /// <param name="pattern">Wildcard pattern to string match. Supports "*" for wildcards and "?" for queries</param>
        /// <param name="input">String to match against</param>
        abstract matchPattern: pattern: string * input: string -> bool

[<AutoOpen>]
module __utils_TimeUtils =

    type [<AllowNullLiteral>] IExports =
        abstract TimeUtils: TimeUtilsStatic

    /// Utility class which exposes functions for managing date and time operations.
    type [<AllowNullLiteral>] TimeUtils =
        interface end

    /// Utility class which exposes functions for managing date and time operations.
    type [<AllowNullLiteral>] TimeUtilsStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> TimeUtils
        /// return the current time in Unix time (seconds).
        abstract nowSeconds: unit -> float
        /// check if a token is expired based on given UTC time in seconds.
        abstract isTokenExpired: expiresOn: string * offset: float -> bool

[<AutoOpen>]
module __cache_entities_AccessTokenEntity =
    type CredentialEntity = __cache_entities_CredentialEntity.CredentialEntity
    type ICrypto = __crypto_ICrypto.ICrypto

    type [<AllowNullLiteral>] IExports =
        abstract AccessTokenEntity: AccessTokenEntityStatic

    /// ACCESS_TOKEN Credential Type
    ///
    /// Key:Value Schema:
    ///
    /// Key Example: uid.utid-login.microsoftonline.com-accesstoken-clientId-contoso.com-user.read
    ///
    /// Value Schema:
    /// {
    ///       homeAccountId: home account identifier for the auth scheme,
    ///       environment: entity that issued the token, represented as a full host
    ///       credentialType: Type of credential as a string, can be one of the following: RefreshToken, AccessToken, IdToken, Password, Cookie, Certificate, Other
    ///       clientId: client ID of the application
    ///       secret: Actual credential as a string
    ///       familyId: Family ID identifier, usually only used for refresh tokens
    ///       realm: Full tenant or organizational identifier that the account belongs to
    ///       target: Permissions that are included in the token, or for refresh tokens, the resource identifier.
    ///       cachedAt: Absolute device time when entry was created in the cache.
    ///       expiresOn: Token expiry time, calculated based on current UTC time in seconds. Represented as a string.
    ///       extendedExpiresOn: Additional extended expiry time until when token is valid in case of server-side outage. Represented as string in UTC seconds.
    ///       keyId: used for POP and SSH tokenTypes
    ///       tokenType: Type of the token issued. Usually "Bearer"
    /// }
    type [<AllowNullLiteral>] AccessTokenEntity =
        inherit CredentialEntity
        abstract realm: string with get, set
        abstract target: string with get, set
        abstract cachedAt: string with get, set
        abstract expiresOn: string with get, set
        abstract extendedExpiresOn: string option with get, set
        abstract refreshOn: string option with get, set
        abstract keyId: string option with get, set
        abstract tokenType: string option with get, set

    /// ACCESS_TOKEN Credential Type
    ///
    /// Key:Value Schema:
    ///
    /// Key Example: uid.utid-login.microsoftonline.com-accesstoken-clientId-contoso.com-user.read
    ///
    /// Value Schema:
    /// {
    ///       homeAccountId: home account identifier for the auth scheme,
    ///       environment: entity that issued the token, represented as a full host
    ///       credentialType: Type of credential as a string, can be one of the following: RefreshToken, AccessToken, IdToken, Password, Cookie, Certificate, Other
    ///       clientId: client ID of the application
    ///       secret: Actual credential as a string
    ///       familyId: Family ID identifier, usually only used for refresh tokens
    ///       realm: Full tenant or organizational identifier that the account belongs to
    ///       target: Permissions that are included in the token, or for refresh tokens, the resource identifier.
    ///       cachedAt: Absolute device time when entry was created in the cache.
    ///       expiresOn: Token expiry time, calculated based on current UTC time in seconds. Represented as a string.
    ///       extendedExpiresOn: Additional extended expiry time until when token is valid in case of server-side outage. Represented as string in UTC seconds.
    ///       keyId: used for POP and SSH tokenTypes
    ///       tokenType: Type of the token issued. Usually "Bearer"
    /// }
    type [<AllowNullLiteral>] AccessTokenEntityStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> AccessTokenEntity
        /// Create AccessTokenEntity
        abstract createAccessTokenEntity: homeAccountId: string * environment: string * accessToken: string * clientId: string * tenantId: string * scopes: string * expiresOn: float * extExpiresOn: float * cryptoUtils: ICrypto * ?refreshOn: float * ?tokenType: string * ?oboAssertion: string -> AccessTokenEntity
        /// Validates an entity: checks for all expected params
        abstract isAccessTokenEntity: entity: obj -> bool

[<AutoOpen>]
module __cache_entities_AccountEntity =
    type Authority = __authority_Authority.Authority
    type AuthToken = __account_AuthToken.AuthToken
    type ICrypto = __crypto_ICrypto.ICrypto
    type AccountInfo = __account_AccountInfo.AccountInfo
    type AuthorityType = __authority_AuthorityType.AuthorityType
    type Logger = __logger_Logger.Logger
    type TokenClaims = __account_TokenClaims.TokenClaims

    type [<AllowNullLiteral>] IExports =
        abstract AccountEntity: AccountEntityStatic

    /// Type that defines required and optional parameters for an Account field (based on universal cache schema implemented by all MSALs).
    ///
    /// Key : Value Schema
    ///
    /// Key: <home_account_id>-<environment>-<realm*>
    ///
    /// Value Schema:
    /// {
    ///       homeAccountId: home account identifier for the auth scheme,
    ///       environment: entity that issued the token, represented as a full host
    ///       realm: Full tenant or organizational identifier that the account belongs to
    ///       localAccountId: Original tenant-specific accountID, usually used for legacy cases
    ///       username: primary username that represents the user, usually corresponds to preferred_username in the v2 endpt
    ///       authorityType: Accounts authority type as a string
    ///       name: Full name for the account, including given name and family name,
    ///       clientInfo: Full base64 encoded client info received from ESTS
    ///       lastModificationTime: last time this entity was modified in the cache
    ///       lastModificationApp:
    ///       oboAssertion: access token passed in as part of OBO request
    ///       idTokenClaims: Object containing claims parsed from ID token
    /// }
    type [<AllowNullLiteral>] AccountEntity =
        abstract homeAccountId: string with get, set
        abstract environment: string with get, set
        abstract realm: string with get, set
        abstract localAccountId: string with get, set
        abstract username: string with get, set
        abstract authorityType: string with get, set
        abstract name: string option with get, set
        abstract clientInfo: string option with get, set
        abstract lastModificationTime: string option with get, set
        abstract lastModificationApp: string option with get, set
        abstract oboAssertion: string option with get, set
        abstract cloudGraphHostName: string option with get, set
        abstract msGraphHost: string option with get, set
        abstract idTokenClaims: TokenClaims option with get, set
        /// Generate Account Id key component as per the schema: <home_account_id>-<environment>
        abstract generateAccountId: unit -> string
        /// Generate Account Cache Key as per the schema: <home_account_id>-<environment>-<realm*>
        abstract generateAccountKey: unit -> string
        /// returns the type of the cache (in this case account)
        abstract generateType: unit -> float
        /// Returns the AccountInfo interface for this account.
        abstract getAccountInfo: unit -> AccountInfo

    /// Type that defines required and optional parameters for an Account field (based on universal cache schema implemented by all MSALs).
    ///
    /// Key : Value Schema
    ///
    /// Key: <home_account_id>-<environment>-<realm*>
    ///
    /// Value Schema:
    /// {
    ///       homeAccountId: home account identifier for the auth scheme,
    ///       environment: entity that issued the token, represented as a full host
    ///       realm: Full tenant or organizational identifier that the account belongs to
    ///       localAccountId: Original tenant-specific accountID, usually used for legacy cases
    ///       username: primary username that represents the user, usually corresponds to preferred_username in the v2 endpt
    ///       authorityType: Accounts authority type as a string
    ///       name: Full name for the account, including given name and family name,
    ///       clientInfo: Full base64 encoded client info received from ESTS
    ///       lastModificationTime: last time this entity was modified in the cache
    ///       lastModificationApp:
    ///       oboAssertion: access token passed in as part of OBO request
    ///       idTokenClaims: Object containing claims parsed from ID token
    /// }
    type [<AllowNullLiteral>] AccountEntityStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> AccountEntity
        /// Generates account key from interface
        abstract generateAccountCacheKey: accountInterface: AccountInfo -> string
        /// Build Account cache from IdToken, clientInfo and authority/policy. Associated with AAD.
        abstract createAccount: clientInfo: string * homeAccountId: string * authority: Authority * idToken: AuthToken * ?oboAssertion: string * ?cloudGraphHostName: string * ?msGraphHost: string -> AccountEntity
        /// Builds non-AAD/ADFS account.
        abstract createGenericAccount: authority: Authority * homeAccountId: string * idToken: AuthToken * ?oboAssertion: string * ?cloudGraphHostName: string * ?msGraphHost: string -> AccountEntity
        /// Generate HomeAccountId from server response
        abstract generateHomeAccountId: serverClientInfo: string * authType: AuthorityType * logger: Logger * cryptoObj: ICrypto * ?idToken: AuthToken -> string
        /// Validates an entity: checks for all expected params
        abstract isAccountEntity: entity: obj -> bool
        /// Helper function to determine whether 2 accounts are equal
        /// Used to avoid unnecessary state updates
        abstract accountInfoIsEqual: accountA: AccountInfo option * accountB: AccountInfo option -> bool

[<AutoOpen>]
module __cache_entities_AppMetadataEntity =

    type [<AllowNullLiteral>] IExports =
        abstract AppMetadataEntity: AppMetadataEntityStatic

    /// APP_METADATA Cache
    ///
    /// Key:Value Schema:
    ///
    /// Key: appmetadata-<environment>-<client_id>
    ///
    /// Value:
    /// {
    ///       clientId: client ID of the application
    ///       environment: entity that issued the token, represented as a full host
    ///       familyId: Family ID identifier, '1' represents Microsoft Family
    /// }
    type [<AllowNullLiteral>] AppMetadataEntity =
        abstract clientId: string with get, set
        abstract environment: string with get, set
        abstract familyId: string option with get, set
        /// Generate AppMetadata Cache Key as per the schema: appmetadata-<environment>-<client_id>
        abstract generateAppMetadataKey: unit -> string

    /// APP_METADATA Cache
    ///
    /// Key:Value Schema:
    ///
    /// Key: appmetadata-<environment>-<client_id>
    ///
    /// Value:
    /// {
    ///       clientId: client ID of the application
    ///       environment: entity that issued the token, represented as a full host
    ///       familyId: Family ID identifier, '1' represents Microsoft Family
    /// }
    type [<AllowNullLiteral>] AppMetadataEntityStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> AppMetadataEntity
        /// Generate AppMetadata Cache Key
        abstract generateAppMetadataCacheKey: environment: string * clientId: string -> string
        /// Creates AppMetadataEntity
        abstract createAppMetadataEntity: clientId: string * environment: string * ?familyId: string -> AppMetadataEntity
        /// Validates an entity: checks for all expected params
        abstract isAppMetadataEntity: key: string * entity: obj -> bool

[<AutoOpen>]
module __cache_entities_AuthorityMetadataEntity =
    type CloudDiscoveryMetadata = __authority_CloudDiscoveryMetadata.CloudDiscoveryMetadata
    type OpenIdConfigResponse = __authority_OpenIdConfigResponse.OpenIdConfigResponse

    type [<AllowNullLiteral>] IExports =
        abstract AuthorityMetadataEntity: AuthorityMetadataEntityStatic

    type [<AllowNullLiteral>] AuthorityMetadataEntity =
        abstract aliases: Array<string> with get, set
        abstract preferred_cache: string with get, set
        abstract preferred_network: string with get, set
        abstract canonical_authority: string with get, set
        abstract authorization_endpoint: string with get, set
        abstract token_endpoint: string with get, set
        abstract end_session_endpoint: string with get, set
        abstract issuer: string with get, set
        abstract aliasesFromNetwork: bool with get, set
        abstract endpointsFromNetwork: bool with get, set
        abstract expiresAt: float with get, set
        /// Update the entity with new aliases, preferred_cache and preferred_network values
        abstract updateCloudDiscoveryMetadata: metadata: CloudDiscoveryMetadata * fromNetwork: bool -> unit
        /// Update the entity with new endpoints
        abstract updateEndpointMetadata: metadata: OpenIdConfigResponse * fromNetwork: bool -> unit
        /// Save the authority that was used to create this cache entry
        abstract updateCanonicalAuthority: authority: string -> unit
        /// Reset the exiresAt value
        abstract resetExpiresAt: unit -> unit
        /// Returns whether or not the data needs to be refreshed
        abstract isExpired: unit -> bool

    type [<AllowNullLiteral>] AuthorityMetadataEntityStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> AuthorityMetadataEntity
        /// Validates an entity: checks for all expected params
        abstract isAuthorityMetadataEntity: key: string * entity: obj -> bool

[<AutoOpen>]
module __cache_entities_CacheRecord =
    type IdTokenEntity = __cache_entities_IdTokenEntity.IdTokenEntity
    type AccessTokenEntity = __cache_entities_AccessTokenEntity.AccessTokenEntity
    type RefreshTokenEntity = __cache_entities_RefreshTokenEntity.RefreshTokenEntity
    type AccountEntity = __cache_entities_AccountEntity.AccountEntity
    type AppMetadataEntity = __cache_entities_AppMetadataEntity.AppMetadataEntity

    type [<AllowNullLiteral>] IExports =
        abstract CacheRecord: CacheRecordStatic

    type [<AllowNullLiteral>] CacheRecord =
        abstract account: AccountEntity option with get, set
        abstract idToken: IdTokenEntity option with get, set
        abstract accessToken: AccessTokenEntity option with get, set
        abstract refreshToken: RefreshTokenEntity option with get, set
        abstract appMetadata: AppMetadataEntity option with get, set

    type [<AllowNullLiteral>] CacheRecordStatic =
        [<Emit "new $0($1...)">] abstract Create: ?accountEntity: AccountEntity * ?idTokenEntity: IdTokenEntity * ?accessTokenEntity: AccessTokenEntity * ?refreshTokenEntity: RefreshTokenEntity * ?appMetadataEntity: AppMetadataEntity -> CacheRecord

[<AutoOpen>]
module __cache_entities_CredentialEntity =
    type CredentialType = __utils_Constants.CredentialType

    type [<AllowNullLiteral>] IExports =
        abstract CredentialEntity: CredentialEntityStatic

    /// Base type for credentials to be stored in the cache: eg: ACCESS_TOKEN, ID_TOKEN etc
    ///
    /// Key:Value Schema:
    ///
    /// Key: <home_account_id*>-<environment>-<credential_type>-<client_id>-<realm*>-<target*>
    ///
    /// Value Schema:
    /// {
    ///       homeAccountId: home account identifier for the auth scheme,
    ///       environment: entity that issued the token, represented as a full host
    ///       credentialType: Type of credential as a string, can be one of the following: RefreshToken, AccessToken, IdToken, Password, Cookie, Certificate, Other
    ///       clientId: client ID of the application
    ///       secret: Actual credential as a string
    ///       familyId: Family ID identifier, usually only used for refresh tokens
    ///       realm: Full tenant or organizational identifier that the account belongs to
    ///       target: Permissions that are included in the token, or for refresh tokens, the resource identifier.
    ///       oboAssertion: access token passed in as part of OBO request
    /// }
    type [<AllowNullLiteral>] CredentialEntity =
        abstract homeAccountId: string with get, set
        abstract environment: string with get, set
        abstract credentialType: CredentialType with get, set
        abstract clientId: string with get, set
        abstract secret: string with get, set
        abstract familyId: string option with get, set
        abstract realm: string option with get, set
        abstract target: string option with get, set
        abstract oboAssertion: string option with get, set
        /// Generate Account Id key component as per the schema: <home_account_id>-<environment>
        abstract generateAccountId: unit -> string
        /// Generate Credential Id key component as per the schema: <credential_type>-<client_id>-<realm>
        abstract generateCredentialId: unit -> string
        /// Generate target key component as per schema: <target>
        abstract generateTarget: unit -> string
        /// generates credential key
        abstract generateCredentialKey: unit -> string
        /// returns the type of the cache (in this case credential)
        abstract generateType: unit -> float

    /// Base type for credentials to be stored in the cache: eg: ACCESS_TOKEN, ID_TOKEN etc
    ///
    /// Key:Value Schema:
    ///
    /// Key: <home_account_id*>-<environment>-<credential_type>-<client_id>-<realm*>-<target*>
    ///
    /// Value Schema:
    /// {
    ///       homeAccountId: home account identifier for the auth scheme,
    ///       environment: entity that issued the token, represented as a full host
    ///       credentialType: Type of credential as a string, can be one of the following: RefreshToken, AccessToken, IdToken, Password, Cookie, Certificate, Other
    ///       clientId: client ID of the application
    ///       secret: Actual credential as a string
    ///       familyId: Family ID identifier, usually only used for refresh tokens
    ///       realm: Full tenant or organizational identifier that the account belongs to
    ///       target: Permissions that are included in the token, or for refresh tokens, the resource identifier.
    ///       oboAssertion: access token passed in as part of OBO request
    /// }
    type [<AllowNullLiteral>] CredentialEntityStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> CredentialEntity
        /// helper function to return `CredentialType`
        abstract getCredentialType: key: string -> string
        /// generates credential key
        abstract generateCredentialCacheKey: homeAccountId: string * environment: string * credentialType: CredentialType * clientId: string * ?realm: string * ?target: string * ?familyId: string -> string

[<AutoOpen>]
module __cache_entities_IdTokenEntity =
    type CredentialEntity = __cache_entities_CredentialEntity.CredentialEntity

    type [<AllowNullLiteral>] IExports =
        abstract IdTokenEntity: IdTokenEntityStatic

    /// ID_TOKEN Cache
    ///
    /// Key:Value Schema:
    ///
    /// Key Example: uid.utid-login.microsoftonline.com-idtoken-clientId-contoso.com-
    ///
    /// Value Schema:
    /// {
    ///       homeAccountId: home account identifier for the auth scheme,
    ///       environment: entity that issued the token, represented as a full host
    ///       credentialType: Type of credential as a string, can be one of the following: RefreshToken, AccessToken, IdToken, Password, Cookie, Certificate, Other
    ///       clientId: client ID of the application
    ///       secret: Actual credential as a string
    ///       realm: Full tenant or organizational identifier that the account belongs to
    /// }
    type [<AllowNullLiteral>] IdTokenEntity =
        inherit CredentialEntity
        abstract realm: string with get, set

    /// ID_TOKEN Cache
    ///
    /// Key:Value Schema:
    ///
    /// Key Example: uid.utid-login.microsoftonline.com-idtoken-clientId-contoso.com-
    ///
    /// Value Schema:
    /// {
    ///       homeAccountId: home account identifier for the auth scheme,
    ///       environment: entity that issued the token, represented as a full host
    ///       credentialType: Type of credential as a string, can be one of the following: RefreshToken, AccessToken, IdToken, Password, Cookie, Certificate, Other
    ///       clientId: client ID of the application
    ///       secret: Actual credential as a string
    ///       realm: Full tenant or organizational identifier that the account belongs to
    /// }
    type [<AllowNullLiteral>] IdTokenEntityStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> IdTokenEntity
        /// Create IdTokenEntity
        abstract createIdTokenEntity: homeAccountId: string * environment: string * idToken: string * clientId: string * tenantId: string * ?oboAssertion: string -> IdTokenEntity
        /// Validates an entity: checks for all expected params
        abstract isIdTokenEntity: entity: obj -> bool

[<AutoOpen>]
module __cache_entities_RefreshTokenEntity =
    type CredentialEntity = __cache_entities_CredentialEntity.CredentialEntity

    type [<AllowNullLiteral>] IExports =
        abstract RefreshTokenEntity: RefreshTokenEntityStatic

    /// REFRESH_TOKEN Cache
    ///
    /// Key:Value Schema:
    ///
    /// Key Example: uid.utid-login.microsoftonline.com-refreshtoken-clientId--
    ///
    /// Value:
    /// {
    ///       homeAccountId: home account identifier for the auth scheme,
    ///       environment: entity that issued the token, represented as a full host
    ///       credentialType: Type of credential as a string, can be one of the following: RefreshToken, AccessToken, IdToken, Password, Cookie, Certificate, Other
    ///       clientId: client ID of the application
    ///       secret: Actual credential as a string
    ///       familyId: Family ID identifier, '1' represents Microsoft Family
    ///       realm: Full tenant or organizational identifier that the account belongs to
    ///       target: Permissions that are included in the token, or for refresh tokens, the resource identifier.
    /// }
    type [<AllowNullLiteral>] RefreshTokenEntity =
        inherit CredentialEntity
        abstract familyId: string option with get, set

    /// REFRESH_TOKEN Cache
    ///
    /// Key:Value Schema:
    ///
    /// Key Example: uid.utid-login.microsoftonline.com-refreshtoken-clientId--
    ///
    /// Value:
    /// {
    ///       homeAccountId: home account identifier for the auth scheme,
    ///       environment: entity that issued the token, represented as a full host
    ///       credentialType: Type of credential as a string, can be one of the following: RefreshToken, AccessToken, IdToken, Password, Cookie, Certificate, Other
    ///       clientId: client ID of the application
    ///       secret: Actual credential as a string
    ///       familyId: Family ID identifier, '1' represents Microsoft Family
    ///       realm: Full tenant or organizational identifier that the account belongs to
    ///       target: Permissions that are included in the token, or for refresh tokens, the resource identifier.
    /// }
    type [<AllowNullLiteral>] RefreshTokenEntityStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> RefreshTokenEntity
        /// Create RefreshTokenEntity
        abstract createRefreshTokenEntity: homeAccountId: string * environment: string * refreshToken: string * clientId: string * ?familyId: string * ?oboAssertion: string -> RefreshTokenEntity
        /// Validates an entity: checks for all expected params
        abstract isRefreshTokenEntity: entity: obj -> bool

[<AutoOpen>]
module __cache_entities_ServerTelemetryEntity =

    type [<AllowNullLiteral>] IExports =
        abstract ServerTelemetryEntity: ServerTelemetryEntityStatic

    type [<AllowNullLiteral>] ServerTelemetryEntity =
        abstract failedRequests: Array<U2<string, float>> with get, set
        abstract errors: ResizeArray<string> with get, set
        abstract cacheHits: float with get, set

    type [<AllowNullLiteral>] ServerTelemetryEntityStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> ServerTelemetryEntity
        /// validates if a given cache entry is "Telemetry", parses <key,value>
        abstract isServerTelemetryEntity: key: string * ?entity: obj -> bool

[<AutoOpen>]
module __cache_entities_ThrottlingEntity =

    type [<AllowNullLiteral>] IExports =
        abstract ThrottlingEntity: ThrottlingEntityStatic

    type [<AllowNullLiteral>] ThrottlingEntity =
        abstract throttleTime: float with get, set
        abstract error: string option with get, set
        abstract errorCodes: Array<string> option with get, set
        abstract errorMessage: string option with get, set
        abstract subError: string option with get, set

    type [<AllowNullLiteral>] ThrottlingEntityStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> ThrottlingEntity
        /// validates if a given cache entry is "Throttling", parses <key,value>
        abstract isThrottlingEntity: key: string * ?entity: obj -> bool

[<AutoOpen>]
module __cache_interface_ICacheManager =
    type CredentialEntity = __cache_entities_CredentialEntity.CredentialEntity
    type AccountCache = __cache_utils_CacheTypes.AccountCache
    type CredentialCache = __cache_utils_CacheTypes.CredentialCache
    type AccountFilter = __cache_utils_CacheTypes.AccountFilter
    type CredentialFilter = __cache_utils_CacheTypes.CredentialFilter
    type CacheRecord = __cache_entities_CacheRecord.CacheRecord
    type AccountEntity = __cache_entities_AccountEntity.AccountEntity
    type AccountInfo = __account_AccountInfo.AccountInfo
    type AppMetadataEntity = __cache_entities_AppMetadataEntity.AppMetadataEntity
    type ServerTelemetryEntity = __cache_entities_ServerTelemetryEntity.ServerTelemetryEntity
    type ThrottlingEntity = __cache_entities_ThrottlingEntity.ThrottlingEntity
    type IdTokenEntity = __cache_entities_IdTokenEntity.IdTokenEntity
    type AccessTokenEntity = __cache_entities_AccessTokenEntity.AccessTokenEntity
    type RefreshTokenEntity = __cache_entities_RefreshTokenEntity.RefreshTokenEntity
    type AuthorityMetadataEntity = __cache_entities_AuthorityMetadataEntity.AuthorityMetadataEntity

    type [<AllowNullLiteral>] ICacheManager =
        /// fetch the account entity from the platform cache
        abstract getAccount: accountKey: string -> AccountEntity option
        /// set account entity in the platform cache
        abstract setAccount: account: AccountEntity -> unit
        /// fetch the idToken entity from the platform cache
        abstract getIdTokenCredential: idTokenKey: string -> IdTokenEntity option
        /// set idToken entity to the platform cache
        abstract setIdTokenCredential: idToken: IdTokenEntity -> unit
        /// fetch the idToken entity from the platform cache
        abstract getAccessTokenCredential: accessTokenKey: string -> AccessTokenEntity option
        /// set idToken entity to the platform cache
        abstract setAccessTokenCredential: accessToken: AccessTokenEntity -> unit
        /// fetch the idToken entity from the platform cache
        abstract getRefreshTokenCredential: refreshTokenKey: string -> RefreshTokenEntity option
        /// set idToken entity to the platform cache
        abstract setRefreshTokenCredential: refreshToken: RefreshTokenEntity -> unit
        /// fetch appMetadata entity from the platform cache
        abstract getAppMetadata: appMetadataKey: string -> AppMetadataEntity option
        /// set appMetadata entity to the platform cache
        abstract setAppMetadata: appMetadata: AppMetadataEntity -> unit
        /// fetch server telemetry entity from the platform cache
        abstract getServerTelemetry: serverTelemetryKey: string -> ServerTelemetryEntity option
        /// set server telemetry entity to the platform cache
        abstract setServerTelemetry: serverTelemetryKey: string * serverTelemetry: ServerTelemetryEntity -> unit
        /// fetch cloud discovery metadata entity from the platform cache
        abstract getAuthorityMetadata: key: string -> AuthorityMetadataEntity option
        /// Get cache keys for authority metadata
        abstract getAuthorityMetadataKeys: unit -> Array<string>
        /// set cloud discovery metadata entity to the platform cache
        abstract setAuthorityMetadata: key: string * value: AuthorityMetadataEntity -> unit
        /// Provide an alias to find a matching AuthorityMetadataEntity in cache
        abstract getAuthorityMetadataByAlias: host: string -> AuthorityMetadataEntity option
        /// given an authority generates the cache key for authorityMetadata
        abstract generateAuthorityMetadataCacheKey: authority: string -> string
        /// fetch throttling entity from the platform cache
        abstract getThrottlingCache: throttlingCacheKey: string -> ThrottlingEntity option
        /// set throttling entity to the platform cache
        abstract setThrottlingCache: throttlingCacheKey: string * throttlingCache: ThrottlingEntity -> unit
        /// Returns all accounts in cache
        abstract getAllAccounts: unit -> ResizeArray<AccountInfo>
        /// saves a cache record
        abstract saveCacheRecord: cacheRecord: CacheRecord -> unit
        /// retrieve accounts matching all provided filters; if no filter is set, get all accounts
        abstract getAccountsFilteredBy: filter: AccountFilter -> AccountCache
        /// retrieve credentials matching all provided filters; if no filter is set, get all credentials
        abstract getCredentialsFilteredBy: filter: CredentialFilter -> CredentialCache
        /// Removes all accounts and related tokens from cache.
        abstract removeAllAccounts: unit -> bool
        /// returns a boolean if the given account is removed
        abstract removeAccount: accountKey: string -> bool
        /// returns a boolean if the given account is removed
        abstract removeAccountContext: account: AccountEntity -> bool
        /// returns a boolean if the given credential is removed
        abstract removeCredential: credential: CredentialEntity -> bool

[<AutoOpen>]
module __cache_interface_ICachePlugin =
    type TokenCacheContext = __cache_persistence_TokenCacheContext.TokenCacheContext

    type [<AllowNullLiteral>] ICachePlugin =
        abstract beforeCacheAccess: (TokenCacheContext -> Promise<unit>) with get, set
        abstract afterCacheAccess: (TokenCacheContext -> Promise<unit>) with get, set

[<AutoOpen>]
module __cache_interface_ISerializableTokenCache =

    type [<AllowNullLiteral>] ISerializableTokenCache =
        abstract deserialize: (string -> unit) with get, set
        abstract serialize: (unit -> string) with get, set

[<AutoOpen>]
module __cache_persistence_TokenCacheContext =
    type ISerializableTokenCache = __cache_interface_ISerializableTokenCache.ISerializableTokenCache

    type [<AllowNullLiteral>] IExports =
        abstract TokenCacheContext: TokenCacheContextStatic

    /// This class instance helps track the memory changes facilitating
    /// decisions to read from and write to the persistent cache
    type [<AllowNullLiteral>] TokenCacheContext =
        /// boolean indicating cache change
        abstract hasChanged: bool with get, set
        /// serializable token cache interface
        abstract cache: ISerializableTokenCache with get, set
        // obj
        // obj

    /// This class instance helps track the memory changes facilitating
    /// decisions to read from and write to the persistent cache
    type [<AllowNullLiteral>] TokenCacheContextStatic =
        [<Emit "new $0($1...)">] abstract Create: tokenCache: ISerializableTokenCache * hasChanged: bool -> TokenCacheContext

[<AutoOpen>]
module __cache_utils_CacheTypes =
    type AccountEntity = __cache_entities_AccountEntity.AccountEntity
    type IdTokenEntity = __cache_entities_IdTokenEntity.IdTokenEntity
    type AccessTokenEntity = __cache_entities_AccessTokenEntity.AccessTokenEntity
    type RefreshTokenEntity = __cache_entities_RefreshTokenEntity.RefreshTokenEntity
    type AppMetadataEntity = __cache_entities_AppMetadataEntity.AppMetadataEntity
    type ServerTelemetryEntity = __cache_entities_ServerTelemetryEntity.ServerTelemetryEntity
    type ThrottlingEntity = __cache_entities_ThrottlingEntity.ThrottlingEntity
    type AuthorityMetadataEntity = __cache_entities_AuthorityMetadataEntity.AuthorityMetadataEntity

    type AccountCache =
        Record<string, AccountEntity>

    type IdTokenCache =
        Record<string, IdTokenEntity>

    type AccessTokenCache =
        Record<string, AccessTokenEntity>

    type RefreshTokenCache =
        Record<string, RefreshTokenEntity>

    type AppMetadataCache =
        Record<string, AppMetadataEntity>

    type [<AllowNullLiteral>] CredentialCache =
        abstract idTokens: IdTokenCache with get, set
        abstract accessTokens: AccessTokenCache with get, set
        abstract refreshTokens: RefreshTokenCache with get, set

    type ValidCacheType =
        obj

    type ValidCredentialType =
        U3<IdTokenEntity, AccessTokenEntity, RefreshTokenEntity>

    type [<AllowNullLiteral>] AccountFilter =
        abstract homeAccountId: string option with get, set
        abstract environment: string option with get, set
        abstract realm: string option with get, set

    type [<AllowNullLiteral>] CredentialFilter =
        abstract homeAccountId: string option with get, set
        abstract environment: string option with get, set
        abstract credentialType: string option with get, set
        abstract clientId: string option with get, set
        abstract familyId: string option with get, set
        abstract realm: string option with get, set
        abstract target: string option with get, set
        abstract oboAssertion: string option with get, set

    type [<AllowNullLiteral>] AppMetadataFilter =
        abstract environment: string option with get, set
        abstract clientId: string option with get, set

[<AutoOpen>]
module __telemetry_server_ServerTelemetryManager =
    type CacheManager = __cache_CacheManager.CacheManager
    type AuthError = __error_AuthError.AuthError
    type ServerTelemetryRequest = __telemetry_server_ServerTelemetryRequest.ServerTelemetryRequest
    type ServerTelemetryEntity = __cache_entities_ServerTelemetryEntity.ServerTelemetryEntity

    type [<AllowNullLiteral>] IExports =
        abstract ServerTelemetryManager: ServerTelemetryManagerStatic

    type [<AllowNullLiteral>] ServerTelemetryManager =
        /// API to add MSER Telemetry to request
        abstract generateCurrentRequestHeaderValue: unit -> string
        /// API to add MSER Telemetry for the last failed request
        abstract generateLastRequestHeaderValue: unit -> string
        /// API to cache token failures for MSER data capture
        abstract cacheFailedRequest: error: AuthError -> unit
        /// Update server telemetry cache entry by incrementing cache hit counter
        abstract incrementCacheHits: unit -> float
        /// Get the server telemetry entity from cache or initialize a new one
        abstract getLastRequests: unit -> ServerTelemetryEntity
        /// Remove server telemetry cache entry
        abstract clearTelemetryCache: unit -> unit

    type [<AllowNullLiteral>] ServerTelemetryManagerStatic =
        [<Emit "new $0($1...)">] abstract Create: telemetryRequest: ServerTelemetryRequest * cacheManager: CacheManager -> ServerTelemetryManager
        /// Returns the maximum number of errors that can be flushed to the server in the next network request
        abstract maxErrorsToSend: serverTelemetryEntity: ServerTelemetryEntity -> float

[<AutoOpen>]
module __telemetry_server_ServerTelemetryRequest =

    type [<AllowNullLiteral>] ServerTelemetryRequest =
        abstract clientId: string with get, set
        abstract apiId: float with get, set
        abstract correlationId: string with get, set
        abstract forceRefresh: bool option with get, set
        abstract wrapperSKU: string option with get, set
        abstract wrapperVer: string option with get, set
