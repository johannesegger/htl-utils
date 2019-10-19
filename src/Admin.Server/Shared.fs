namespace Shared

#if FABLE_COMPILER
open Thoth.Json
#else
open Thoth.Json.Net
#endif

module AADGroupUpdates =
    type UserId = UserId of string

    module UserId =
        let encode (UserId userId) = Encode.string userId
        let decode : Decoder<_> = Decode.string |> Decode.map UserId

    type GroupId = GroupId of string

    module GroupId =
        let encode (GroupId userId) = Encode.string userId
        let decode : Decoder<_> = Decode.string |> Decode.map GroupId

    type User =
        {
            Id: UserId
            ShortName: string
            FirstName: string
            LastName: string
        }

    module User =
        let encode u =
            Encode.object [
                "id", UserId.encode u.Id
                "shortName", Encode.string u.ShortName
                "firstName", Encode.string u.FirstName
                "lastName", Encode.string u.LastName
            ]
        let decode : Decoder<_> =
            Decode.object (fun get ->
                {
                    Id = get.Required.Field "id" UserId.decode
                    ShortName = get.Required.Field "shortName" Decode.string
                    FirstName = get.Required.Field "firstName" Decode.string
                    LastName = get.Required.Field "lastName" Decode.string
                }
            )

    type MemberUpdates =
        {
            AddMembers: User list
            RemoveMembers: User list
        }

    module MemberUpdates =
        let encode memberUpdates =
            Encode.object [
                "addMembers", (List.map User.encode >> Encode.list) memberUpdates.AddMembers
                "removeMembers", (List.map User.encode >> Encode.list) memberUpdates.RemoveMembers
            ]
        let decode : Decoder<_> =
            Decode.object (fun get -> {
                AddMembers = get.Required.Field "addMembers" (Decode.list User.decode)
                RemoveMembers = get.Required.Field "removeMembers" (Decode.list User.decode)
            })

    type Group = {
        Id: GroupId
        Name: string
    }

    module Group =
        let encode u =
            Encode.object [
                "id", GroupId.encode u.Id
                "name", Encode.string u.Name
            ]
        let decode : Decoder<_> =
            Decode.object (fun get ->
                {
                    Id = get.Required.Field "id" GroupId.decode
                    Name = get.Required.Field "name" Decode.string
                }
            )

    type GroupUpdate =
        | CreateGroup of string * User list
        | UpdateGroup of Group * MemberUpdates
        | DeleteGroup of Group

    module GroupUpdate =
        let encode = function
            | CreateGroup (name, members) -> Encode.object [ "createGroup", Encode.tuple2 Encode.string (List.map User.encode >> Encode.list) (name, members) ]
            | UpdateGroup (group, memberUpdates) -> Encode.object [ "updateGroup", Encode.tuple2 Group.encode MemberUpdates.encode (group, memberUpdates) ]
            | DeleteGroup group -> Encode.object [ "deleteGroup", Group.encode group ]
        let decode : Decoder<_> =
            Decode.oneOf [
                Decode.field "createGroup" (Decode.tuple2 Decode.string (Decode.list User.decode)) |> Decode.map CreateGroup
                Decode.field "updateGroup" (Decode.tuple2 Group.decode MemberUpdates.decode) |> Decode.map UpdateGroup
                Decode.field "deleteGroup" Group.decode |> Decode.map DeleteGroup
            ]
