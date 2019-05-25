// ts2fable 0.6.2
module rec Msal
open System
open Fable.Core
open Fable.Core.JS

[<Import("UserAgentApplication","msal")>]
let UserAgentApplication: UserAgentApplicationStatic =
    jsNative

let [<Import("AuthErrorMessage","module")>] AuthErrorMessage: obj = jsNative
let [<Import("ClientAuthErrorMessage","module")>] ClientAuthErrorMessage: obj = jsNative
let [<Import("ClientConfigurationErrorMessage","module")>] ClientConfigurationErrorMessage: obj = jsNative
let [<Import("InteractionRequiredAuthErrorMessage","module")>] InteractionRequiredAuthErrorMessage: obj = jsNative
let [<Import("ServerErrorMessage","module")>] ServerErrorMessage: obj = jsNative
let [<Import("CacheKeys","module")>] CacheKeys: obj = jsNative
let [<Import("SSOTypes","module")>] SSOTypes: obj = jsNative
let [<Import("PromptState","module")>] PromptState: obj = jsNative
let [<Import("Library","module")>] Library: obj = jsNative

type [<AllowNullLiteral>] IExports =
    abstract AuthError: AuthErrorStatic
    abstract ClientAuthError: ClientAuthErrorStatic
    abstract ClientConfigurationError: ClientConfigurationErrorStatic
    abstract InteractionRequiredAuthError: InteractionRequiredAuthErrorStatic
    abstract ServerError: ServerErrorStatic
    abstract AadAuthority: AadAuthorityStatic
    abstract AccessTokenCacheItem: AccessTokenCacheItemStatic
    abstract AccessTokenKey: AccessTokenKeyStatic
    abstract AccessTokenValue: AccessTokenValueStatic
    abstract Account: AccountStatic
    abstract validateClaimsRequest: request: AuthenticationParameters -> unit
    abstract Authority: AuthorityStatic
    abstract AuthorityFactory: AuthorityFactoryStatic
    abstract buildResponseStateOnly: state: string -> AuthResponse
    abstract B2cAuthority: B2cAuthorityStatic
    abstract ClientInfo: ClientInfoStatic
    abstract FRAME_TIMEOUT: obj
    abstract OFFSET: obj
    abstract NAVIGATE_FRAME_WAIT: obj
    abstract DEFAULT_AUTH_OPTIONS: AuthOptions
    abstract DEFAULT_CACHE_OPTIONS: CacheOptions
    abstract DEFAULT_SYSTEM_OPTIONS: SystemOptions
    abstract DEFAULT_FRAMEWORK_OPTIONS: FrameworkOptions
    /// Function to set the default options when not explicitly set
    // abstract buildConfiguration: { auth, cache = {}, system = {}, framework = {}}: Configuration -> Configuration
    abstract Constants: ConstantsStatic
    abstract IdToken: IdTokenStatic
    abstract Logger: LoggerStatic
    abstract ServerRequestParameters: ServerRequestParametersStatic
    abstract Storage: StorageStatic
    abstract Telemetry: TelemetryStatic
    abstract DEFAULT_AUTHORITY: obj
    abstract ResponseTypes: obj
    abstract resolveTokenOnlyIfOutOfIframe: obj
    abstract UserAgentApplication: UserAgentApplicationStatic
    abstract Utils: UtilsStatic
    abstract XhrClient: XhrClientStatic

/// General error class thrown by the MSAL.js library.
type [<AllowNullLiteral>] AuthError =
    // inherit Error
    abstract errorCode: string with get, set
    abstract errorMessage: string with get, set

/// General error class thrown by the MSAL.js library.
type [<AllowNullLiteral>] AuthErrorStatic =
    [<Emit "new $0($1...)">] abstract Create: errorCode: string * ?errorMessage: string -> AuthError
    abstract createUnexpectedError: errDesc: string -> unit

/// Error thrown when there is an error in the client code running on the browser.
type [<AllowNullLiteral>] ClientAuthError =
    inherit AuthError

/// Error thrown when there is an error in the client code running on the browser.
type [<AllowNullLiteral>] ClientAuthErrorStatic =
    [<Emit "new $0($1...)">] abstract Create: errorCode: string * ?errorMessage: string -> ClientAuthError
    abstract createEndpointResolutionError: ?errDetail: string -> ClientAuthError
    abstract createMultipleMatchingTokensInCacheError: scope: string -> ClientAuthError
    abstract createMultipleAuthoritiesInCacheError: scope: string -> ClientAuthError
    abstract createPopupWindowError: ?errDetail: string -> ClientAuthError
    abstract createTokenRenewalTimeoutError: unit -> ClientAuthError
    abstract createInvalidIdTokenError: idToken: IdToken -> ClientAuthError
    abstract createInvalidStateError: invalidState: string * actualState: string -> ClientAuthError
    abstract createNonceMismatchError: invalidNonce: string * actualNonce: string -> ClientAuthError
    abstract createLoginInProgressError: unit -> ClientAuthError
    abstract createAcquireTokenInProgressError: unit -> ClientAuthError
    abstract createUserCancelledError: unit -> ClientAuthError
    abstract createErrorInCallbackFunction: errorDesc: string -> ClientAuthError
    abstract createUserLoginRequiredError: unit -> ClientAuthError
    abstract createUserDoesNotExistError: unit -> ClientAuthError
    abstract createClientInfoDecodingError: caughtError: string -> ClientAuthError
    abstract createClientInfoNotPopulatedError: caughtError: string -> ClientAuthError
    abstract createIdTokenNullOrEmptyError: invalidRawTokenString: string -> ClientAuthError
    abstract createIdTokenParsingError: caughtParsingError: string -> ClientAuthError
    abstract createTokenEncodingError: incorrectlyEncodedToken: string -> ClientAuthError

/// Error thrown when there is an error in configuration of the .js library.
type [<AllowNullLiteral>] ClientConfigurationError =
    inherit ClientAuthError

/// Error thrown when there is an error in configuration of the .js library.
type [<AllowNullLiteral>] ClientConfigurationErrorStatic =
    [<Emit "new $0($1...)">] abstract Create: errorCode: string * ?errorMessage: string -> ClientConfigurationError
    abstract createNoSetConfigurationError: unit -> ClientConfigurationError
    abstract createInvalidCacheLocationConfigError: givenCacheLocation: string -> ClientConfigurationError
    abstract createNoStorageSupportedError: unit -> ClientConfigurationError
    abstract createRedirectCallbacksNotSetError: unit -> ClientConfigurationError
    abstract createInvalidCallbackObjectError: callbackObject: obj -> ClientConfigurationError
    abstract createEmptyScopesArrayError: scopesValue: string -> ClientConfigurationError
    abstract createScopesNonArrayError: scopesValue: string -> ClientConfigurationError
    abstract createClientIdSingleScopeError: scopesValue: string -> ClientConfigurationError
    abstract createScopesRequiredError: scopesValue: obj option -> ClientConfigurationError
    abstract createInvalidPromptError: promptValue: obj option -> ClientConfigurationError
    abstract createClaimsRequestParsingError: claimsRequestParseError: string -> ClientConfigurationError

/// Error thrown when the user is required to perform an interactive token request.
type [<AllowNullLiteral>] InteractionRequiredAuthError =
    inherit ServerError

/// Error thrown when the user is required to perform an interactive token request.
type [<AllowNullLiteral>] InteractionRequiredAuthErrorStatic =
    [<Emit "new $0($1...)">] abstract Create: errorCode: string * ?errorMessage: string -> InteractionRequiredAuthError
    abstract createLoginRequiredAuthError: errorDesc: string -> InteractionRequiredAuthError
    abstract createInteractionRequiredAuthError: errorDesc: string -> InteractionRequiredAuthError
    abstract createConsentRequiredAuthError: errorDesc: string -> InteractionRequiredAuthError

/// Error thrown when there is an error with the server code, for example, unavailability.
type [<AllowNullLiteral>] ServerError =
    inherit AuthError

/// Error thrown when there is an error with the server code, for example, unavailability.
type [<AllowNullLiteral>] ServerErrorStatic =
    [<Emit "new $0($1...)">] abstract Create: errorCode: string * ?errorMessage: string -> ServerError
    abstract createServerUnavailableError: unit -> ServerError
    abstract createUnknownServerError: errorDesc: string -> ServerError

type [<AllowNullLiteral>] AadAuthority =
    inherit Authority
    // obj
    // obj
    /// Returns a promise which resolves to the OIDC endpoint
    /// Only responds with the endpoint
    abstract GetOpenIdConfigurationEndpointAsync: unit -> Promise<string>
    /// Checks to see if the host is in a list of trusted hosts
    abstract IsInTrustedHostList: host: string -> bool

type [<AllowNullLiteral>] AadAuthorityStatic =
    [<Emit "new $0($1...)">] abstract Create: authority: string * validateAuthority: bool -> AadAuthority

type [<AllowNullLiteral>] AccessTokenCacheItem =
    abstract key: AccessTokenKey with get, set
    abstract value: AccessTokenValue with get, set

type [<AllowNullLiteral>] AccessTokenCacheItemStatic =
    [<Emit "new $0($1...)">] abstract Create: key: AccessTokenKey * value: AccessTokenValue -> AccessTokenCacheItem

type [<AllowNullLiteral>] AccessTokenKey =
    abstract authority: string with get, set
    abstract clientId: string with get, set
    abstract scopes: string with get, set
    abstract homeAccountIdentifier: string with get, set

type [<AllowNullLiteral>] AccessTokenKeyStatic =
    [<Emit "new $0($1...)">] abstract Create: authority: string * clientId: string * scopes: string * uid: string * utid: string -> AccessTokenKey

type [<AllowNullLiteral>] AccessTokenValue =
    abstract accessToken: string with get, set
    abstract idToken: string with get, set
    abstract expiresIn: string with get, set
    abstract homeAccountIdentifier: string with get, set

type [<AllowNullLiteral>] AccessTokenValueStatic =
    [<Emit "new $0($1...)">] abstract Create: accessToken: string * idToken: string * expiresIn: string * homeAccountIdentifier: string -> AccessTokenValue

/// accountIdentifier       combination of idToken.uid and idToken.utid
/// homeAccountIdentifier   combination of clientInfo.uid and clientInfo.utid
/// userName                idToken.preferred_username
/// name                    idToken.name
/// idToken                 idToken
/// sid                     idToken.sid - session identifier
/// environment             idtoken.issuer (the authority that issues the token)
type [<AllowNullLiteral>] Account =
    abstract accountIdentifier: string with get, set
    abstract homeAccountIdentifier: string with get, set
    abstract userName: string with get, set
    abstract name: string with get, set
    abstract idToken: Object with get, set
    abstract sid: string with get, set
    abstract environment: string with get, set

/// accountIdentifier       combination of idToken.uid and idToken.utid
/// homeAccountIdentifier   combination of clientInfo.uid and clientInfo.utid
/// userName                idToken.preferred_username
/// name                    idToken.name
/// idToken                 idToken
/// sid                     idToken.sid - session identifier
/// environment             idtoken.issuer (the authority that issues the token)
type [<AllowNullLiteral>] AccountStatic =
    /// <summary>Creates an Account Object</summary>
    /// <param name="homeAccountIdentifier"></param>
    /// <param name="userName"></param>
    /// <param name="name"></param>
    /// <param name="idToken"></param>
    /// <param name="sid"></param>
    /// <param name="environment"></param>
    [<Emit "new $0($1...)">] abstract Create: accountIdentifier: string * homeAccountIdentifier: string * userName: string * name: string * idToken: Object * sid: string * environment: string -> Account
    /// <param name="idToken"></param>
    /// <param name="clientInfo"></param>
    abstract createAccount: idToken: IdToken * clientInfo: ClientInfo -> Account

type [<AllowNullLiteral>] QPDict =
    [<Emit "$0[$1]{{=$2}}">] abstract Item: key: string -> string with get, set

type [<AllowNullLiteral>] AuthenticationParameters =
    abstract scopes: ResizeArray<string> option with get, set
    abstract extraScopesToConsent: ResizeArray<string> option with get, set
    abstract prompt: string option with get, set
    abstract extraQueryParameters: QPDict option with get, set
    abstract claimsRequest: string option with get, set
    abstract authority: string option with get, set
    abstract state: string option with get, set
    abstract correlationId: string option with get, set
    abstract account: Account option with get, set
    abstract sid: string option with get, set
    abstract loginHint: string option with get, set

type AuthorityType =
    obj

type [<AllowNullLiteral>] Authority =
    // obj
    abstract IsValidationEnabled: bool with get, set
    // obj
    // obj
    // obj
    // obj
    // obj
    // obj
    // obj
    // obj
    /// Returns a promise.
    /// Checks to see if the authority is in the cache
    /// Discover endpoints via openid-configuration
    /// If successful, caches the endpoint for later use in OIDC
    abstract resolveEndpointsAsync: unit -> Promise<Authority>
    /// Returns a promise with the TenantDiscoveryEndpoint
    abstract GetOpenIdConfigurationEndpointAsync: unit -> Promise<string>

type [<AllowNullLiteral>] AuthorityStatic =
    [<Emit "new $0($1...)">] abstract Create: authority: string * validateAuthority: bool -> Authority

type [<AllowNullLiteral>] AuthorityFactory =
    interface end

type [<AllowNullLiteral>] AuthorityFactoryStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> AuthorityFactory
    /// Create an authority object of the correct type based on the url
    /// Performs basic authority validation - checks to see if the authority is of a valid type (eg aad, b2c)
    abstract CreateInstance: authorityUrl: string * validateAuthority: bool -> Authority

type [<AllowNullLiteral>] AuthResponse =
    abstract uniqueId: string with get, set
    abstract tenantId: string with get, set
    abstract tokenType: string with get, set
    abstract idToken: IdToken with get, set
    abstract accessToken: string with get, set
    abstract scopes: ResizeArray<string> with get, set
    abstract expiresOn: DateTime with get, set
    abstract account: Account with get, set
    abstract accountState: string with get, set

type [<AllowNullLiteral>] B2cAuthority =
    inherit AadAuthority
    // obj
    /// Returns a promise with the TenantDiscoveryEndpoint
    abstract GetOpenIdConfigurationEndpointAsync: unit -> Promise<string>

type [<AllowNullLiteral>] B2cAuthorityStatic =
    [<Emit "new $0($1...)">] abstract Create: authority: string * validateAuthority: bool -> B2cAuthority

type [<AllowNullLiteral>] ClientInfo = class end
//     obj
//     obj
//     obj
//     obj

type [<AllowNullLiteral>] ClientInfoStatic =
    [<Emit "new $0($1...)">] abstract Create: rawClientInfo: string -> ClientInfo

type [<StringEnum>] [<RequireQualifiedAccess>] CacheLocation =
    | LocalStorage
    | SessionStorage

type [<AllowNullLiteral>] AuthOptions =
    abstract clientId: string with get, set
    abstract authority: string option with get, set
    abstract validateAuthority: bool option with get, set
    abstract redirectUri: U2<string, (unit -> string)> option with get, set
    abstract postLogoutRedirectUri: U2<string, (unit -> string)> option with get, set
    abstract navigateToLoginRequestUrl: bool option with get, set

type [<AllowNullLiteral>] CacheOptions =
    abstract cacheLocation: CacheLocation option with get, set
    abstract storeAuthStateInCookie: bool option with get, set

type [<AllowNullLiteral>] SystemOptions =
    abstract logger: Logger option with get, set
    abstract loadFrameTimeout: float option with get, set
    abstract tokenRenewalOffsetSeconds: float option with get, set
    abstract navigateFrameWait: float option with get, set

type [<AllowNullLiteral>] FrameworkOptions =
    abstract isAngular: bool option with get, set
    abstract unprotectedResources: ResizeArray<string> option with get, set
    abstract protectedResourceMap: Map<string, ResizeArray<string>> option with get, set

type [<AllowNullLiteral>] Configuration =
    abstract auth: AuthOptions with get, set
    abstract cache: CacheOptions option with get, set
    abstract system: SystemOptions option with get, set
    abstract framework: FrameworkOptions option with get, set

type [<AllowNullLiteral>] Constants = class end
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj
//     obj

type [<AllowNullLiteral>] ConstantsStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> Constants

type [<AllowNullLiteral>] IdToken =
    abstract issuer: string with get, set
    abstract objectId: string with get, set
    abstract subject: string with get, set
    abstract tenantId: string with get, set
    abstract version: string with get, set
    abstract preferredName: string with get, set
    abstract name: string with get, set
    abstract homeObjectId: string with get, set
    abstract nonce: string with get, set
    abstract expiration: string with get, set
    abstract rawIdToken: string with get, set
    abstract decodedIdToken: Object with get, set
    abstract sid: string with get, set

type [<AllowNullLiteral>] IdTokenStatic =
    [<Emit "new $0($1...)">] abstract Create: rawIdToken: string -> IdToken

type [<AllowNullLiteral>] IInstanceDiscoveryResponse =
    abstract TenantDiscoveryEndpoint: string with get, set

type [<AllowNullLiteral>] ITenantDiscoveryResponse =
    abstract AuthorizationEndpoint: string with get, set
    abstract EndSessionEndpoint: string with get, set
    abstract Issuer: string with get, set

type [<AllowNullLiteral>] IUri =
    abstract Protocol: string with get, set
    abstract HostNameAndPort: string with get, set
    abstract AbsolutePath: string with get, set
    abstract Search: string with get, set
    abstract Hash: string with get, set
    abstract PathSegments: ResizeArray<string> with get, set

type [<AllowNullLiteral>] ILoggerCallback =
    [<Emit "$0($1...)">] abstract Invoke: level: LogLevel * message: string * containsPii: bool -> unit

type LogLevel =
    obj

type [<AllowNullLiteral>] Logger =
    abstract executeCallback: level: LogLevel * message: string * containsPii: bool -> unit
    abstract error: message: string -> unit
    abstract errorPii: message: string -> unit
    abstract warning: message: string -> unit
    abstract warningPii: message: string -> unit
    abstract info: message: string -> unit
    abstract infoPii: message: string -> unit
    abstract verbose: message: string -> unit
    abstract verbosePii: message: string -> unit

type [<AllowNullLiteral>] LoggerStatic =
    [<Emit "new $0($1...)">] abstract Create: localCallback: ILoggerCallback * options: LoggerStaticOptions -> Logger

type [<AllowNullLiteral>] LoggerStaticOptions =
    abstract correlationId: string option with get, set
    abstract level: LogLevel option with get, set
    abstract piiLoggingEnabled: bool option with get, set

/// Nonce: OIDC Nonce definition: https://openid.net/specs/openid-connect-core-1_0.html#IDToken
/// State: OAuth Spec: https://tools.ietf.org/html/rfc6749#section-10.12
type [<AllowNullLiteral>] ServerRequestParameters =
    abstract authorityInstance: Authority with get, set
    abstract clientId: string with get, set
    abstract scopes: ResizeArray<string> with get, set
    abstract nonce: string with get, set
    abstract state: string with get, set
    abstract xClientVer: string with get, set
    abstract xClientSku: string with get, set
    abstract correlationId: string with get, set
    abstract responseType: string with get, set
    abstract redirectUri: string with get, set
    abstract promptValue: string with get, set
    abstract claimsValue: string with get, set
    abstract queryParameters: string with get, set
    abstract extraQueryParameters: string with get, set
    // obj
    /// <summary>generates the URL with QueryString Parameters</summary>
    /// <param name="scopes"></param>
    abstract createNavigateUrl: scopes: ResizeArray<string> -> string
    /// <summary>Generate the array of all QueryStringParams to be sent to the server</summary>
    /// <param name="scopes"></param>
    abstract createNavigationUrlString: scopes: ResizeArray<string> -> ResizeArray<string>
    /// <summary>append the required scopes: https://openid.net/specs/openid-connect-basic-1_0.html#Scopes</summary>
    /// <param name="scopes"></param>
    abstract translateclientIdUsedInScope: scopes: ResizeArray<string> -> unit
    /// <summary>Parse the scopes into a formatted scopeList</summary>
    /// <param name="scopes"></param>
    abstract parseScope: scopes: ResizeArray<string> -> string

/// Nonce: OIDC Nonce definition: https://openid.net/specs/openid-connect-core-1_0.html#IDToken
/// State: OAuth Spec: https://tools.ietf.org/html/rfc6749#section-10.12
type [<AllowNullLiteral>] ServerRequestParametersStatic =
    /// <summary>Constructor</summary>
    /// <param name="authority"></param>
    /// <param name="clientId"></param>
    /// <param name="scope"></param>
    /// <param name="responseType"></param>
    /// <param name="redirectUri"></param>
    /// <param name="state"></param>
    [<Emit "new $0($1...)">] abstract Create: authority: Authority * clientId: string * scope: ResizeArray<string> * responseType: string * redirectUri: string * state: string -> ServerRequestParameters

type [<AllowNullLiteral>] Storage =
    abstract setItem: key: string * value: string * ?enableCookieStorage: bool -> unit
    abstract getItem: key: string * ?enableCookieStorage: bool -> string
    abstract removeItem: key: string -> unit
    abstract clear: unit -> unit
    abstract getAllAccessTokens: clientId: string * homeAccountIdentifier: string -> ResizeArray<AccessTokenCacheItem>
    abstract removeAcquireTokenEntries: unit -> unit
    abstract resetCacheItems: unit -> unit
    abstract setItemCookie: cName: string * cValue: string * ?expires: float -> unit
    abstract getItemCookie: cName: string -> string
    abstract getCookieExpirationTime: cookieLifeDays: float -> string
    abstract clearCookie: unit -> unit

type [<AllowNullLiteral>] StorageStatic =
    [<Emit "new $0($1...)">] abstract Create: cacheLocation: CacheLocation -> Storage
    /// <summary>Create acquireTokenAccountKey to cache account object</summary>
    /// <param name="accountId"></param>
    /// <param name="state"></param>
    abstract generateAcquireTokenAccountKey: accountId: obj option * state: string -> string
    /// <summary>Create authorityKey to cache authority</summary>
    /// <param name="state"></param>
    abstract generateAuthorityKey: state: string -> string

type [<AllowNullLiteral>] Telemetry =
    abstract RegisterReceiver: receiverCallback: (ResizeArray<Object> -> unit) -> unit

type [<AllowNullLiteral>] TelemetryStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> Telemetry
    abstract GetInstance: unit -> Telemetry

type [<AllowNullLiteral>] Window =
    abstract msal: Object with get, set
    // abstract CustomEvent: CustomEvent with get, set
    // abstract Event: Event with get, set
    abstract activeRenewals: TypeLiteral_01 with get, set
    abstract renewStates: ResizeArray<string> with get, set
    abstract callbackMappedToRenewStates: TypeLiteral_01 with get, set
    abstract promiseMappedToRenewStates: TypeLiteral_01 with get, set
    abstract openedWindows: ResizeArray<Window> with get, set
    abstract requestType: string with get, set

type [<AllowNullLiteral>] CacheResult =
    abstract errorDesc: string with get, set
    abstract token: string with get, set
    abstract error: string with get, set

type [<AllowNullLiteral>] ResponseStateInfo =
    abstract state: string with get, set
    abstract stateMatch: bool with get, set
    abstract requestType: string with get, set

type authResponseCallback =
    // [<Emit "$0($1...)">] abstract Invoke: authErr: AuthError * ?response: AuthResponse -> unit
    AuthError -> AuthResponse option -> unit

type [<AllowNullLiteral>] tokenReceivedCallback =
    [<Emit "$0($1...)">] abstract Invoke: response: AuthResponse -> unit

type [<AllowNullLiteral>] errorReceivedCallback =
    [<Emit "$0($1...)">] abstract Invoke: authErr: AuthError * accountState: string -> unit

/// UserAgentApplication class : {@link UserAgentApplication}
/// Object Instance that the developer can use to make loginXX OR acquireTokenXX functions
type [<AllowNullLiteral>] UserAgentApplication =
    abstract cacheStorage: Storage with get, set
    abstract authorityInstance: Authority with get, set
    // obj
    // obj
    /// returns the authority instance
    abstract getAuthorityInstance: unit -> Authority
    /// Sets the callback functions for the redirect flow to send back the success or error object.
    abstract handleRedirectCallback: tokenReceivedCallback: tokenReceivedCallback * errorReceivedCallback: errorReceivedCallback -> unit
    abstract handleRedirectCallback: authCallback: authResponseCallback -> unit
    abstract handleRedirectCallback: authOrTokenCallback: U2<authResponseCallback, tokenReceivedCallback> * ?errorReceivedCallback: errorReceivedCallback -> unit
    /// Use when initiating the login process by redirecting the user's browser to the authorization endpoint.
    abstract loginRedirect: ?request: AuthenticationParameters -> unit
    /// Used when you want to obtain an access_token for your API by redirecting the user to the authorization endpoint.
    abstract acquireTokenRedirect: request: AuthenticationParameters -> unit
    /// <param name="hash">- Hash passed from redirect page.</param>
    abstract isCallback: hash: string -> bool
    /// Use when initiating the login process via opening a popup window in the user's browser
    abstract loginPopup: ?request: AuthenticationParameters -> Promise<AuthResponse>
    /// Use when you want to obtain an access_token for your API via opening a popup window in the user's browser
    abstract acquireTokenPopup: request: AuthenticationParameters -> Promise<AuthResponse>
    /// Use this function to obtain a token before every call to the API / resource provider
    ///
    /// MSAL return's a cached token when available
    /// Or it send's a request to the STS to obtain a new token using a hidden iframe.
    abstract acquireTokenSilent: request: AuthenticationParameters -> Promise<AuthResponse>
    abstract isInIframe: unit -> unit
    /// Used to log out the current user, and redirect the user to the postLogoutRedirectUri.
    /// Defaults behaviour is to redirect the user to `window.location.href`.
    abstract logout: unit -> unit
    abstract clearCache: unit -> unit
    /// <param name="accessToken"></param>
    abstract clearCacheForScope: accessToken: string -> unit
    /// <param name="hash">-  Hash passed from redirect page</param>
    abstract getResponseState: hash: string -> ResponseStateInfo
    abstract saveTokenFromHash: hash: string * stateInfo: ResponseStateInfo -> AuthResponse
    /// Returns the signed in account (received from an account object created at the time of login) or null when no state is found
    abstract getAccount: unit -> Account
    abstract getAccountState: state: string -> unit
    /// Used to filter all cached items and return a list of unique accounts based on homeAccountIdentifier.
    abstract getAllAccounts: unit -> ResizeArray<Account>
    /// <param name="scopes"></param>
    /// <param name="state"></param>
    abstract getCachedTokenInternal: scopes: ResizeArray<string> * account: Account * state: string -> AuthResponse
    /// <param name="endpoint"></param>
    abstract getScopesForEndpoint: endpoint: string -> ResizeArray<string>
    /// Return boolean flag to developer to help inform if login is in progress
    abstract getLoginInProgress: unit -> bool
    /// <param name="loginInProgress"></param>
    abstract setloginInProgress: loginInProgress: bool -> unit
    abstract getAcquireTokenInProgress: unit -> bool
    /// <param name="acquireTokenInProgress"></param>
    abstract setAcquireTokenInProgress: acquireTokenInProgress: bool -> unit
    abstract getLogger: unit -> unit
    /// Use to get the redirect uri configured in MSAL or null.
    /// Evaluates redirectUri if its a function, otherwise simply returns its value.
    abstract getRedirectUri: unit -> string
    /// Use to get the post logout redirect uri configured in MSAL or null.
    /// Evaluates postLogoutredirectUri if its a function, otherwise simply returns its value.
    abstract getPostLogoutRedirectUri: unit -> string
    /// Use to get the current {@link Configuration} object in MSAL
    abstract getCurrentConfiguration: unit -> Configuration

/// UserAgentApplication class : {@link UserAgentApplication}
/// Object Instance that the developer can use to make loginXX OR acquireTokenXX functions
type [<AllowNullLiteral>] UserAgentApplicationStatic =
    /// Constructor for the {@link UserAgentApplication} object
    /// This is to be able to instantiate the {@link UserAgentApplication} object
    [<Emit "new $0($1...)">] abstract Create: configuration: Configuration -> UserAgentApplication

type [<AllowNullLiteral>] Utils =
    interface end

type [<AllowNullLiteral>] UtilsStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> Utils
    /// <summary>Utils function to compare two Account objects - used to check if the same user account is logged in</summary>
    /// <param name="a1">: Account object</param>
    /// <param name="a2">: Account object</param>
    abstract compareAccounts: a1: Account * a2: Account -> bool
    /// <summary>Decimal to Hex</summary>
    /// <param name="num"></param>
    abstract decimalToHex: num: float -> string
    /// MSAL JS Library Version
    abstract getLibraryVersion: unit -> string
    /// Creates a new random GUID - used to populate state?
    abstract createNewGuid: unit -> string
    /// <summary>Returns time in seconds for expiration based on string value passed in.</summary>
    /// <param name="expires"></param>
    abstract expiresIn: expires: string -> float
    /// return the current time in Unix time. Date.getTime() returns in milliseconds.
    abstract now: unit -> float
    /// <summary>Check if a string is empty</summary>
    /// <param name="str"></param>
    abstract isEmpty: str: string -> bool
    /// <summary>decode a JWT</summary>
    /// <param name="jwtToken"></param>
    abstract decodeJwt: jwtToken: string -> obj option
    /// <summary>Extract IdToken by decoding the RAWIdToken</summary>
    /// <param name="encodedIdToken"></param>
    abstract extractIdToken: encodedIdToken: string -> obj option
    /// <summary>encoding string to base64 - platform specific check</summary>
    /// <param name="input"></param>
    abstract base64EncodeStringUrlSafe: input: string -> string
    /// <summary>decoding base64 token - platform specific check</summary>
    /// <param name="base64IdToken"></param>
    abstract base64DecodeStringUrlSafe: base64IdToken: string -> string
    /// <summary>base64 encode a string</summary>
    /// <param name="input"></param>
    abstract encode: input: string -> string
    /// <summary>utf8 encode a string</summary>
    /// <param name="input"></param>
    abstract utf8Encode: input: string -> string
    /// <summary>decode a base64 token string</summary>
    /// <param name="base64IdToken"></param>
    abstract decode: base64IdToken: string -> string
    /// <summary>deserialize a string</summary>
    /// <param name="query"></param>
    abstract deserialize: query: string -> obj option
    /// <summary>Check if there are dup scopes in a given request</summary>
    /// <param name="cachedScopes"></param>
    /// <param name="scopes"></param>
    abstract isIntersectingScopes: cachedScopes: ResizeArray<string> * scopes: ResizeArray<string> -> bool
    /// <summary>Check if a given scope is present in the request</summary>
    /// <param name="cachedScopes"></param>
    /// <param name="scopes"></param>
    abstract containsScope: cachedScopes: ResizeArray<string> * scopes: ResizeArray<string> -> bool
    /// <summary>toLower</summary>
    /// <param name="scopes"></param>
    abstract convertToLowerCase: scopes: ResizeArray<string> -> ResizeArray<string>
    /// <summary>remove one element from a scope array</summary>
    /// <param name="scopes"></param>
    /// <param name="scope"></param>
    abstract removeElement: scopes: ResizeArray<string> * scope: string -> ResizeArray<string>
    abstract getDefaultRedirectUri: unit -> string
    /// <summary>Given a url like https://a:b/common/d?e=f#g, and a tenantId, returns https://a:b/tenantId/d</summary>
    /// <param name="tenantId">The tenant id to replace</param>
    abstract replaceTenantPath: url: string * tenantId: string -> string
    abstract constructAuthorityUriFromObject: urlObject: IUri * pathArray: ResizeArray<string> -> unit
    /// Parses out the components from a url string.
    abstract GetUrlComponents: url: string -> IUri
    /// <summary>Given a url or path, append a trailing slash if one doesnt exist</summary>
    /// <param name="url"></param>
    abstract CanonicalizeUri: url: string -> string
    /// <summary>Checks to see if the url ends with the suffix
    /// Required because we are compiling for es5 instead of es6</summary>
    /// <param name="url"></param>
    abstract endsWith: url: string * suffix: string -> bool
    /// <summary>Utils function to remove the login_hint and domain_hint from the i/p extraQueryParameters</summary>
    /// <param name="url"></param>
    /// <param name="name"></param>
    abstract urlRemoveQueryStringParameter: url: string * name: string -> string
    /// <summary>Constructs extraQueryParameters to be sent to the server for the AuthenticationParameters set by the developer
    /// in any login() or acquireToken() calls</summary>
    /// <param name="idTokenObject"></param>
    abstract constructUnifiedCacheQueryParameter: request: AuthenticationParameters * idTokenObject: obj option -> QPDict
    /// Add SID to extraQueryParameters
    abstract addSSOParameter: ssoType: string * ssoData: string * ?ssoParam: QPDict -> QPDict
    /// Utility to generate a QueryParameterString from a Key-Value mapping of extraQueryParameters passed
    abstract generateQueryParametersString: queryParameters: QPDict -> string
    /// <summary>Check to see if there are SSO params set in the Request</summary>
    /// <param name="request"></param>
    abstract isSSOParam: request: AuthenticationParameters -> unit
    abstract setResponseIdToken: originalResponse: AuthResponse * idToken: IdToken -> AuthResponse

/// XHR client for JSON endpoints
/// https://www.npmjs.com/package/async-promise
type [<AllowNullLiteral>] XhrClient =
    abstract sendRequestAsync: url: string * ``method``: string * ?enableCaching: bool -> Promise<obj option>
    abstract handleError: responseText: string -> obj option

/// XHR client for JSON endpoints
/// https://www.npmjs.com/package/async-promise
type [<AllowNullLiteral>] XhrClientStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> XhrClient

type [<AllowNullLiteral>] TypeLiteral_01 =
    interface end