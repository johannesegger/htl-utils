// ts2fable 0.6.1
module rec Msal

open System
open Fable.Core
open Fable.Import.JS

[<Import("UserAgentApplication","msal")>]
let UserAgentApplication: UserAgentApplicationStatic =
    jsNative

/// Copyright (c) Microsoft Corporation
///   All Rights Reserved
///   MIT License
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy of this
/// software and associated documentation files (the 'Software'), to deal in the Software
/// without restriction, including without limitation the rights to use, copy, modify,
/// merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
/// permit persons to whom the Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
/// OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
/// OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
type [<AllowNullLiteral>] IUri =
    abstract Protocol: string with get, set
    abstract HostNameAndPort: string with get, set
    abstract AbsolutePath: string with get, set
    abstract Search: string with get, set
    abstract Hash: string with get, set
    abstract PathSegments: ResizeArray<string> with get, set

type [<AllowNullLiteral>] IExports =
    abstract Authority: AuthorityStatic
    abstract Logger: LoggerStatic
    abstract AccessTokenValue: AccessTokenValueStatic
    abstract AccessTokenKey: AccessTokenKeyStatic
    abstract AccessTokenCacheItem: AccessTokenCacheItemStatic
    abstract Storage: StorageStatic
    abstract TokenResponse: TokenResponseStatic
    abstract ClientInfo: ClientInfoStatic
    abstract IdToken: IdTokenStatic
    abstract User: UserStatic
    abstract UserAgentApplication: UserAgentApplicationStatic
    abstract Constants: ConstantsStatic
    abstract ErrorCodes: ErrorCodesStatic
    abstract ErrorDescription: ErrorDescriptionStatic

type [<RequireQualifiedAccess>] AuthorityType =
    | Aad = 0
    | Adfs = 1
    | B2C = 2

type [<AllowNullLiteral>] Authority =
    abstract AuthorityType: AuthorityType
    abstract IsValidationEnabled: bool with get, set
    abstract Tenant: string
    abstract tenantDiscoveryResponse: obj with get, set
    abstract AuthorizationEndpoint: string
    abstract EndSessionEndpoint: string
    abstract SelfSignedJwtAudience: string
    abstract validateResolved: obj with get, set
    abstract CanonicalAuthority: string with get, set
    abstract canonicalAuthority: obj with get, set
    abstract canonicalAuthorityUrlComponents: obj with get, set
    abstract CanonicalAuthorityUrlComponents: IUri
    abstract DefaultOpenIdConfigurationEndpoint: string
    abstract validateAsUri: obj with get, set
    abstract DiscoverEndpoints: obj with get, set
    abstract ResolveEndpointsAsync: unit -> Promise<Authority>
    abstract GetOpenIdConfigurationEndpointAsync: unit -> Promise<string>

type [<AllowNullLiteral>] AuthorityStatic =
    [<Emit "new $0($1...)">] abstract Create: authority: string * validateAuthority: bool -> Authority

/// Copyright (c) Microsoft Corporation
///   All Rights Reserved
///   MIT License
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy of this
/// software and associated documentation files (the 'Software'), to deal in the Software
/// without restriction, including without limitation the rights to use, copy, modify,
/// merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
/// permit persons to whom the Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
/// OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
/// OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
type [<AllowNullLiteral>] ILoggerCallback =
    [<Emit "$0($1...)">] abstract Invoke: level: LogLevel * message: string * containsPii: bool -> unit

type [<RequireQualifiedAccess>] LogLevel =
    | Error = 0
    | Warning = 1
    | Info = 2
    | Verbose = 3

type [<AllowNullLiteral>] Logger =
    abstract _instance: obj with get, set
    abstract _correlationId: obj with get, set
    abstract _level: obj with get, set
    abstract _piiLoggingEnabled: obj with get, set
    abstract _localCallback: obj with get, set
    abstract logMessage: obj with get, set
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
    [<Emit "new $0($1...)">] abstract Create: localCallback: ILoggerCallback * ?options: LoggerStaticOptions -> Logger

type [<AllowNullLiteral>] LoggerStaticOptions =
    abstract correlationId: string option with get, set
    abstract level: LogLevel option with get, set
    abstract piiLoggingEnabled: bool option with get, set

type [<AllowNullLiteral>] AccessTokenKey =
    abstract authority: string with get, set
    abstract clientId: string with get, set
    abstract userIdentifier: string with get, set
    abstract scopes: string with get, set

type [<AllowNullLiteral>] AccessTokenKeyStatic =
    [<Emit "new $0($1...)">] abstract Create: authority: string * clientId: string * scopes: string * uid: string * utid: string -> AccessTokenKey

/// Copyright (c) Microsoft Corporation
///   All Rights Reserved
///   MIT License
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy of this
/// software and associated documentation files (the 'Software'), to deal in the Software
/// without restriction, including without limitation the rights to use, copy, modify,
/// merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
/// permit persons to whom the Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
/// OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
/// OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
type [<AllowNullLiteral>] AccessTokenValue =
    abstract accessToken: string with get, set
    abstract idToken: string with get, set
    abstract expiresIn: string with get, set
    abstract clientInfo: string with get, set

/// Copyright (c) Microsoft Corporation
///   All Rights Reserved
///   MIT License
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy of this
/// software and associated documentation files (the 'Software'), to deal in the Software
/// without restriction, including without limitation the rights to use, copy, modify,
/// merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
/// permit persons to whom the Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
/// OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
/// OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
type [<AllowNullLiteral>] AccessTokenValueStatic =
    [<Emit "new $0($1...)">] abstract Create: accessToken: string * idToken: string * expiresIn: string * clientInfo: string -> AccessTokenValue

type [<AllowNullLiteral>] AccessTokenCacheItem =
    abstract key: AccessTokenKey with get, set
    abstract value: AccessTokenValue with get, set

type [<AllowNullLiteral>] AccessTokenCacheItemStatic =
    [<Emit "new $0($1...)">] abstract Create: key: AccessTokenKey * value: AccessTokenValue -> AccessTokenCacheItem

type [<AllowNullLiteral>] Storage =
    abstract _instance: obj with get, set
    abstract _localStorageSupported: obj with get, set
    abstract _sessionStorageSupported: obj with get, set
    abstract _cacheLocation: obj with get, set
    abstract setItem: key: string * value: string * ?enableCookieStorage: bool -> unit
    abstract getItem: key: string * ?enableCookieStorage: bool -> string
    abstract removeItem: key: string -> unit
    abstract clear: unit -> unit
    abstract getAllAccessTokens: clientId: string * userIdentifier: string -> Array<AccessTokenCacheItem>
    abstract removeAcquireTokenEntries: authorityKey: string * acquireTokenUserKey: string -> unit
    abstract resetCacheItems: unit -> unit
    abstract setItemCookie: cName: string * cValue: string * ?expires: float -> unit
    abstract getItemCookie: cName: string -> string
    abstract setExpirationCookie: cookieLife: float -> string
    abstract clearCookie: unit -> unit

type [<AllowNullLiteral>] StorageStatic =
    [<Emit "new $0($1...)">] abstract Create: cacheLocation: string -> Storage

/// Copyright (c) Microsoft Corporation
///   All Rights Reserved
///   MIT License
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy of this
/// software and associated documentation files (the 'Software'), to deal in the Software
/// without restriction, including without limitation the rights to use, copy, modify,
/// merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
/// permit persons to whom the Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
/// OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
/// OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
type [<AllowNullLiteral>] TokenResponse =
    abstract valid: bool with get, set
    abstract parameters: Object with get, set
    abstract stateMatch: bool with get, set
    abstract stateResponse: string with get, set
    abstract requestType: string with get, set

/// Copyright (c) Microsoft Corporation
///   All Rights Reserved
///   MIT License
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy of this
/// software and associated documentation files (the 'Software'), to deal in the Software
/// without restriction, including without limitation the rights to use, copy, modify,
/// merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
/// permit persons to whom the Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
/// OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
/// OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
type [<AllowNullLiteral>] TokenResponseStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> TokenResponse

/// Copyright (c) Microsoft Corporation
///   All Rights Reserved
///   MIT License
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy of this
/// software and associated documentation files (the 'Software'), to deal in the Software
/// without restriction, including without limitation the rights to use, copy, modify,
/// merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
/// permit persons to whom the Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
/// OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
/// OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
type [<AllowNullLiteral>] ClientInfo =
    abstract _uid: obj with get, set
    abstract uid: string with get, set
    abstract _utid: obj with get, set
    abstract utid: string with get, set

/// Copyright (c) Microsoft Corporation
///   All Rights Reserved
///   MIT License
/// 
/// Permission is hereby granted, free of charge, to any person obtaining a copy of this
/// software and associated documentation files (the 'Software'), to deal in the Software
/// without restriction, including without limitation the rights to use, copy, modify,
/// merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
/// permit persons to whom the Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS
/// OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
/// OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
type [<AllowNullLiteral>] ClientInfoStatic =
    [<Emit "new $0($1...)">] abstract Create: rawClientInfo: string -> ClientInfo

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

type [<AllowNullLiteral>] User =
    abstract displayableId: string with get, set
    abstract name: string with get, set
    abstract identityProvider: string with get, set
    abstract userIdentifier: string with get, set
    abstract idToken: Object with get, set
    abstract sid: string with get, set

type [<AllowNullLiteral>] UserStatic =
    [<Emit "new $0($1...)">] abstract Create: displayableId: string * name: string * identityProvider: string * userIdentifier: string * idToken: Object * sid: string -> User
    abstract createUser: idToken: IdToken * clientInfo: ClientInfo * authority: string -> User

type [<AllowNullLiteral>] Window =
    abstract msal: Object with get, set
    // abstract CustomEvent: CustomEvent with get, set
    // abstract Event: Event with get, set
    abstract activeRenewals: obj with get, set
    abstract renewStates: Array<string> with get, set
    abstract callBackMappedToRenewStates: obj with get, set
    abstract callBacksMappedToRenewStates: obj with get, set
    abstract openedWindows: Array<Window> with get, set
    abstract requestType: string with get, set

type [<AllowNullLiteral>] CacheResult =
    abstract errorDesc: string with get, set
    abstract token: string with get, set
    abstract error: string with get, set

type tokenReceivedCallback =
    // [<Emit "$0($1...)">] abstract Invoke: errorDesc: string * token: string * error: string * tokenType: string * userState: string -> unit
    string -> string -> string -> string -> string -> unit

type [<AllowNullLiteral>] UserAgentApplication =
    abstract _cacheLocations: obj with get, set
    abstract _cacheLocation: obj with get, set
    abstract cacheLocation: string
    abstract _logger: Logger with get, set
    abstract _loginInProgress: obj with get, set
    abstract _acquireTokenInProgress: obj with get, set
    abstract _clockSkew: obj with get, set
    abstract _cacheStorage: Storage with get, set
    abstract _tokenReceivedCallback: obj with get, set
    abstract _user: obj with get, set
    abstract clientId: string with get, set
    abstract authorityInstance: Authority with get, set
    abstract authority: string with get, set
    abstract validateAuthority: bool with get, set
    abstract _redirectUri: obj with get, set
    abstract _state: obj with get, set
    abstract _postLogoutredirectUri: obj with get, set
    abstract loadFrameTimeout: float with get, set
    abstract _navigateToLoginRequestUrl: bool with get, set
    abstract _isAngular: obj with get, set
    abstract _protectedResourceMap: obj with get, set
    abstract _unprotectedResources: obj with get, set
    abstract storeAuthStateInCookie: obj with get, set
    abstract processCallBack: obj with get, set
    abstract loginRedirect: ?scopes: Array<string> * ?extraQueryParameters: string -> unit
    abstract loginPopup: scopes: Array<string> * ?extraQueryParameters: string -> Promise<string>
    abstract promptUser: obj with get, set
    abstract openWindow: obj with get, set
    abstract broadcast: obj with get, set
    abstract logout: unit -> unit
    abstract clearCache: unit -> unit
    abstract clearCacheForScope: accessToken: string -> unit
    abstract openPopup: obj with get, set
    abstract validateInputScope: obj with get, set
    abstract filterScopes: obj with get, set
    abstract registerCallback: obj with get, set
    abstract getCachedTokenInternal: scopes: Array<string> * user: User -> CacheResult
    abstract getCachedToken: obj with get, set
    abstract getAllUsers: unit -> Array<User>
    abstract getUniqueUsers: obj with get, set
    abstract getUniqueAuthority: obj with get, set
    abstract addHintParameters: obj with get, set
    abstract urlContainsQueryStringParameter: obj with get, set
    abstract acquireTokenRedirect: scopes: Array<string> -> unit
    abstract acquireTokenRedirect: scopes: Array<string> * authority: string -> unit
    abstract acquireTokenRedirect: scopes: Array<string> * authority: string * user: User -> unit
    abstract acquireTokenRedirect: scopes: Array<string> * authority: string * user: User * extraQueryParameters: string -> unit
    abstract acquireTokenPopup: scopes: Array<string> -> Promise<string>
    abstract acquireTokenPopup: scopes: Array<string> * authority: string -> Promise<string>
    abstract acquireTokenPopup: scopes: Array<string> * authority: string * user: User -> Promise<string>
    abstract acquireTokenPopup: scopes: Array<string> * authority: string * user: User * extraQueryParameters: string -> Promise<string>
    abstract acquireTokenSilent: scopes: Array<string> * ?authority: string * ?user: User * ?extraQueryParameters: string -> Promise<string>
    abstract loadIframeTimeout: obj with get, set
    abstract loadFrame: obj with get, set
    abstract addAdalFrame: obj with get, set
    abstract renewToken: obj with get, set
    abstract renewIdToken: obj with get, set
    abstract getUser: unit -> User
    abstract handleAuthenticationResponse: obj with get, set
    abstract saveAccessToken: obj with get, set
    abstract saveTokenFromHash: tokenResponse: TokenResponse -> unit
    abstract isCallback: hash: string -> bool
    abstract getHash: obj with get, set
    abstract getRequestInfo: hash: string -> TokenResponse
    abstract getScopeFromState: obj with get, set
    abstract getUserState: state: string -> string
    abstract isInIframe: obj with get, set
    abstract loginInProgress: unit -> bool
    abstract getHostFromUri: obj with get, set
    abstract getScopesForEndpoint: endpoint: string -> Array<string>
    abstract setloginInProgress: loginInProgress: bool -> unit
    abstract getAcquireTokenInProgress: unit -> bool
    abstract setAcquireTokenInProgress: acquireTokenInProgress: bool -> unit
    abstract getLogger: unit -> Logger

type [<AllowNullLiteral>] UserAgentApplicationStatic =
    [<Emit "new $0($1...)">] abstract Create: clientId: string * authority: string option * tokenReceivedCallback: tokenReceivedCallback * ?options: UserAgentApplicationStaticOptions -> UserAgentApplication

type [<AllowNullLiteral>] UserAgentApplicationStaticOptions =
    abstract validateAuthority: bool option with get, set
    abstract cacheLocation: string option with get, set
    abstract redirectUri: string option with get, set
    abstract postLogoutRedirectUri: string option with get, set
    abstract logger: Logger option with get, set
    abstract loadFrameTimeout: float option with get, set
    abstract navigateToLoginRequestUrl: bool option with get, set
    abstract state: string option with get, set
    abstract isAngular: bool option with get, set
    abstract unprotectedResources: Array<string> option with get, set
    abstract protectedResourceMap: Map<string, Array<string>> option with get, set
    abstract storeAuthStateInCookie: bool option with get, set

type [<AllowNullLiteral>] Constants =
    abstract errorDescription: string
    abstract error: string
    abstract scope: string
    abstract acquireTokenUser: string
    abstract clientInfo: string
    abstract clientId: string
    abstract authority: string
    abstract idToken: string
    abstract accessToken: string
    abstract expiresIn: string
    abstract sessionState: string
    abstract msalClientInfo: string
    abstract msalError: string
    abstract msalErrorDescription: string
    abstract msalSessionState: string
    abstract tokenKeys: string
    abstract accessTokenKey: string
    abstract expirationKey: string
    abstract stateLogin: string
    abstract stateAcquireToken: string
    abstract stateRenew: string
    abstract nonceIdToken: string
    abstract userName: string
    abstract idTokenKey: string
    abstract loginRequest: string
    abstract loginError: string
    abstract renewStatus: string
    abstract msal: string
    abstract no_user: string
    abstract login_hint: string
    abstract domain_hint: string
    abstract sid: string
    abstract prompt_select_account: string
    abstract prompt_none: string
    abstract response_mode_fragment: string
    abstract resourceDelimeter: string
    abstract tokenRenewStatusCancelled: string
    abstract tokenRenewStatusCompleted: string
    abstract tokenRenewStatusInProgress: string
    abstract _popUpWidth: obj with get, set
    abstract popUpWidth: float with get, set
    abstract _popUpHeight: obj with get, set
    abstract popUpHeight: float with get, set
    abstract login: string
    abstract renewToken: string
    abstract unknown: string
    abstract urlHash: string
    abstract angularLoginRequest: string
    abstract userIdentifier: string

type [<AllowNullLiteral>] ConstantsStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> Constants

type [<AllowNullLiteral>] ErrorCodes =
    abstract loginProgressError: string
    abstract acquireTokenProgressError: string
    abstract inputScopesError: string
    abstract endpointResolutionError: string
    abstract popUpWindowError: string
    abstract userLoginError: string
    abstract userCancelledError: string

type [<AllowNullLiteral>] ErrorCodesStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> ErrorCodes

type [<AllowNullLiteral>] ErrorDescription =
    abstract loginProgressError: string
    abstract acquireTokenProgressError: string
    abstract inputScopesError: string
    abstract endpointResolutionError: string
    abstract popUpWindowError: string
    abstract userLoginError: string
    abstract userCancelledError: string

type [<AllowNullLiteral>] ErrorDescriptionStatic =
    [<Emit "new $0($1...)">] abstract Create: unit -> ErrorDescription
