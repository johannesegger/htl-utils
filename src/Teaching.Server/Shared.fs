namespace Shared

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type CreateDirectoriesData = {
    ClassName: string
    Path: string
}
module CreateDirectoriesData =
    let encode v =
        Encode.object [
            "className", Encode.string v.ClassName
            "path", Encode.string v.Path
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            ClassName = get.Required.Field "className" Decode.string
            Path = get.Required.Field "path" Decode.string
        })
