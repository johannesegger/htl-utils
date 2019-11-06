namespace global

open Thoth.Json.Net

type Base64EncodedImage = Base64EncodedImage of string
module Base64EncodedImage =
    let encode (Base64EncodedImage v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map Base64EncodedImage
