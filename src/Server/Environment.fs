module Environment

open System
open System.Net

module AAD =
    let clientId = Environment.getEnvVarOrFail "MICROSOFT_GRAPH_CLIENT_ID"
    let tenantId = Environment.getEnvVarOrFail "MICROSOFT_GRAPH_TENANT_ID"
    let appKey = Environment.getEnvVarOrFail "MICROSOFT_GRAPH_APP_KEY"
    let authority = Environment.getEnvVarOrFail "MICROSOFT_GRAPH_AUTHORITY"
    let username = Environment.getEnvVarOrFail "MICROSOFT_GRAPH_USERNAME"
    let password = Environment.getEnvVarOrFail "MICROSOFT_GRAPH_PASSWORD"
    let securePassword = NetworkCredential("", password).SecurePassword

module WebUntis =
    let baseUrl = Environment.getEnvVarOrFail "WEBUNTIS_BASE_URL" |> Uri
    let schoolName = Environment.getEnvVarOrFail "WEBUNTIS_SCHOOL_NAME"
    let username = Environment.getEnvVarOrFail "WEBUNTIS_USERNAME"
    let password = Environment.getEnvVarOrFail "WEBUNTIS_PASSWORD"
