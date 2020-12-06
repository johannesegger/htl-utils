namespace GenerateITInformationSheet.DataTransferTypes

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type User =
    {
        ShortName: string
        FirstName: string
        LastName: string
    }

module User =
    let encode t =
        Encode.object [
            "shortName", Encode.string t.ShortName
            "firstName", Encode.string t.FirstName
            "lastName", Encode.string t.LastName
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                ShortName = get.Required.Field "shortName" Decode.string
                FirstName = get.Required.Field "firstName" Decode.string
                LastName = get.Required.Field "lastName" Decode.string
            }
        )

type Base64EncodedContent = Base64EncodedContent of string
module Base64EncodedContent =
    let encode (Base64EncodedContent v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map Base64EncodedContent

type InformationSheet =
    {
        Title: string
        Content: Base64EncodedContent
    }
module InformationSheet =
    let encode v =
        Encode.object [
            "title", Encode.string v.Title
            "content", Base64EncodedContent.encode v.Content
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Title = get.Required.Field "title" Decode.string
                Content = get.Required.Field "content" Base64EncodedContent.decoder
            }
        )
