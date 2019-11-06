module Shared

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type Student = {
    LastName: string
    FirstName: string
}
module Student =
    let decoder : Decoder<_> =
        Decode.object (fun get -> {
            LastName = get.Required.Field "lastName" Decode.string
            FirstName = get.Required.Field "firstName1" Decode.string
        })