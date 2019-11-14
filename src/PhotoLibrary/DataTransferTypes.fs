module PhotoLibrary.DataTransferTypes

open Thoth.Json.Net

type Base64EncodedImage = Base64EncodedImage of string
module Base64EncodedImage =
    let encode (Base64EncodedImage v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map Base64EncodedImage

type TeacherPhoto = {
    FirstName: string
    LastName: string
    Data: Base64EncodedImage
}
module TeacherPhoto =
    let encode v =
        Encode.object [
            "firstName", Encode.string v.FirstName
            "lastName", Encode.string v.LastName
            "data", Base64EncodedImage.encode v.Data
        ]
    let decoder : Decoder<_> =
        Decode.object(fun get -> {
            FirstName = get.Required.Field "firstName" Decode.string
            LastName = get.Required.Field "lastName" Decode.string
            Data = get.Required.Field "data" (Decode.string |> Decode.map Base64EncodedImage)
        })
