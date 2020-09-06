module IncrementClassGroups.DataTransferTypes

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type ClassGroupModification =
    | ChangeClassGroupName of oldName: string * newName: string
    | DeleteClassGroup of string
module ClassGroupModification =
    let encode = function
        | ChangeClassGroupName (oldName, newName) -> Encode.object [ "changeClassGroupName", Encode.tuple2 Encode.string Encode.string (oldName, newName) ]
        | DeleteClassGroup groupName -> Encode.object [ "deleteClassGroup", Encode.string groupName ]
    let decoder : Decoder<_> =
        Decode.oneOf [
            Decode.field "changeClassGroupName" (Decode.tuple2 Decode.string Decode.string) |> Decode.map ChangeClassGroupName
            Decode.field "deleteClassGroup" Decode.string |> Decode.map DeleteClassGroup
        ]

type ClassGroupModificationGroup = {
    Title: string
    Modifications: ClassGroupModification list
}
module ClassGroupModificationGroup =
    let encode g =
        Encode.object [
            "title", Encode.string g.Title
            "modifications", (List.map ClassGroupModification.encode >> Encode.list) g.Modifications
        ]
    let decoder : Decoder<_> =
        Decode.object (fun get ->
            {
                Title = get.Required.Field "title" Decode.string
                Modifications = get.Required.Field "modifications" (Decode.list ClassGroupModification.decoder)
            }
        )
