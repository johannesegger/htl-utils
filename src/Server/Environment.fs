module Environment

open System
open System.Net

let getEnvVar name =
    Environment.GetEnvironmentVariable name

let getEnvVarOrFail name =
    let value = getEnvVar name
    if isNull value
    then failwithf "Environment variable \"%s\" not set" name
    else value

module AAD =
    let clientId = getEnvVarOrFail "MICROSOFT_GRAPH_CLIENT_ID"
    let tenantId = getEnvVarOrFail "MICROSOFT_GRAPH_TENANT_ID"
    let appKey = getEnvVarOrFail "MICROSOFT_GRAPH_APP_KEY"
    let authority = getEnvVarOrFail "MICROSOFT_GRAPH_AUTHORITY"
    let username = getEnvVarOrFail "MICROSOFT_GRAPH_USERNAME"
    let password = getEnvVarOrFail "MICROSOFT_GRAPH_PASSWORD"
    let securePassword = NetworkCredential("", password).SecurePassword

module WebUntis =
    let baseUrl = getEnvVarOrFail "WEBUNTIS_BASE_URL" |> Uri
    let schoolName = getEnvVarOrFail "WEBUNTIS_SCHOOL_NAME"
    let username = getEnvVarOrFail "WEBUNTIS_USERNAME"
    let password = getEnvVarOrFail "WEBUNTIS_PASSWORD"
