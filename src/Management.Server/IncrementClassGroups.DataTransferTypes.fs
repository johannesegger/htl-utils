namespace IncrementClassGroups.DataTransferTypes

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type ClassGroupModification =
    | ChangeClassGroupName of oldName: string * newName: string
    | DeleteClassGroup of string

type ClassGroupModificationGroup = {
    Title: string
    Modifications: ClassGroupModification list
}

module Thoth =
    let addCoders (v: ExtraCoders) = v
