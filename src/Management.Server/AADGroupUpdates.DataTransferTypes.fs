namespace AADGroupUpdates.DataTransferTypes

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

type UserId = UserId of string
module UserId =
    let encode (UserId v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map UserId

type GroupId = GroupId of string
module GroupId =
    let encode (GroupId v) = Encode.string v
    let decoder : Decoder<_> = Decode.string |> Decode.map GroupId

type User =
    {
        Id: UserId
        UserName: string
        FirstName: string
        LastName: string
    }

type MemberUpdates =
    {
        AddMembers: User list
        RemoveMembers: User list
    }
module MemberUpdates =
    let isEmpty memberUpdates =
        memberUpdates.AddMembers.IsEmpty && memberUpdates.RemoveMembers.IsEmpty

type Group = {
    Id: GroupId
    Name: string
}

type GroupUpdate =
    | CreateGroup of string * User list
    | UpdateGroup of Group * MemberUpdates
    | DeleteGroup of Group

module Thoth =
    let addCoders =
        Extra.withCustom UserId.encode UserId.decoder
        >> Extra.withCustom GroupId.encode GroupId.decoder
