module PhotoLibrary.DataTransferTypes

open Thoth.Json.Net

type Base64EncodedImage = Base64EncodedImage of string
module Base64EncodedImage =
    let encode (Base64EncodedImage v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map Base64EncodedImage

type TeacherPhoto = {
    ShortName: string
    Data: Base64EncodedImage
}
module TeacherPhoto =
    let encode v =
        Encode.object [
            "shortName", Encode.string v.ShortName
            "data", Base64EncodedImage.encode v.Data
        ]
    let decoder : Decoder<_> =
        Decode.object(fun get -> {
            ShortName = get.Required.Field "shortName" Decode.string
            Data = get.Required.Field "data" Base64EncodedImage.decoder
        })

type SokratesId = SokratesId of string
module SokratesIdModule =
    let encode (SokratesId v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map SokratesId

type StudentPhoto = {
    StudentId: SokratesId
    Data: Base64EncodedImage
}
module StudentPhoto =
    let encode v =
        Encode.object [
            "studentId", SokratesIdModule.encode v.StudentId
            "data", Base64EncodedImage.encode v.Data
        ]
    let decoder : Decoder<_> =
        Decode.object(fun get -> {
            StudentId = get.Required.Field "studentId" SokratesIdModule.decoder
            Data = get.Required.Field "data" Base64EncodedImage.decoder
        })
