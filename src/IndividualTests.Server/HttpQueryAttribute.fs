namespace IndividualTests.Server

open Microsoft.AspNetCore.Mvc.Routing
open System.Diagnostics.CodeAnalysis

type HttpQueryAttribute =
    inherit HttpMethodAttribute

    new() = { inherit HttpMethodAttribute(["QUERY"]) }
    new([<StringSyntax("Route")>] template: string) = { inherit HttpMethodAttribute(["QUERY"], template) }
