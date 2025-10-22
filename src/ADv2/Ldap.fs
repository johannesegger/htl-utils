module AD.Ldap

open AD.Configuration
open AD.Directory
open System
open System.DirectoryServices.Protocols
open System.Globalization
open System.Text
open System.Text.RegularExpressions

module SearchResultEntry =
    module private TryConvert =
        let toString (v: obj) =
            match v with
            | :? string as value -> Some value
            | :? (byte[]) as content -> Some <| Encoding.UTF8.GetString content
            | _ -> None
        let toInt (v: obj) =
            match toString v with
            | Some v ->
                match Int32.TryParse v with
                | (true, v) -> Some v
                | (false, _) -> None
            | None -> None
        let toByteArray (v: obj) =
            match v with
            | :? (byte[]) as content -> Some content
            | _ -> None
        let toDateTime (v: obj) =
            let tryParse (v: string) =
                tryDo (fun text -> DateTime.TryParseExact(text, "yyyyMMddHHmmss.0Z", CultureInfo.InvariantCulture, DateTimeStyles.None)) v
            match v with
            | :? string as value -> tryParse value
            | :? (byte[]) as content -> Encoding.UTF8.GetString content |> tryParse
            | _ -> None

    let private getAttributeValues attributeName (object: SearchResultEntry) tryConvert =
        object.Attributes.[attributeName]
        :> Collections.IEnumerable
        |> Option.ofObj
        |> Option.defaultValue []
        |> Seq.cast<obj>
        |> Seq.map tryConvert
        |> Option.sequence
    let private getSingleAttributeValue attributeName object tryConvert =
        getAttributeValues attributeName object tryConvert
        |> Option.bind List.tryExactlyOne
    let private getOptionalAttributeValue attributeName object tryConvert =
        match getAttributeValues attributeName object tryConvert with
        | Some []
        | None -> Some None
        | Some [v] -> Some (Some v)
        | _ -> None

    let getStringAttributeValues attributeName object =
        getAttributeValues attributeName object TryConvert.toString
        |> Option.defaultWith (fun () -> failwith $"Attribute \"{attributeName}\" of object \"{object.DistinguishedName}\" is not a string list")

    let getStringAttributeValue attributeName object =
        getSingleAttributeValue attributeName object TryConvert.toString
        |> Option.defaultWith (fun () -> failwith $"Attribute \"{attributeName}\" of object \"{object.DistinguishedName}\" is not a single string")
    let getBytesAttributeValue attributeName object =
        getSingleAttributeValue attributeName object TryConvert.toByteArray
        |> Option.defaultWith (fun () -> failwith $"Attribute \"{attributeName}\" of object \"{object.DistinguishedName}\" is not a single byte array")
    let getDateTimeAttributeValue attributeName object =
        getSingleAttributeValue attributeName object TryConvert.toDateTime
        |> Option.defaultWith (fun () -> failwith $"Attribute \"{attributeName}\" of object \"{object.DistinguishedName}\" is not a timestamp")
    let getIntAttributeValue attributeName object =
        getSingleAttributeValue attributeName object TryConvert.toInt
        |> Option.defaultWith (fun () -> failwith $"Attribute \"{attributeName}\" of object \"{object.DistinguishedName}\" is not a single int")
    let getOptionalStringAttributeValue attributeName object =
        getOptionalAttributeValue attributeName object TryConvert.toString
        |> Option.defaultWith (fun () -> failwith $"Attribute \"{attributeName}\" of object \"{object.DistinguishedName}\" is neither empty nor a single string")

type Ldap(config: LdapConnectionConfig) =
    let connection =
        let c = new LdapConnection(
            LdapDirectoryIdentifier(config.HostName, 636),
            Net.NetworkCredential(config.UserName, config.Password),
            AuthType.Basic
        )
        c.SessionOptions.SecureSocketLayer <- true
        c.SessionOptions.ProtocolVersion <- 3 // v2 e.g. doesn't allow ModifyDNRequest to move object to different OU
        c

    let gate = Object()

    let sendRequest (request: #DirectoryRequest) : Async<#DirectoryResponse> = async {
        return lock gate (fun () ->
            connection.SendRequest(request) :?> 'res
        )
    }
    let search (request: SearchRequest) : Async<SearchResponse> = sendRequest request
    let add (request: AddRequest) : Async<AddResponse> = sendRequest request
    let modify (request: ModifyRequest) : Async<ModifyResponse> = sendRequest request
    let delete (request: DeleteRequest) : Async<DeleteResponse> = sendRequest request
    let modifyDN (request: ModifyDNRequest) : Async<ModifyDNResponse> = sendRequest request

    let directoryAttributeValues v =
        match v with
        | Unset -> Array.zeroCreate<string> 0 :> obj :?> obj[]
        | Text v -> [| v |] :> obj :?> obj[]
        | Bytes v -> [| v |] :> obj :?> obj[]
        | TextList v -> v |> List.toArray :> obj :?> obj[]
    let directoryAttribute name value =
        DirectoryAttribute(name, directoryAttributeValues value)
    let directoryAttributes =
        List.map (uncurry directoryAttribute) >> List.toArray
    let directoryAttributeModification name operation value =
        let v = DirectoryAttributeModification(Name = name, Operation = operation)
        v.AddRange(directoryAttributeValues value)
        v
    let directoryAttributeModifications operation =
        List.map (fun (name, value) -> directoryAttributeModification name operation value) >> List.toArray

    let findDescendants (DistinguishedName parentDn) ldapFilter attributes = async {
        try
            let! response =
                SearchRequest(parentDn, ldapFilter, SearchScope.Subtree, attributes)
                |> search
            return
                response.Entries
                |> Seq.cast<SearchResultEntry>
                |> Seq.toList
        with :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.NoSuchObject ->
            return []
    }

    let createNodeIfNotExists (DistinguishedName nodeDn) nodeType properties = async {
        printfn "Creating %A" nodeDn
        let attributes =
            [|
                DirectoryAttribute("objectClass", NodeType.toString nodeType)
                yield! directoryAttributes properties
            |]

        try
            do!
                AddRequest(nodeDn, attributes)
                |> add
                |> Async.Ignore
            return true
        with
            | :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.EntryAlreadyExists ->
                return false
            | e -> return failwith $"Error while creating \"%s{nodeDn}\" with attributes \"%A{attributes}\": %s{e.Message}"
    }

    let createParents node = async {
        let! createdParents =
            DN.parentsAndSelf node
            |> List.filter DN.isOU
            |> List.filter ((<>) node)
            |> List.map (fun path -> async {
                let! isNew = createNodeIfNotExists path ADOrganizationalUnit []
                if isNew then return Some path
                else return None
            })
            |> Async.Sequential
        return
            createdParents
            |> Array.choose id
            |> Array.toList
    }

    interface IDisposable with
        member _.Dispose() = connection.Dispose()

    member _.FindObjectByDn (DistinguishedName objectDn,  attributes) = async {
        let! response =
            SearchRequest(objectDn, null, SearchScope.Base, attributes)
            |> search

        return
            response.Entries
            |> Seq.cast<SearchResultEntry>
            |> Seq.tryHead
            |> Option.defaultWith (fun () -> failwith $"Object \"{objectDn}\" not found")
    }

    member this.FindGroupMembersIfGroupExists (groupDn) = async {
        try
            let! group = this.FindObjectByDn(groupDn, [| "member" |])
            return
                group
                |> SearchResultEntry.getStringAttributeValues "member"
                |> List.map DistinguishedName
        with :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.NoSuchObject ->
            return []
    }

    member _.FindRecursiveGroupMembersIfGroupExists (DistinguishedName groupDn, attributes) = async {
        let! (response: SearchResponse) = async {
            let (DistinguishedName baseDn) = DN.domainBase (DistinguishedName groupDn)
            return!
                SearchRequest(baseDn, $"(&(objectClass=user)(memberof:1.2.840.113556.1.4.1941:=%s{groupDn}))", SearchScope.Subtree, attributes)
                |> search
        }
        return
            response.Entries
            |> Seq.cast<SearchResultEntry>
            |> Seq.toList
    }

    member _.FindFullGroupMembers (DistinguishedName groupDn, attributes) = async {
        let! response = async {
            let (DistinguishedName baseDn) = DN.domainBase (DistinguishedName groupDn)
            return!
                SearchRequest(baseDn, $"(memberof={groupDn})", SearchScope.Subtree, attributes)
                |> search
        }
        return
            response.Entries
            |> Seq.cast<SearchResultEntry>
            |> Seq.toList
    }

    member _.FindDescendantUsers (parentOU, attributes) =
        findDescendants parentOU "(&(objectCategory=person)(objectClass=user))" attributes

    member _.FindDescendantComputers (parentOU, attributes) =
        findDescendants parentOU "(objectCategory=computer)" attributes

    member _.CreateNodeIfNotExists (nodeDn, nodeType, properties) = async {
        do! createNodeIfNotExists nodeDn nodeType properties |> Async.Ignore
    }

    member _.CreateNodeAndParents (node, nodeType, properties) = async {
        let! parentNodes = createParents node
        let! isNew = createNodeIfNotExists node nodeType properties
        if isNew then return parentNodes @ [ node ]
        else return parentNodes
    }

    member _.MoveNode (DistinguishedName source, target) = async {
        let (DistinguishedName targetParentDn) = DN.parent target
        let newName = DN.head target |> uncurry (sprintf "%s=%s")
        do! createParents target |> Async.Ignore
        try
            do!
                ModifyDNRequest(source, targetParentDn, newName)
                |> modifyDN
                |> Async.Ignore
        with e -> failwith $"Error while moving \"{DistinguishedName source}\" to \"{target}\": {e.Message}"
    }

    member _.SetNodeProperties (DistinguishedName node, properties) = async {
        try
            do!
                ModifyRequest(node, directoryAttributeModifications DirectoryAttributeOperation.Replace properties)
                |> modify
                |> Async.Ignore
        with e -> failwith $"Error while setting node properties {properties} of \"{node}\": {e.Message}"
    }
    member this.ReplaceTextInNodePropertyValues (node, properties: {| Name: string; Pattern: Regex; Replacement: string |} list) = async {
        let propertyNames = properties |> List.map (fun v -> v.Name) |> List.toArray
        let! user = async {
            return! this.FindObjectByDn(node, propertyNames)
        }
        let propertyValueMap =
            propertyNames
            |> Seq.map (fun propertyName ->
                let value = SearchResultEntry.getStringAttributeValue propertyName user
                (propertyName, value)
            )
            |> Map.ofSeq
        let properties =
            properties
            |> List.map (fun v ->
                let currentValue = Map.find v.Name propertyValueMap
                let newValue = v.Pattern.Replace(currentValue, v.Replacement)
                v.Name, Text newValue
            )
        do! this.SetNodeProperties(node, properties)
    }
    member _.DeleteNode (DistinguishedName node) = async {
        try
            do!
                DeleteRequest(node)
                |> delete
                |> Async.Ignore
        with
            | :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.NoSuchObject -> ()
            | e -> failwith $"Error while deleting \"%s{node}\": {e.Message}"
    }

    member this.DisableAccount (userDn) = async {
        let! user = this.FindObjectByDn (userDn, [| "userAccountControl" |])
        let userAccountControl =
            SearchResultEntry.getIntAttributeValue "userAccountControl" user
        let properties = [
            ("userAccountControl", Text $"{userAccountControl ||| UserAccountControl.ACCOUNTDISABLE}")
        ]
        do! this.SetNodeProperties (userDn, properties)
    }

    member this.EnableAccount (userDn) = async {
        let! user = this.FindObjectByDn (userDn, [| "userAccountControl" |])
        let userAccountControl =
            SearchResultEntry.getIntAttributeValue "userAccountControl" user
        let properties = [
            ("userAccountControl", Text $"{userAccountControl &&& ~~~UserAccountControl.ACCOUNTDISABLE}")
        ]
        do! this.SetNodeProperties(userDn, properties)
    }
    member _.AddObjectToGroup (DistinguishedName group, DistinguishedName object) = async {
        let modification = directoryAttributeModification "member" DirectoryAttributeOperation.Add (Text object)
        try
            do!
                ModifyRequest(group, modification)
                |> modify
                |> Async.Ignore
        with
            | :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.EntryAlreadyExists -> ()
            | e -> failwith $"Error while adding \"%s{object}\" to group \"%s{group}\": {e.Message}"
    }
    member _.RemoveObjectFromGroup (DistinguishedName group, DistinguishedName object) = async {
        let modification = directoryAttributeModification "member" DirectoryAttributeOperation.Delete (Text object)
        try
            do!
                ModifyRequest(group, modification)
                |> modify
                |> Async.Ignore
        with
            | :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.UnwillingToPerform -> ()
            | e -> failwith $"Error while removing \"%s{object}\" from group \"%s{group}\": {e.Message}"
    }

    member this.RemoveGroupMemberships nodeDn = async {
        let! node = this.FindObjectByDn (nodeDn, [| "memberOf" |])
        do!
            SearchResultEntry.getStringAttributeValues "memberOf" node
            |> List.map (DistinguishedName >> fun groupDn -> async {
                do! this.RemoveObjectFromGroup (groupDn, nodeDn)
            })
            |> Async.Sequential
            |> Async.Ignore
    }
