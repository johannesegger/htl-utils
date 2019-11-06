module PhotoLibrary

open Thoth.Json.Net

type TeacherPhoto = {
    FirstName: string
    LastName: string
    Data: Base64EncodedImage
}
module TeacherPhoto =
    let decoder : Decoder<_> =
        Decode.object(fun get -> {
            FirstName = get.Required.Field "firstName" Decode.string
            LastName = get.Required.Field "lastName" Decode.string
            Data = get.Required.Field "data" (Decode.string |> Decode.map Base64EncodedImage)
        })