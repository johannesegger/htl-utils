namespace GenerateITInformationSheet.DataTransferTypes

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type User = {
    ShortName: string
    FirstName: string
    LastName: string
}

type Base64EncodedContent = Base64EncodedContent of string
module Base64EncodedContent =
    let encode (Base64EncodedContent v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map Base64EncodedContent

type InformationSheet = {
    Title: string
    Content: Base64EncodedContent
}

module Thoth =
    let addCoders =
        Extra.withCustom Base64EncodedContent.encode Base64EncodedContent.decoder
