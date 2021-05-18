// ts2fable 0.7.1
module rec MsalBrowser
open System
open Fable.Core
open Fable.Core.JS
open Browser.Types
open MsalCommon

type Array<'T> = System.Collections.Generic.IList<'T>
type Error = System.Exception
type Function = System.Action
type Required<'T> = 'T

module PackageMetadata =

    type [<AllowNullLiteral>] IExports =
        abstract name: obj
        abstract version: obj

[<AutoOpen>]
module __app_ClientApplication =
    type BrowserCacheManager = __cache_BrowserCacheManager.BrowserCacheManager
    type BrowserConfiguration = __config_Configuration.BrowserConfiguration
    type Configuration = __config_Configuration.Configuration
    type InteractionType = __utils_BrowserConstants.InteractionType
    type WrapperSKU = __utils_BrowserConstants.WrapperSKU
    type RedirectRequest = __request_RedirectRequest.RedirectRequest
    type PopupRequest = __request_PopupRequest.PopupRequest
    type AuthorizationUrlRequest = __request_AuthorizationUrlRequest.AuthorizationUrlRequest
    type SsoSilentRequest = __request_SsoSilentRequest.SsoSilentRequest
    type EventError = __event_EventMessage.EventError
    type EventPayload = __event_EventMessage.EventPayload
    type EventCallbackFunction = __event_EventMessage.EventCallbackFunction
    type EventType = __event_EventType.EventType
    type EndSessionRequest = __request_EndSessionRequest.EndSessionRequest
    type EndSessionPopupRequest = __request_EndSessionPopupRequest.EndSessionPopupRequest
    type INavigationClient = __navigation_INavigationClient.INavigationClient

    type [<AllowNullLiteral>] IExports =
        abstract ClientApplication: ClientApplicationStatic

    type [<AllowNullLiteral>] ClientApplication =
        abstract browserCrypto: ICrypto
        abstract browserStorage: BrowserCacheManager
        abstract networkClient: INetworkModule
        abstract navigationClient: INavigationClient with get, set
        abstract config: BrowserConfiguration with get, set
        abstract logger: Logger with get, set
        abstract isBrowserEnvironment: bool with get, set
        /// <summary>Event handler function which allows users to fire events after the PublicClientApplication object
        /// has loaded during redirect flows. This should be invoked on all page loads involved in redirect
        /// auth flows.</summary>
        /// <param name="hash">Hash to process. Defaults to the current value of window.location.hash. Only needs to be provided explicitly if the response to be handled is not contained in the current value.</param>
        abstract handleRedirectPromise: ?hash: string -> Promise<AuthenticationResult option>
        /// Use when you want to obtain an access_token for your API by redirecting the user's browser window to the authorization endpoint. This function redirects
        /// the page, so any code that follows this function will not execute.
        ///
        /// IMPORTANT: It is NOT recommended to have code that is dependent on the resolution of the Promise. This function will navigate away from the current
        /// browser window. It currently returns a Promise in order to reflect the asynchronous nature of the code running in this function.
        abstract acquireTokenRedirect: request: RedirectRequest -> Promise<unit>
        /// Use when you want to obtain an access_token for your API via opening a popup window in the user's browser
        abstract acquireTokenPopup: request: PopupRequest -> Promise<AuthenticationResult>
        /// This function uses a hidden iframe to fetch an authorization code from the eSTS. There are cases where this may not work:
        /// - Any browser using a form of Intelligent Tracking Prevention
        /// - If there is not an established session with the service
        ///
        /// In these cases, the request must be done inside a popup or full frame redirect.
        ///
        /// For the cases where interaction is required, you cannot send a request with prompt=none.
        ///
        /// If your refresh token has expired, you can use this function to fetch a new set of tokens silently as long as
        /// you session on the server still exists.
        abstract ssoSilent: request: SsoSilentRequest -> Promise<AuthenticationResult>
        /// Use this function to obtain a token before every call to the API / resource provider
        ///
        /// MSAL return's a cached token when available
        /// Or it send's a request to the STS to obtain a new token using a refresh token.
        abstract acquireTokenByRefreshToken: request: CommonSilentFlowRequest -> Promise<AuthenticationResult>
        /// Deprecated logout function. Use logoutRedirect or logoutPopup instead
        abstract logout: ?logoutRequest: EndSessionRequest -> Promise<unit>
        /// Use to log out the current user, and redirect the user to the postLogoutRedirectUri.
        /// Default behaviour is to redirect the user to `window.location.href`.
        abstract logoutRedirect: ?logoutRequest: EndSessionRequest -> Promise<unit>
        /// Clears local cache for the current user then opens a popup window prompting the user to sign-out of the server
        abstract logoutPopup: ?logoutRequest: EndSessionPopupRequest -> Promise<unit>
        /// Returns all accounts that MSAL currently has data for.
        /// (the account object is created at the time of successful login)
        /// or empty array when no accounts are found
        abstract getAllAccounts: unit -> ResizeArray<AccountInfo>
        /// Returns the signed in account matching username.
        /// (the account object is created at the time of successful login)
        /// or null when no matching account is found.
        /// This API is provided for convenience but getAccountById should be used for best reliability
        abstract getAccountByUsername: userName: string -> AccountInfo option
        /// Returns the signed in account matching homeAccountId.
        /// (the account object is created at the time of successful login)
        /// or null when no matching account is found
        abstract getAccountByHomeId: homeAccountId: string -> AccountInfo option
        /// Returns the signed in account matching localAccountId.
        /// (the account object is created at the time of successful login)
        /// or null when no matching account is found
        abstract getAccountByLocalId: localAccountId: string -> AccountInfo option
        /// Sets the account to use as the active account. If no account is passed to the acquireToken APIs, then MSAL will use this active account.
        abstract setActiveAccount: account: AccountInfo option -> unit
        /// Gets the currently active account
        abstract getActiveAccount: unit -> AccountInfo option
        /// Use to get the redirect uri configured in MSAL or null.
        abstract getRedirectUri: ?requestRedirectUri: string -> string
        /// Use to get the redirectStartPage either from request or use current window
        abstract getRedirectStartPage: ?requestStartPage: string -> string
        /// Used to get a discovered version of the default authority.
        abstract getDiscoveredAuthority: ?requestAuthority: string -> Promise<Authority>
        /// Helper to check whether interaction is in progress.
        abstract interactionInProgress: unit -> bool
        /// Creates an Authorization Code Client with the given authority, or the default authority.
        abstract createAuthCodeClient: serverTelemetryManager: ServerTelemetryManager * ?authorityUrl: string -> Promise<AuthorizationCodeClient>
        /// Creates an Silent Flow Client with the given authority, or the default authority.
        abstract createSilentFlowClient: serverTelemetryManager: ServerTelemetryManager * ?authorityUrl: string -> Promise<SilentFlowClient>
        /// Creates a Refresh Client with the given authority, or the default authority.
        abstract createRefreshTokenClient: serverTelemetryManager: ServerTelemetryManager * ?authorityUrl: string -> Promise<RefreshTokenClient>
        /// Creates a Client Configuration object with the given request authority, or the default authority.
        abstract getClientConfiguration: serverTelemetryManager: ServerTelemetryManager * ?requestAuthority: string -> Promise<ClientConfiguration>
        /// Helper to validate app environment before making a request.
        abstract preflightInteractiveRequest: request: U2<RedirectRequest, PopupRequest> * interactionType: InteractionType -> AuthorizationUrlRequest
        /// Helper to validate app environment before making an auth request
        /// * @param interactionType
        abstract preflightBrowserEnvironmentCheck: interactionType: InteractionType -> unit
        /// Initializer function for all request APIs
        abstract initializeBaseRequest: request: obj -> BaseAuthRequest
        abstract initializeServerTelemetryManager: apiId: float * correlationId: string * ?forceRefresh: bool -> ServerTelemetryManager
        /// Helper to initialize required request parameters for interactive APIs and ssoSilent()
        abstract initializeAuthorizationRequest: request: U3<RedirectRequest, PopupRequest, SsoSilentRequest> * interactionType: InteractionType -> AuthorizationUrlRequest
        /// Generates an auth code request tied to the url request.
        abstract initializeAuthorizationCodeRequest: request: AuthorizationUrlRequest -> Promise<CommonAuthorizationCodeRequest>
        /// Initializer for the logout request.
        abstract initializeLogoutRequest: ?logoutRequest: EndSessionRequest -> CommonEndSessionRequest
        /// Emits events by calling callback with event message
        abstract emitEvent: eventType: EventType * ?interactionType: InteractionType * ?payload: EventPayload * ?error: EventError -> unit
        /// Adds event callbacks to array
        abstract addEventCallback: callback: EventCallbackFunction -> string option
        /// Removes callback with provided id from callback array
        abstract removeEventCallback: callbackId: string -> unit
        /// Returns the logger instance
        abstract getLogger: unit -> Logger
        /// <summary>Replaces the default logger set in configurations with new Logger with new configurations</summary>
        /// <param name="logger">Logger instance</param>
        abstract setLogger: logger: Logger -> unit
        /// Called by wrapper libraries (Angular & React) to set SKU and Version passed down to telemetry, logger, etc.
        abstract initializeWrapperLibrary: sku: WrapperSKU * version: string -> unit
        /// Sets navigation client
        abstract setNavigationClient: navigationClient: INavigationClient -> unit

    type [<AllowNullLiteral>] ClientApplicationStatic =
        /// <param name="configuration">Object for the MSAL PublicClientApplication instance</param>
        [<Emit "new $0($1...)">] abstract Create: configuration: Configuration -> ClientApplication

[<AutoOpen>]
module __app_IPublicClientApplication =
    type RedirectRequest = __request_RedirectRequest.RedirectRequest
    type PopupRequest = __request_PopupRequest.PopupRequest
    type SilentRequest = __request_SilentRequest.SilentRequest
    type SsoSilentRequest = __request_SsoSilentRequest.SsoSilentRequest
    type EndSessionRequest = __request_EndSessionRequest.EndSessionRequest
    type WrapperSKU = __utils_BrowserConstants.WrapperSKU
    type INavigationClient = __navigation_INavigationClient.INavigationClient

    type [<AllowNullLiteral>] IExports =
        abstract stubbedPublicClientApplication: IPublicClientApplication

    type [<AllowNullLiteral>] IPublicClientApplication =
        abstract acquireTokenPopup: request: PopupRequest -> Promise<AuthenticationResult>
        abstract acquireTokenRedirect: request: RedirectRequest -> Promise<unit>
        abstract acquireTokenSilent: silentRequest: SilentRequest -> Promise<AuthenticationResult>
        abstract addEventCallback: callback: Function -> string option
        abstract removeEventCallback: callbackId: string -> unit
        abstract getAccountByHomeId: homeAccountId: string -> AccountInfo option
        abstract getAccountByLocalId: localId: string -> AccountInfo option
        abstract getAccountByUsername: userName: string -> AccountInfo option
        abstract getAllAccounts: unit -> ResizeArray<AccountInfo>
        abstract handleRedirectPromise: ?hash: string -> Promise<AuthenticationResult option>
        abstract loginPopup: ?request: PopupRequest -> Promise<AuthenticationResult>
        abstract loginRedirect: ?request: RedirectRequest -> Promise<unit>
        abstract logout: ?logoutRequest: EndSessionRequest -> Promise<unit>
        abstract logoutRedirect: ?logoutRequest: EndSessionRequest -> Promise<unit>
        abstract logoutPopup: ?logoutRequest: EndSessionRequest -> Promise<unit>
        abstract ssoSilent: request: SsoSilentRequest -> Promise<AuthenticationResult>
        abstract getLogger: unit -> Logger
        abstract setLogger: logger: Logger -> unit
        abstract setActiveAccount: account: AccountInfo option -> unit
        abstract getActiveAccount: unit -> AccountInfo option
        abstract initializeWrapperLibrary: sku: WrapperSKU * version: string -> unit
        abstract setNavigationClient: navigationClient: INavigationClient -> unit

[<AutoOpen>]
module __app_PublicClientApplication =
    type Configuration = __config_Configuration.Configuration
    type IPublicClientApplication = __app_IPublicClientApplication.IPublicClientApplication
    type RedirectRequest = __request_RedirectRequest.RedirectRequest
    type PopupRequest = __request_PopupRequest.PopupRequest
    type ClientApplication = __app_ClientApplication.ClientApplication
    type SilentRequest = __request_SilentRequest.SilentRequest

    type [<AllowNullLiteral>] IExports =
        abstract PublicClientApplication: PublicClientApplicationStatic

    /// The PublicClientApplication class is the object exposed by the library to perform authentication and authorization functions in Single Page Applications
    /// to obtain JWT tokens as described in the OAuth 2.0 Authorization Code Flow with PKCE specification.
    type [<AllowNullLiteral>] PublicClientApplication =
        inherit ClientApplication
        inherit IPublicClientApplication
        /// Use when initiating the login process by redirecting the user's browser to the authorization endpoint. This function redirects the page, so
        /// any code that follows this function will not execute.
        ///
        /// IMPORTANT: It is NOT recommended to have code that is dependent on the resolution of the Promise. This function will navigate away from the current
        /// browser window. It currently returns a Promise in order to reflect the asynchronous nature of the code running in this function.
        abstract loginRedirect: ?request: RedirectRequest -> Promise<unit>
        /// Use when initiating the login process via opening a popup window in the user's browser
        abstract loginPopup: ?request: PopupRequest -> Promise<AuthenticationResult>
        /// Silently acquire an access token for a given set of scopes. Will use cached token if available, otherwise will attempt to acquire a new token from the network via refresh token.
        abstract acquireTokenSilent: request: SilentRequest -> Promise<AuthenticationResult>

    /// The PublicClientApplication class is the object exposed by the library to perform authentication and authorization functions in Single Page Applications
    /// to obtain JWT tokens as described in the OAuth 2.0 Authorization Code Flow with PKCE specification.
    type [<AllowNullLiteral>] PublicClientApplicationStatic =
        /// <param name="configuration">object for the MSAL PublicClientApplication instance</param>
        [<Emit "new $0($1...)">] abstract Create: configuration: Configuration -> PublicClientApplication

[<AutoOpen>]
module __cache_BrowserCacheManager =
    type CacheOptions = __config_Configuration.CacheOptions
    type InteractionType = __utils_BrowserConstants.InteractionType

    type [<AllowNullLiteral>] IExports =
        abstract BrowserCacheManager: BrowserCacheManagerStatic
        abstract DEFAULT_BROWSER_CACHE_MANAGER: (string -> Logger -> BrowserCacheManager)

    /// This class implements the cache storage interface for MSAL through browser local or session storage.
    /// Cookies are only used if storeAuthStateInCookie is true, and are only used for
    /// parameters such as state and nonce, generally.
    type [<AllowNullLiteral>] BrowserCacheManager =
        inherit CacheManager
        /// fetches the entry from the browser storage based off the key
        abstract getItem: key: string -> string option
        /// sets the entry in the browser storage
        abstract setItem: key: string * value: string -> unit
        /// fetch the account entity from the platform cache
        abstract getAccount: accountKey: string -> AccountEntity option
        /// set account entity in the platform cache
        abstract setAccount: account: AccountEntity -> unit
        /// generates idToken entity from a string
        abstract getIdTokenCredential: idTokenKey: string -> IdTokenEntity option
        /// set IdToken credential to the platform cache
        abstract setIdTokenCredential: idToken: IdTokenEntity -> unit
        /// generates accessToken entity from a string
        abstract getAccessTokenCredential: accessTokenKey: string -> AccessTokenEntity option
        /// set accessToken credential to the platform cache
        abstract setAccessTokenCredential: accessToken: AccessTokenEntity -> unit
        /// generates refreshToken entity from a string
        abstract getRefreshTokenCredential: refreshTokenKey: string -> RefreshTokenEntity option
        /// set refreshToken credential to the platform cache
        abstract setRefreshTokenCredential: refreshToken: RefreshTokenEntity -> unit
        /// fetch appMetadata entity from the platform cache
        abstract getAppMetadata: appMetadataKey: string -> AppMetadataEntity option
        /// set appMetadata entity to the platform cache
        abstract setAppMetadata: appMetadata: AppMetadataEntity -> unit
        /// fetch server telemetry entity from the platform cache
        abstract getServerTelemetry: serverTelemetryKey: string -> ServerTelemetryEntity option
        /// set server telemetry entity to the platform cache
        abstract setServerTelemetry: serverTelemetryKey: string * serverTelemetry: ServerTelemetryEntity -> unit
        abstract getAuthorityMetadata: key: string -> AuthorityMetadataEntity option
        abstract getAuthorityMetadataKeys: unit -> Array<string>
        abstract setAuthorityMetadata: key: string * entity: AuthorityMetadataEntity -> unit
        /// fetch throttling entity from the platform cache
        abstract getThrottlingCache: throttlingCacheKey: string -> ThrottlingEntity option
        /// set throttling entity to the platform cache
        abstract setThrottlingCache: throttlingCacheKey: string * throttlingCache: ThrottlingEntity -> unit
        /// Gets cache item with given key.
        /// Will retrieve frm cookies if storeAuthStateInCookie is set to true.
        abstract getTemporaryCache: cacheKey: string * ?generateKey: bool -> string option
        /// Sets the cache item with the key and value given.
        /// Stores in cookie if storeAuthStateInCookie is set to true.
        /// This can cause cookie overflow if used incorrectly.
        abstract setTemporaryCache: cacheKey: string * value: string * ?generateKey: bool -> unit
        /// Removes the cache item with the given key.
        /// Will also clear the cookie item if storeAuthStateInCookie is set to true.
        abstract removeItem: key: string -> bool
        /// Checks whether key is in cache.
        abstract containsKey: key: string -> bool
        /// Gets all keys in window.
        abstract getKeys: unit -> ResizeArray<string>
        /// Clears all cache entries created by MSAL (except tokens).
        abstract clear: unit -> unit
        /// Add value to cookies
        abstract setItemCookie: cookieName: string * cookieValue: string * ?expires: float -> unit
        /// Get one item by key from cookies
        abstract getItemCookie: cookieName: string -> string
        /// Clear all msal-related cookies currently set in the browser. Should only be used to clear temporary cache items.
        abstract clearMsalCookies: unit -> unit
        /// Clear an item in the cookies by key
        abstract clearItemCookie: cookieName: string -> unit
        /// Get cookie expiration time
        abstract getCookieExpirationTime: cookieLifeDays: float -> string
        /// Gets the cache object referenced by the browser
        abstract getCache: unit -> obj
        /// interface compat, we cannot overwrite browser cache; Functionality is supported by individual entities in browser
        abstract setCache: unit -> unit
        /// Prepend msal.<client-id> to each key; Skip for any JSON object as Key (defined schemas do not need the key appended: AccessToken Keys or the upcoming schema)
        abstract generateCacheKey: key: string -> string
        /// Create authorityKey to cache authority
        abstract generateAuthorityKey: stateString: string -> string
        /// Create Nonce key to cache nonce
        abstract generateNonceKey: stateString: string -> string
        /// <summary>Creates full cache key for the request state</summary>
        /// <param name="stateString">State string for the request</param>
        abstract generateStateKey: stateString: string -> string
        /// Gets the cached authority based on the cached state. Returns empty if no cached state found.
        abstract getCachedAuthority: cachedState: string -> string option
        /// Updates account, authority, and state in cache
        abstract updateCacheEntries: state: string * nonce: string * authorityInstance: string -> unit
        /// Reset all temporary cache items
        abstract resetRequestCache: state: string -> unit
        /// Removes temporary cache for the provided state
        abstract cleanRequestByState: stateString: string -> unit
        /// Looks in temporary cache for any state values with the provided interactionType and removes all temporary cache items for that state
        /// Used in scenarios where temp cache needs to be cleaned but state is not known, such as clicking browser back button.
        abstract cleanRequestByInteractionType: interactionType: InteractionType -> unit
        abstract cacheCodeRequest: authCodeRequest: CommonAuthorizationCodeRequest * browserCrypto: ICrypto -> unit
        /// Gets the token exchange parameters from the cache. Throws an error if nothing is found.
        abstract getCachedRequest: state: string * browserCrypto: ICrypto -> CommonAuthorizationCodeRequest

    /// This class implements the cache storage interface for MSAL through browser local or session storage.
    /// Cookies are only used if storeAuthStateInCookie is true, and are only used for
    /// parameters such as state and nonce, generally.
    type [<AllowNullLiteral>] BrowserCacheManagerStatic =
        [<Emit "new $0($1...)">] abstract Create: clientId: string * cacheConfig: Required<CacheOptions> * cryptoImpl: ICrypto * logger: Logger -> BrowserCacheManager

[<AutoOpen>]
module __cache_BrowserStorage =
    type IWindowStorage = __cache_IWindowStorage.IWindowStorage

    type [<AllowNullLiteral>] IExports =
        abstract BrowserStorage: BrowserStorageStatic

    type [<AllowNullLiteral>] BrowserStorage =
        inherit IWindowStorage
        abstract getItem: key: string -> string option
        abstract setItem: key: string * value: string -> unit
        abstract removeItem: key: string -> unit
        abstract getKeys: unit -> ResizeArray<string>
        abstract containsKey: key: string -> bool

    type [<AllowNullLiteral>] BrowserStorageStatic =
        [<Emit "new $0($1...)">] abstract Create: cacheLocation: string -> BrowserStorage

[<AutoOpen>]
module __cache_DatabaseStorage =

    type [<AllowNullLiteral>] IExports =
        abstract DatabaseStorage: DatabaseStorageStatic

    /// Storage wrapper for IndexedDB storage in browsers: https://developer.mozilla.org/en-US/docs/Web/API/IndexedDB_API
    type [<AllowNullLiteral>] DatabaseStorage<'T> =
        /// Opens IndexedDB instance.
        abstract ``open``: unit -> Promise<unit>
        /// Retrieves item from IndexedDB instance.
        abstract get: key: string -> Promise<'T>
        /// Adds item to IndexedDB under given key
        abstract put: key: string * payload: 'T -> Promise<'T>

    /// Storage wrapper for IndexedDB storage in browsers: https://developer.mozilla.org/en-US/docs/Web/API/IndexedDB_API
    type [<AllowNullLiteral>] DatabaseStorageStatic =
        [<Emit "new $0($1...)">] abstract Create: dbName: string * tableName: string * version: float -> DatabaseStorage<'T>

[<AutoOpen>]
module __cache_IWindowStorage =

    type [<AllowNullLiteral>] IWindowStorage =
        /// Get the item from the window storage object matching the given key.
        abstract getItem: key: string -> string option
        /// Sets the item in the window storage object with the given key.
        abstract setItem: key: string * value: string -> unit
        /// Removes the item in the window storage object matching the given key.
        abstract removeItem: key: string -> unit
        /// Get all the keys from the window storage object as an iterable array of strings.
        abstract getKeys: unit -> ResizeArray<string>
        /// Returns true or false if the given key is present in the cache.
        abstract containsKey: key: string -> bool

[<AutoOpen>]
module __cache_MemoryStorage =
    type IWindowStorage = __cache_IWindowStorage.IWindowStorage

    type [<AllowNullLiteral>] IExports =
        abstract MemoryStorage: MemoryStorageStatic

    type [<AllowNullLiteral>] MemoryStorage =
        inherit IWindowStorage
        abstract getItem: key: string -> string option
        abstract setItem: key: string * value: string -> unit
        abstract removeItem: key: string -> unit
        abstract getKeys: unit -> ResizeArray<string>
        abstract containsKey: key: string -> bool
        abstract clear: unit -> unit

    type [<AllowNullLiteral>] MemoryStorageStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> MemoryStorage

[<AutoOpen>]
module __config_Configuration =
    type BrowserCacheLocation = __utils_BrowserConstants.BrowserCacheLocation
    type INavigationClient = __navigation_INavigationClient.INavigationClient

    type [<AllowNullLiteral>] IExports =
        abstract DEFAULT_POPUP_TIMEOUT_MS: obj
        abstract DEFAULT_IFRAME_TIMEOUT_MS: obj
        abstract DEFAULT_REDIRECT_TIMEOUT_MS: obj
        /// MSAL function that sets the default options when not explicitly configured from app developer
        abstract buildConfiguration: p0: Configuration * isBrowserEnvironment: bool -> BrowserConfiguration

    type [<AllowNullLiteral>] BrowserAuthOptions =
        abstract clientId: string with get, set
        abstract authority: string option with get, set
        abstract knownAuthorities: Array<string> option with get, set
        abstract cloudDiscoveryMetadata: string option with get, set
        abstract authorityMetadata: string option with get, set
        abstract redirectUri: string option with get, set
        abstract postLogoutRedirectUri: string option with get, set
        abstract navigateToLoginRequestUrl: bool option with get, set
        abstract clientCapabilities: Array<string> option with get, set
        abstract protocolMode: ProtocolMode option with get, set

    type [<AllowNullLiteral>] CacheOptions =
        abstract cacheLocation: U2<BrowserCacheLocation, string> option with get, set
        abstract storeAuthStateInCookie: bool option with get, set
        abstract secureCookies: bool option with get, set

    type [<AllowNullLiteral>] BrowserSystemOptions =
        interface end

    type [<AllowNullLiteral>] Configuration =
        abstract auth: BrowserAuthOptions with get, set
        abstract cache: CacheOptions option with get, set
        abstract system: BrowserSystemOptions option with get, set

    type [<AllowNullLiteral>] BrowserConfiguration =
        abstract auth: Required<BrowserAuthOptions> with get, set
        abstract cache: Required<CacheOptions> with get, set
        abstract system: Required<BrowserSystemOptions> with get, set

[<AutoOpen>]
module __crypto_BrowserCrypto =

    type [<AllowNullLiteral>] IExports =
        abstract BrowserCrypto: BrowserCryptoStatic

    /// This class implements functions used by the browser library to perform cryptography operations such as
    /// hashing and encoding. It also has helper functions to validate the availability of specific APIs.
    type [<AllowNullLiteral>] BrowserCrypto =
        /// Returns a sha-256 hash of the given dataString as an ArrayBuffer.
        abstract sha256Digest: dataString: string -> Promise<ArrayBuffer>
        /// Populates buffer with cryptographically random values.
        abstract getRandomValues: dataBuffer: Uint8Array -> unit
        /// Generates a keypair based on current keygen algorithm config.
        // abstract generateKeyPair: extractable: bool * usages: Array<KeyUsage> -> Promise<CryptoKeyPair>
        /// Export key as Json Web Key (JWK)
        // abstract exportJwk: key: CryptoKey -> Promise<JsonWebKey>
        /// Imports key as Json Web Key (JWK), can set extractable and usages.
        // abstract importJwk: key: JsonWebKey * extractable: bool * usages: Array<KeyUsage> -> Promise<CryptoKey>
        /// Signs given data with given key
        // abstract sign: key: CryptoKey * data: ArrayBuffer -> Promise<ArrayBuffer>

    /// This class implements functions used by the browser library to perform cryptography operations such as
    /// hashing and encoding. It also has helper functions to validate the availability of specific APIs.
    type [<AllowNullLiteral>] BrowserCryptoStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> BrowserCrypto
        /// Returns stringified jwk.
        // abstract getJwkString: jwk: JsonWebKey -> string

[<AutoOpen>]
module __crypto_CryptoOps =
    type [<AllowNullLiteral>] IExports =
        abstract CryptoOps: CryptoOpsStatic

    type [<AllowNullLiteral>] CachedKeyPair =
        // abstract publicKey: CryptoKey with get, set
        // abstract privateKey: CryptoKey with get, set
        abstract requestMethod: string option with get, set
        abstract requestUri: string option with get, set

    /// This class implements MSAL's crypto interface, which allows it to perform base64 encoding and decoding, generating cryptographically random GUIDs and
    /// implementing Proof Key for Code Exchange specs for the OAuth Authorization Code Flow using PKCE (rfc here: https://tools.ietf.org/html/rfc7636).
    type [<AllowNullLiteral>] CryptoOps =
        inherit ICrypto
        /// Creates a new random GUID - used to populate state and nonce.
        abstract createNewGuid: unit -> string
        /// Encodes input string to base64.
        abstract base64Encode: input: string -> string
        /// Decodes input string from base64.
        abstract base64Decode: input: string -> string
        /// Generates PKCE codes used in Authorization Code Flow.
        abstract generatePkceCodes: unit -> Promise<PkceCodes>
        /// Generates a keypair, stores it and returns a thumbprint
        abstract getPublicKeyThumbprint: request: BaseAuthRequest -> Promise<string>
        /// Signs the given object as a jwt payload with private key retrieved by given kid.
        abstract signJwt: payload: SignedHttpRequest * kid: string -> Promise<string>

    /// This class implements MSAL's crypto interface, which allows it to perform base64 encoding and decoding, generating cryptographically random GUIDs and
    /// implementing Proof Key for Code Exchange specs for the OAuth Authorization Code Flow using PKCE (rfc here: https://tools.ietf.org/html/rfc7636).
    type [<AllowNullLiteral>] CryptoOpsStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> CryptoOps

[<AutoOpen>]
module __crypto_GuidGenerator =
    type BrowserCrypto = __crypto_BrowserCrypto.BrowserCrypto

    type [<AllowNullLiteral>] IExports =
        abstract GuidGenerator: GuidGeneratorStatic

    type [<AllowNullLiteral>] GuidGenerator =
        abstract generateGuid: unit -> string

    type [<AllowNullLiteral>] GuidGeneratorStatic =
        [<Emit "new $0($1...)">] abstract Create: cryptoObj: BrowserCrypto -> GuidGenerator
        /// verifies if a string is  GUID
        abstract isGuid: guid: string -> bool

[<AutoOpen>]
module __crypto_PkceGenerator =
    type BrowserCrypto = __crypto_BrowserCrypto.BrowserCrypto

    type [<AllowNullLiteral>] IExports =
        abstract PkceGenerator: PkceGeneratorStatic

    /// Class which exposes APIs to generate PKCE codes and code verifiers.
    type [<AllowNullLiteral>] PkceGenerator =
        /// Generates PKCE Codes. See the RFC for more information: https://tools.ietf.org/html/rfc7636
        abstract generateCodes: unit -> Promise<PkceCodes>

    /// Class which exposes APIs to generate PKCE codes and code verifiers.
    type [<AllowNullLiteral>] PkceGeneratorStatic =
        [<Emit "new $0($1...)">] abstract Create: cryptoObj: BrowserCrypto -> PkceGenerator

[<AutoOpen>]
module __encode_Base64Decode =

    type [<AllowNullLiteral>] IExports =
        abstract Base64Decode: Base64DecodeStatic

    /// Class which exposes APIs to decode base64 strings to plaintext. See here for implementation details:
    /// https://developer.mozilla.org/en-US/docs/Web/API/WindowBase64/Base64_encoding_and_decoding#Solution_2_%E2%80%93_JavaScript's_UTF-16_%3E_UTF-8_%3E_base64
    type [<AllowNullLiteral>] Base64Decode =
        /// Returns a URL-safe plaintext decoded string from b64 encoded input.
        abstract decode: input: string -> string

    /// Class which exposes APIs to decode base64 strings to plaintext. See here for implementation details:
    /// https://developer.mozilla.org/en-US/docs/Web/API/WindowBase64/Base64_encoding_and_decoding#Solution_2_%E2%80%93_JavaScript's_UTF-16_%3E_UTF-8_%3E_base64
    type [<AllowNullLiteral>] Base64DecodeStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> Base64Decode

[<AutoOpen>]
module __encode_Base64Encode =

    type [<AllowNullLiteral>] IExports =
        abstract Base64Encode: Base64EncodeStatic

    /// Class which exposes APIs to encode plaintext to base64 encoded string. See here for implementation details:
    /// https://developer.mozilla.org/en-US/docs/Web/API/WindowBase64/Base64_encoding_and_decoding#Solution_2_%E2%80%93_JavaScript's_UTF-16_%3E_UTF-8_%3E_base64
    type [<AllowNullLiteral>] Base64Encode =
        /// Returns URL Safe b64 encoded string from a plaintext string.
        abstract urlEncode: input: string -> string
        /// Returns URL Safe b64 encoded string from an int8Array.
        abstract urlEncodeArr: inputArr: Uint8Array -> string
        /// Returns b64 encoded string from plaintext string.
        abstract encode: input: string -> string

    /// Class which exposes APIs to encode plaintext to base64 encoded string. See here for implementation details:
    /// https://developer.mozilla.org/en-US/docs/Web/API/WindowBase64/Base64_encoding_and_decoding#Solution_2_%E2%80%93_JavaScript's_UTF-16_%3E_UTF-8_%3E_base64
    type [<AllowNullLiteral>] Base64EncodeStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> Base64Encode

[<AutoOpen>]
module __error_BrowserAuthError =
    type [<AllowNullLiteral>] IExports =
        abstract BrowserAuthErrorMessage: IExportsBrowserAuthErrorMessage
        abstract BrowserAuthError: BrowserAuthErrorStatic

    /// Browser library error class thrown by the MSAL.js library for SPAs
    [<AbstractClass>]
    type [<AllowNullLiteral>] BrowserAuthError =
        inherit AuthError

    /// Browser library error class thrown by the MSAL.js library for SPAs
    type [<AllowNullLiteral>] BrowserAuthErrorStatic =
        [<Emit "new $0($1...)">] abstract Create: errorCode: string * ?errorMessage: string -> BrowserAuthError
        /// Creates an error thrown when PKCE is not implemented.
        abstract createPkceNotGeneratedError: errDetail: string -> BrowserAuthError
        /// Creates an error thrown when the crypto object is unavailable.
        abstract createCryptoNotAvailableError: errDetail: string -> BrowserAuthError
        /// Creates an error thrown when an HTTP method hasn't been implemented by the browser class.
        abstract createHttpMethodNotImplementedError: ``method``: string -> BrowserAuthError
        /// Creates an error thrown when the navigation URI is empty.
        abstract createEmptyNavigationUriError: unit -> BrowserAuthError
        /// Creates an error thrown when the hash string value is unexpectedly empty.
        abstract createEmptyHashError: hashValue: string -> BrowserAuthError
        /// Creates an error thrown when the hash string value is unexpectedly empty.
        abstract createHashDoesNotContainStateError: unit -> BrowserAuthError
        /// Creates an error thrown when the hash string value does not contain known properties
        abstract createHashDoesNotContainKnownPropertiesError: unit -> BrowserAuthError
        /// Creates an error thrown when the hash string value is unexpectedly empty.
        abstract createUnableToParseStateError: unit -> BrowserAuthError
        /// Creates an error thrown when the state value in the hash does not match the interaction type of the API attempting to consume it.
        abstract createStateInteractionTypeMismatchError: unit -> BrowserAuthError
        /// Creates an error thrown when a browser interaction (redirect or popup) is in progress.
        abstract createInteractionInProgressError: unit -> BrowserAuthError
        /// Creates an error thrown when the popup window could not be opened.
        abstract createPopupWindowError: ?errDetail: string -> BrowserAuthError
        /// Creates an error thrown when window.open returns an empty window object.
        abstract createEmptyWindowCreatedError: unit -> BrowserAuthError
        /// Creates an error thrown when the user closes a popup.
        abstract createUserCancelledError: unit -> BrowserAuthError
        /// Creates an error thrown when monitorPopupFromHash times out for a given popup.
        abstract createMonitorPopupTimeoutError: unit -> BrowserAuthError
        /// Creates an error thrown when monitorIframeFromHash times out for a given iframe.
        abstract createMonitorIframeTimeoutError: unit -> BrowserAuthError
        /// Creates an error thrown when navigateWindow is called inside an iframe.
        abstract createRedirectInIframeError: windowParentCheck: bool -> BrowserAuthError
        /// Creates an error thrown when an auth reload is done inside an iframe.
        abstract createBlockReloadInHiddenIframeError: unit -> BrowserAuthError
        /// Creates an error thrown when a popup attempts to call an acquireToken API
        abstract createBlockAcquireTokenInPopupsError: unit -> BrowserAuthError
        /// Creates an error thrown when an iframe is found to be closed before the timeout is reached.
        abstract createIframeClosedPrematurelyError: unit -> BrowserAuthError
        /// Creates an error thrown when the login_hint, sid or account object is not provided in the ssoSilent API.
        abstract createSilentSSOInsufficientInfoError: unit -> BrowserAuthError
        /// Creates an error thrown when the account object is not provided in the acquireTokenSilent API.
        abstract createNoAccountError: unit -> BrowserAuthError
        /// Creates an error thrown when a given prompt value is invalid for silent requests.
        abstract createSilentPromptValueError: givenPrompt: string -> BrowserAuthError
        /// Creates an error thrown when the cached token request could not be retrieved from the cache
        abstract createUnableToParseTokenRequestCacheError: unit -> BrowserAuthError
        /// Creates an error thrown when the token request could not be retrieved from the cache
        abstract createNoTokenRequestCacheError: unit -> BrowserAuthError
        /// Creates an error thrown when handleCodeResponse is called before initiateAuthRequest (InteractionHandler)
        abstract createAuthRequestNotSetError: unit -> BrowserAuthError
        /// Creates an error thrown when the authority could not be retrieved from the cache
        abstract createNoCachedAuthorityError: unit -> BrowserAuthError
        /// Creates an error thrown if cache type is invalid.
        abstract createInvalidCacheTypeError: unit -> BrowserAuthError
        /// Create an error thrown when login and token requests are made from a non-browser environment
        abstract createNonBrowserEnvironmentError: unit -> BrowserAuthError
        /// Create an error thrown when indexDB database is not open
        abstract createDatabaseNotOpenError: unit -> BrowserAuthError
        /// Create an error thrown when token fetch fails due to no internet
        abstract createNoNetworkConnectivityError: unit -> BrowserAuthError
        /// Create an error thrown when token fetch fails due to reasons other than internet connectivity
        abstract createPostRequestFailedError: errorDesc: string * endpoint: string -> BrowserAuthError
        /// Create an error thrown when get request fails due to reasons other than internet connectivity
        abstract createGetRequestFailedError: errorDesc: string * endpoint: string -> BrowserAuthError
        /// Create an error thrown when network client fails to parse network response
        abstract createFailedToParseNetworkResponseError: endpoint: string -> BrowserAuthError

    type [<AllowNullLiteral>] IExportsBrowserAuthErrorMessagePkceNotGenerated =
        abstract code: string with get, set
        abstract desc: string with get, set

    type [<AllowNullLiteral>] IExportsBrowserAuthErrorMessage =
        abstract pkceNotGenerated: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract cryptoDoesNotExist: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract httpMethodNotImplementedError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract emptyNavigateUriError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract hashEmptyError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract hashDoesNotContainStateError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract hashDoesNotContainKnownPropertiesError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract unableToParseStateError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract stateInteractionTypeMismatchError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract interactionInProgress: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract popUpWindowError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract emptyWindowError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract userCancelledError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract monitorPopupTimeoutError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract monitorIframeTimeoutError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract redirectInIframeError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract blockTokenRequestsInHiddenIframeError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract blockAcquireTokenInPopupsError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract iframeClosedPrematurelyError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract silentSSOInsufficientInfoError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract noAccountError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract silentPromptValueError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract noTokenRequestCacheError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract unableToParseTokenRequestCacheError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract noCachedAuthorityError: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract authRequestNotSet: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract invalidCacheType: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract notInBrowserEnvironment: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract databaseNotOpen: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract noNetworkConnectivity: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract postRequestFailed: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract getRequestFailed: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set
        abstract failedToParseNetworkResponse: IExportsBrowserAuthErrorMessagePkceNotGenerated with get, set

[<AutoOpen>]
module __error_BrowserConfigurationAuthError =

    type [<AllowNullLiteral>] IExports =
        abstract BrowserConfigurationAuthErrorMessage: IExportsBrowserConfigurationAuthErrorMessage
        abstract BrowserConfigurationAuthError: BrowserConfigurationAuthErrorStatic

    /// Browser library error class thrown by the MSAL.js library for SPAs
    [<AbstractClass>]
    type [<AllowNullLiteral>] BrowserConfigurationAuthError =
        inherit AuthError

    /// Browser library error class thrown by the MSAL.js library for SPAs
    type [<AllowNullLiteral>] BrowserConfigurationAuthErrorStatic =
        [<Emit "new $0($1...)">] abstract Create: errorCode: string * ?errorMessage: string -> BrowserConfigurationAuthError
        /// Creates an error thrown when the redirect uri is empty (not set by caller)
        abstract createRedirectUriEmptyError: unit -> BrowserConfigurationAuthError
        /// Creates an error thrown when the post-logout redirect uri is empty (not set by caller)
        abstract createPostLogoutRedirectUriEmptyError: unit -> BrowserConfigurationAuthError
        /// Creates error thrown when given storage location is not supported.
        abstract createStorageNotSupportedError: givenStorageLocation: string -> BrowserConfigurationAuthError
        /// Creates error thrown when callback object is invalid.
        abstract createInvalidCallbackObjectError: callbackObject: obj -> BrowserConfigurationAuthError
        /// Creates error thrown when redirect callbacks are not set before calling loginRedirect() or acquireTokenRedirect().
        abstract createRedirectCallbacksNotSetError: unit -> BrowserConfigurationAuthError
        /// Creates error thrown when the stub instance of PublicClientApplication is called.
        abstract createStubPcaInstanceCalledError: unit -> BrowserConfigurationAuthError
        abstract createInMemoryRedirectUnavailableError: unit -> BrowserConfigurationAuthError

    type [<AllowNullLiteral>] IExportsBrowserConfigurationAuthErrorMessageRedirectUriNotSet =
        abstract code: string with get, set
        abstract desc: string with get, set

    type [<AllowNullLiteral>] IExportsBrowserConfigurationAuthErrorMessage =
        abstract redirectUriNotSet: IExportsBrowserConfigurationAuthErrorMessageRedirectUriNotSet with get, set
        abstract postLogoutUriNotSet: IExportsBrowserConfigurationAuthErrorMessageRedirectUriNotSet with get, set
        abstract storageNotSupportedError: IExportsBrowserConfigurationAuthErrorMessageRedirectUriNotSet with get, set
        abstract noRedirectCallbacksSet: IExportsBrowserConfigurationAuthErrorMessageRedirectUriNotSet with get, set
        abstract invalidCallbackObject: IExportsBrowserConfigurationAuthErrorMessageRedirectUriNotSet with get, set
        abstract stubPcaInstanceCalled: IExportsBrowserConfigurationAuthErrorMessageRedirectUriNotSet with get, set
        abstract inMemRedirectUnavailable: IExportsBrowserConfigurationAuthErrorMessageRedirectUriNotSet with get, set

[<AutoOpen>]
module __event_EventMessage =
    type EventType = __event_EventType.EventType
    type InteractionStatus = __utils_BrowserConstants.InteractionStatus
    type InteractionType = __utils_BrowserConstants.InteractionType
    type PopupRequest = PopupRequest
    type RedirectRequest = RedirectRequest
    type SilentRequest = SilentRequest
    type SsoSilentRequest = SsoSilentRequest
    type EndSessionRequest = EndSessionRequest

    type [<AllowNullLiteral>] IExports =
        abstract EventMessageUtils: EventMessageUtilsStatic

    type [<AllowNullLiteral>] EventMessage =
        abstract eventType: EventType with get, set
        abstract interactionType: InteractionType option with get, set
        abstract payload: EventPayload with get, set
        abstract error: EventError with get, set
        abstract timestamp: float with get, set

    type [<AllowNullLiteral>] PopupEvent =
        abstract popupWindow: Window with get, set

    type EventPayload =
        U7<PopupRequest, RedirectRequest, SilentRequest, SsoSilentRequest, EndSessionRequest, AuthenticationResult, PopupEvent> option

    type EventError =
        U2<AuthError, Error> option

    type [<AllowNullLiteral>] EventCallbackFunction =
        [<Emit "$0($1...)">] abstract Invoke: message: EventMessage -> unit

    type [<AllowNullLiteral>] EventMessageUtils =
        interface end

    type [<AllowNullLiteral>] EventMessageUtilsStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> EventMessageUtils
        /// Gets interaction status from event message
        abstract getInteractionStatusFromEvent: message: EventMessage -> InteractionStatus option

[<AutoOpen>]
module __event_EventType =

    type [<StringEnum>] [<RequireQualifiedAccess>] EventType =
        | [<CompiledName "msal:loginStart">] LOGIN_START
        | [<CompiledName "msal:loginSuccess">] LOGIN_SUCCESS
        | [<CompiledName "msal:loginFailure">] LOGIN_FAILURE
        | [<CompiledName "msal:acquireTokenStart">] ACQUIRE_TOKEN_START
        | [<CompiledName "msal:acquireTokenSuccess">] ACQUIRE_TOKEN_SUCCESS
        | [<CompiledName "msal:acquireTokenFailure">] ACQUIRE_TOKEN_FAILURE
        | [<CompiledName "msal:acquireTokenFromNetworkStart">] ACQUIRE_TOKEN_NETWORK_START
        | [<CompiledName "msal:ssoSilentStart">] SSO_SILENT_START
        | [<CompiledName "msal:ssoSilentSuccess">] SSO_SILENT_SUCCESS
        | [<CompiledName "msal:ssoSilentFailure">] SSO_SILENT_FAILURE
        | [<CompiledName "msal:handleRedirectStart">] HANDLE_REDIRECT_START
        | [<CompiledName "msal:handleRedirectEnd">] HANDLE_REDIRECT_END
        | [<CompiledName "msal:popupOpened">] POPUP_OPENED
        | [<CompiledName "msal:logoutStart">] LOGOUT_START
        | [<CompiledName "msal:logoutSuccess">] LOGOUT_SUCCESS
        | [<CompiledName "msal:logoutFailure">] LOGOUT_FAILURE
        | [<CompiledName "msal:logoutEnd">] LOGOUT_END

[<AutoOpen>]
module __interaction_handler_InteractionHandler =
    type BrowserCacheManager = __cache_BrowserCacheManager.BrowserCacheManager

    type [<AllowNullLiteral>] IExports =
        abstract InteractionHandler: InteractionHandlerStatic

    type [<AllowNullLiteral>] InteractionParams =
        interface end

    /// Abstract class which defines operations for a browser interaction handling class.
    type [<AllowNullLiteral>] InteractionHandler =
        abstract authModule: AuthorizationCodeClient with get, set
        abstract browserStorage: BrowserCacheManager with get, set
        abstract authCodeRequest: CommonAuthorizationCodeRequest with get, set
        /// Function to enable user interaction.
        abstract initiateAuthRequest: requestUrl: string * ``params``: InteractionParams -> U3<Window, Promise<HTMLIFrameElement>, Promise<unit>>
        /// Function to handle response parameters from hash.
        abstract handleCodeResponse: locationHash: string * state: string * authority: Authority * networkModule: INetworkModule -> Promise<AuthenticationResult>
        abstract updateTokenEndpointAuthority: cloudInstanceHostname: string * authority: Authority * networkModule: INetworkModule -> Promise<unit>

    /// Abstract class which defines operations for a browser interaction handling class.
    type [<AllowNullLiteral>] InteractionHandlerStatic =
        [<Emit "new $0($1...)">] abstract Create: authCodeModule: AuthorizationCodeClient * storageImpl: BrowserCacheManager * authCodeRequest: CommonAuthorizationCodeRequest -> InteractionHandler

[<AutoOpen>]
module __interaction_handler_PopupHandler =
    type InteractionHandler = __interaction_handler_InteractionHandler.InteractionHandler
    type InteractionParams = __interaction_handler_InteractionHandler.InteractionParams
    type BrowserCacheManager = __cache_BrowserCacheManager.BrowserCacheManager

    type [<AllowNullLiteral>] IExports =
        abstract PopupHandler: PopupHandlerStatic

    type [<AllowNullLiteral>] PopupParams =
        interface end

    /// This class implements the interaction handler base class for browsers. It is written specifically for handling
    /// popup window scenarios. It includes functions for monitoring the popup window for a hash.
    type [<AllowNullLiteral>] PopupHandler =
        inherit InteractionHandler
        /// Opens a popup window with given request Url.
        abstract initiateAuthRequest: requestUrl: string * ``params``: PopupParams -> Window
        /// <summary>Monitors a window until it loads a url with a known hash, or hits a specified timeout.</summary>
        /// <param name="popupWindow">- window that is being monitored</param>
        abstract monitorPopupForHash: popupWindow: Window -> Promise<string>

    /// This class implements the interaction handler base class for browsers. It is written specifically for handling
    /// popup window scenarios. It includes functions for monitoring the popup window for a hash.
    type [<AllowNullLiteral>] PopupHandlerStatic =
        [<Emit "new $0($1...)">] abstract Create: authCodeModule: AuthorizationCodeClient * storageImpl: BrowserCacheManager * authCodeRequest: CommonAuthorizationCodeRequest -> PopupHandler

[<AutoOpen>]
module __interaction_handler_RedirectHandler =
    type BrowserCacheManager = __cache_BrowserCacheManager.BrowserCacheManager
    type InteractionHandler = __interaction_handler_InteractionHandler.InteractionHandler
    type InteractionParams = __interaction_handler_InteractionHandler.InteractionParams
    type INavigationClient = __navigation_INavigationClient.INavigationClient

    type [<AllowNullLiteral>] IExports =
        abstract RedirectHandler: RedirectHandlerStatic

    type [<AllowNullLiteral>] RedirectParams =
        interface end

    type [<AllowNullLiteral>] RedirectHandler =
        inherit InteractionHandler
        /// Redirects window to given URL.
        abstract initiateAuthRequest: requestUrl: string * ``params``: RedirectParams -> Promise<unit>
        /// Handle authorization code response in the window.
        abstract handleCodeResponse: locationHash: string * state: string * authority: Authority * networkModule: INetworkModule * ?clientId: string -> Promise<AuthenticationResult>

    type [<AllowNullLiteral>] RedirectHandlerStatic =
        [<Emit "new $0($1...)">] abstract Create: authCodeModule: AuthorizationCodeClient * storageImpl: BrowserCacheManager * authCodeRequest: CommonAuthorizationCodeRequest * browserCrypto: ICrypto -> RedirectHandler

[<AutoOpen>]
module __interaction_handler_SilentHandler =
    type InteractionHandler = __interaction_handler_InteractionHandler.InteractionHandler
    type BrowserCacheManager = __cache_BrowserCacheManager.BrowserCacheManager

    type [<AllowNullLiteral>] IExports =
        abstract SilentHandler: SilentHandlerStatic

    type [<AllowNullLiteral>] SilentHandler =
        inherit InteractionHandler
        /// Creates a hidden iframe to given URL using user-requested scopes as an id.
        abstract initiateAuthRequest: requestUrl: string -> Promise<HTMLIFrameElement>
        /// Monitors an iframe content window until it loads a url with a known hash, or hits a specified timeout.
        abstract monitorIframeForHash: iframe: HTMLIFrameElement * timeout: float -> Promise<string>

    type [<AllowNullLiteral>] SilentHandlerStatic =
        [<Emit "new $0($1...)">] abstract Create: authCodeModule: AuthorizationCodeClient * storageImpl: BrowserCacheManager * authCodeRequest: CommonAuthorizationCodeRequest * navigateFrameWait: float -> SilentHandler

[<AutoOpen>]
module __navigation_INavigationClient =
    type NavigationOptions = __navigation_NavigationOptions.NavigationOptions

    type [<AllowNullLiteral>] INavigationClient =
        /// Navigates to other pages within the same web application
        /// Return false if this doesn't cause the page to reload i.e. Client-side navigation
        abstract navigateInternal: url: string * options: NavigationOptions -> Promise<bool>
        /// Navigates to other pages outside the web application i.e. the Identity Provider
        abstract navigateExternal: url: string * options: NavigationOptions -> Promise<bool>

[<AutoOpen>]
module __navigation_NavigationClient =
    type INavigationClient = __navigation_INavigationClient.INavigationClient
    type NavigationOptions = __navigation_NavigationOptions.NavigationOptions

    type [<AllowNullLiteral>] IExports =
        abstract NavigationClient: NavigationClientStatic

    type [<AllowNullLiteral>] NavigationClient =
        inherit INavigationClient
        /// Navigates to other pages within the same web application
        abstract navigateInternal: url: string * options: NavigationOptions -> Promise<bool>
        /// Navigates to other pages outside the web application i.e. the Identity Provider
        abstract navigateExternal: url: string * options: NavigationOptions -> Promise<bool>

    type [<AllowNullLiteral>] NavigationClientStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> NavigationClient

[<AutoOpen>]
module __navigation_NavigationOptions =
    type ApiId = __utils_BrowserConstants.ApiId

    type [<AllowNullLiteral>] NavigationOptions =
        /// The Id of the API that initiated navigation
        abstract apiId: ApiId with get, set
        /// Suggested timeout (ms) based on the configuration provided to PublicClientApplication
        abstract timeout: float with get, set
        /// When set to true the url should not be added to the browser history
        abstract noHistory: bool with get, set

[<AutoOpen>]
module __network_FetchClient =
    type [<AllowNullLiteral>] IExports =
        abstract FetchClient: FetchClientStatic

    /// This class implements the Fetch API for GET and POST requests. See more here: https://developer.mozilla.org/en-US/docs/Web/API/Fetch_API
    type [<AllowNullLiteral>] FetchClient =
        inherit INetworkModule
        /// Fetch Client for REST endpoints - Get request
        abstract sendGetRequestAsync: url: string * ?options: NetworkRequestOptions -> Promise<NetworkResponse<'T>>
        /// Fetch Client for REST endpoints - Post request
        abstract sendPostRequestAsync: url: string * ?options: NetworkRequestOptions -> Promise<NetworkResponse<'T>>

    /// This class implements the Fetch API for GET and POST requests. See more here: https://developer.mozilla.org/en-US/docs/Web/API/Fetch_API
    type [<AllowNullLiteral>] FetchClientStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> FetchClient

[<AutoOpen>]
module __network_XhrClient =
    type [<AllowNullLiteral>] IExports =
        abstract XhrClient: XhrClientStatic

    /// This client implements the XMLHttpRequest class to send GET and POST requests.
    type [<AllowNullLiteral>] XhrClient =
        inherit INetworkModule
        /// XhrClient for REST endpoints - Get request
        abstract sendGetRequestAsync: url: string * ?options: NetworkRequestOptions -> Promise<NetworkResponse<'T>>
        /// XhrClient for REST endpoints - Post request
        abstract sendPostRequestAsync: url: string * ?options: NetworkRequestOptions -> Promise<NetworkResponse<'T>>

    /// This client implements the XMLHttpRequest class to send GET and POST requests.
    type [<AllowNullLiteral>] XhrClientStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> XhrClient

[<AutoOpen>]
module __request_AuthorizationUrlRequest =
    type [<AllowNullLiteral>] AuthorizationUrlRequest =
        interface end

[<AutoOpen>]
module __request_EndSessionPopupRequest =
    type [<AllowNullLiteral>] EndSessionPopupRequest =
        interface end

[<AutoOpen>]
module __request_EndSessionRequest =
    type [<AllowNullLiteral>] EndSessionRequest =
        interface end

[<AutoOpen>]
module __request_PopupRequest =
    type [<AllowNullLiteral>] PopupRequest =
        interface end

[<AutoOpen>]
module __request_RedirectRequest =
    type [<AllowNullLiteral>] RedirectRequest =
        interface end

[<AutoOpen>]
module __request_SilentRequest =
    type [<AllowNullLiteral>] SilentRequest =
        interface end

[<AutoOpen>]
module __request_SsoSilentRequest =
    type SsoSilentRequest =
        obj

[<AutoOpen>]
module __utils_BrowserConstants =
    type PopupRequest = __request_PopupRequest.PopupRequest
    type RedirectRequest = __request_RedirectRequest.RedirectRequest

    type [<AllowNullLiteral>] IExports =
        abstract BrowserConstants: IExportsBrowserConstants
        abstract DEFAULT_REQUEST: U2<RedirectRequest, PopupRequest>
        abstract KEY_FORMAT_JWK: obj

    type [<StringEnum>] [<RequireQualifiedAccess>] BrowserCacheLocation =
        | LocalStorage
        | SessionStorage
        | MemoryStorage

    type [<StringEnum>] [<RequireQualifiedAccess>] HTTP_REQUEST_TYPE =
        | [<CompiledName "GET">] GET
        | [<CompiledName "POST">] POST

    type [<StringEnum>] [<RequireQualifiedAccess>] TemporaryCacheKeys =
        | [<CompiledName "authority">] AUTHORITY
        | [<CompiledName "acquireToken.account">] ACQUIRE_TOKEN_ACCOUNT
        | [<CompiledName "session.state">] SESSION_STATE
        | [<CompiledName "request.state">] REQUEST_STATE
        | [<CompiledName "nonce.id_token">] NONCE_IDTOKEN
        | [<CompiledName "request.origin">] ORIGIN_URI
        | [<CompiledName "token.renew.status">] RENEW_STATUS
        | [<CompiledName "urlHash">] URL_HASH
        | [<CompiledName "request.params">] REQUEST_PARAMS
        | [<CompiledName "scopes">] SCOPES
        | [<CompiledName "interaction.status">] INTERACTION_STATUS_KEY

    type [<RequireQualifiedAccess>] ApiId =
        | AcquireTokenRedirect = 861
        | AcquireTokenPopup = 862
        | SsoSilent = 863
        | AcquireTokenSilent_authCode = 864
        | HandleRedirectPromise = 865
        | AcquireTokenSilent_silentFlow = 61
        | Logout = 961
        | LogoutPopup = 962

    type [<StringEnum>] [<RequireQualifiedAccess>] InteractionType =
        | Redirect
        | Popup
        | Silent

    type [<StringEnum>] [<RequireQualifiedAccess>] InteractionStatus =
        | Startup
        | Login
        | Logout
        | AcquireToken
        | SsoSilent
        | HandleRedirect
        | None

    type [<StringEnum>] [<RequireQualifiedAccess>] WrapperSKU =
        | [<CompiledName "@azure/msal-react">] React
        | [<CompiledName "@azure/msal-angular">] Angular

    type [<AllowNullLiteral>] IExportsBrowserConstants =
        /// Interaction in progress cache value
        abstract INTERACTION_IN_PROGRESS_VALUE: string with get, set
        /// Invalid grant error code
        abstract INVALID_GRANT_ERROR: string with get, set
        /// Default popup window width
        abstract POPUP_WIDTH: float with get, set
        /// Default popup window height
        abstract POPUP_HEIGHT: float with get, set
        /// Name of the popup window starts with
        abstract POPUP_NAME_PREFIX: string with get, set
        /// Default popup monitor poll interval in milliseconds
        abstract POLL_INTERVAL_MS: float with get, set
        /// Msal-browser SKU
        abstract MSAL_SKU: string with get, set

[<AutoOpen>]
module __utils_BrowserProtocolUtils =
    type InteractionType = __utils_BrowserConstants.InteractionType

    type [<AllowNullLiteral>] IExports =
        abstract BrowserProtocolUtils: BrowserProtocolUtilsStatic

    type [<AllowNullLiteral>] BrowserStateObject =
        abstract interactionType: InteractionType with get, set

    type [<AllowNullLiteral>] BrowserProtocolUtils =
        interface end

    type [<AllowNullLiteral>] BrowserProtocolUtilsStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> BrowserProtocolUtils
        /// Extracts the BrowserStateObject from the state string.
        abstract extractBrowserRequestState: browserCrypto: ICrypto * state: string -> BrowserStateObject option
        /// <summary>Parses properties of server response from url hash</summary>
        /// <param name="locationHash">Hash from url</param>
        abstract parseServerResponseFromHash: locationHash: string -> ServerAuthorizationCodeResponse

[<AutoOpen>]
module __utils_BrowserStringUtils =

    type [<AllowNullLiteral>] IExports =
        abstract BrowserStringUtils: BrowserStringUtilsStatic

    /// Utility functions for strings in a browser. See here for implementation details:
    /// https://developer.mozilla.org/en-US/docs/Web/API/WindowBase64/Base64_encoding_and_decoding#Solution_2_%E2%80%93_JavaScript's_UTF-16_%3E_UTF-8_%3E_base64
    type [<AllowNullLiteral>] BrowserStringUtils =
        interface end

    /// Utility functions for strings in a browser. See here for implementation details:
    /// https://developer.mozilla.org/en-US/docs/Web/API/WindowBase64/Base64_encoding_and_decoding#Solution_2_%E2%80%93_JavaScript's_UTF-16_%3E_UTF-8_%3E_base64
    type [<AllowNullLiteral>] BrowserStringUtilsStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> BrowserStringUtils
        /// Converts string to Uint8Array
        abstract stringToUtf8Arr: sDOMStr: string -> Uint8Array
        /// Converst string to ArrayBuffer
        abstract stringToArrayBuffer: dataString: string -> ArrayBuffer
        /// Converts Uint8Array to a string
        abstract utf8ArrToString: aBytes: Uint8Array -> string

[<AutoOpen>]
module __utils_BrowserUtils =
    type InteractionType = __utils_BrowserConstants.InteractionType

    type [<AllowNullLiteral>] IExports =
        abstract BrowserUtils: BrowserUtilsStatic

    /// Utility class for browser specific functions
    type [<AllowNullLiteral>] BrowserUtils =
        interface end

    /// Utility class for browser specific functions
    type [<AllowNullLiteral>] BrowserUtilsStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> BrowserUtils
        /// Clears hash from window url.
        abstract clearHash: unit -> unit
        /// Replaces current hash with hash from provided url
        abstract replaceHash: url: string -> unit
        /// Returns boolean of whether the current window is in an iframe or not.
        abstract isInIframe: unit -> bool
        /// Returns current window URL as redirect uri
        abstract getCurrentUri: unit -> string
        /// Gets the homepage url for the current window location.
        abstract getHomepage: unit -> string
        /// Returns best compatible network client object.
        abstract getBrowserNetworkClient: unit -> INetworkModule
        /// Throws error if we have completed an auth and are
        /// attempting another auth request inside an iframe.
        abstract blockReloadInHiddenIframes: unit -> unit
        /// <summary>Block redirect operations in iframes unless explicitly allowed</summary>
        /// <param name="interactionType">Interaction type for the request</param>
        /// <param name="allowRedirectInIframe">Config value to allow redirects when app is inside an iframe</param>
        abstract blockRedirectInIframe: interactionType: InteractionType * allowRedirectInIframe: bool -> unit
        /// Block redirectUri loaded in popup from calling AcquireToken APIs
        abstract blockAcquireTokenInPopups: unit -> unit
        /// <summary>Throws error if token requests are made in non-browser environment</summary>
        /// <param name="isBrowserEnvironment">Flag indicating if environment is a browser.</param>
        abstract blockNonBrowserEnvironment: isBrowserEnvironment: bool -> unit
        /// Returns boolean of whether current browser is an Internet Explorer or Edge browser.
        abstract detectIEOrEdge: unit -> bool

[<AutoOpen>]
module __utils_MathUtils =

    type [<AllowNullLiteral>] IExports =
        abstract MathUtils: MathUtilsStatic

    /// Utility class for math specific functions in browser.
    type [<AllowNullLiteral>] MathUtils =
        interface end

    /// Utility class for math specific functions in browser.
    type [<AllowNullLiteral>] MathUtilsStatic =
        [<Emit "new $0($1...)">] abstract Create: unit -> MathUtils
        /// Decimal to Hex
        abstract decimalToHex: num: float -> string

[<AutoOpen>]
module __utils_PopupUtils =
    type BrowserCacheManager = __cache_BrowserCacheManager.BrowserCacheManager
    type AuthorizationUrlRequest = __request_AuthorizationUrlRequest.AuthorizationUrlRequest

    type [<AllowNullLiteral>] IExports =
        abstract PopupUtils: PopupUtilsStatic

    type [<AllowNullLiteral>] PopupUtils =
        abstract openPopup: urlNavigate: string * popupName: string * ?popup: Window -> Window
        /// Event callback to unload main window.
        abstract unloadWindow: e: Event -> unit
        /// Closes popup, removes any state vars created during popup calls.
        abstract cleanPopup: ?popupWindow: Window -> unit
        /// <summary>Monitors a window until it loads a url with the same origin.</summary>
        /// <param name="popupWindow">- window that is being monitored</param>
        abstract monitorPopupForSameOrigin: popupWindow: Window -> Promise<unit>

    type [<AllowNullLiteral>] PopupUtilsStatic =
        [<Emit "new $0($1...)">] abstract Create: storageImpl: BrowserCacheManager * logger: Logger -> PopupUtils
        abstract openSizedPopup: urlNavigate: string * popupName: string -> Window option
        /// Generates the name for the popup based on the client id and request
        abstract generatePopupName: clientId: string * request: AuthorizationUrlRequest -> string
        /// Generates the name for the popup based on the client id and request for logouts
        abstract generateLogoutPopupName: clientId: string * request: CommonEndSessionRequest -> string
