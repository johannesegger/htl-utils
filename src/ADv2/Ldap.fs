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

module Ldap =
    let connect config =
        let connection =
            new LdapConnection(
                LdapDirectoryIdentifier(config.HostName, 636),
                Net.NetworkCredential(config.UserName, config.Password),
                AuthType.Basic
            )
        connection.SessionOptions.SecureSocketLayer <- true
        connection.SessionOptions.VerifyServerCertificate <- fun conn cert -> true
        connection.SessionOptions.ProtocolVersion <- 3 // v2 e.g. doesn't allow ModifyDNRequest to move object to different OU
        connection
    let private sendRequest<'req, 'res when 'req :> DirectoryRequest and 'res :> DirectoryResponse> (connection: LdapConnection) (request: 'req) = async {
        #if DEBUG
        let response = connection.SendRequest(request)
        #else
        let! response =
            Async.FromBeginEnd(
                (fun (callback, state) -> connection.BeginSendRequest(request, PartialResultProcessing.NoPartialResultSupport, callback, state)),
                connection.EndSendRequest,
                ignore)
        #endif
        return response :?> 'res
    }
    let private search = sendRequest<SearchRequest, SearchResponse>
    let private add = sendRequest<AddRequest, AddResponse>
    let private modify = sendRequest<ModifyRequest, ModifyResponse>
    let private delete = sendRequest<DeleteRequest, DeleteResponse>
    let private modifyDN = sendRequest<ModifyDNRequest, ModifyDNResponse>

    let private directoryAttributeValues v =
        match v with
        | Unset -> Array.zeroCreate<string> 0 :> obj :?> obj[]
        | Text v -> [| v |] :> obj :?> obj[]
        | Bytes v -> [| v |] :> obj :?> obj[]
        | TextList v -> v |> List.toArray :> obj :?> obj[]
    let private directoryAttribute name value =
        DirectoryAttribute(name, directoryAttributeValues value)
    let private directoryAttributes =
        List.map (uncurry directoryAttribute) >> List.toArray
    let private directoryAttributeModification name operation value =
        let v = DirectoryAttributeModification(Name = name, Operation = operation)
        v.AddRange(directoryAttributeValues value)
        v
    let private directoryAttributeModifications operation =
        List.map (fun (name, value) -> directoryAttributeModification name operation value) >> List.toArray

    let findObjectByDn (connection: LdapConnection) (DistinguishedName objectDn) attributes = async {
        let! response =
            SearchRequest(objectDn, null, SearchScope.Base, attributes)
            |> search connection

        return
            response.Entries
            |> Seq.cast<SearchResultEntry>
            |> Seq.tryHead
            |> Option.defaultWith (fun () -> failwith $"Object \"{objectDn}\" not found")
    }

    let findGroupMembersIfGroupExists (connection: LdapConnection) groupDn = async {
        try
            let! group = findObjectByDn connection groupDn [| "member" |]
            return
                group
                |> SearchResultEntry.getStringAttributeValues "member"
                |> List.map DistinguishedName
        with :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.NoSuchObject ->
            return []
    }

    let findRecursiveGroupMembersIfGroupExists (connection: LdapConnection) (DistinguishedName groupDn) attributes = async {
        let! (response: SearchResponse) = async {
            let (DistinguishedName baseDn) = DN.domainBase (DistinguishedName groupDn)
            return!
                SearchRequest(baseDn, $"(&(objectClass=user)(memberof:1.2.840.113556.1.4.1941:=%s{groupDn}))", SearchScope.Subtree, attributes)
                |> search connection
        }
        return
            response.Entries
            |> Seq.cast<SearchResultEntry>
            |> Seq.toList
    }

    let findFullGroupMembers (connection: LdapConnection) (DistinguishedName groupDn) attributes = async {
        let! response = async {
            let (DistinguishedName baseDn) = DN.domainBase (DistinguishedName groupDn)
            return!
                SearchRequest(baseDn, $"(memberof={groupDn})", SearchScope.Subtree, attributes)
                |> search connection
        }
        return
            response.Entries
            |> Seq.cast<SearchResultEntry>
            |> Seq.toList
    }
    let private findDescendants (connection: LdapConnection) (DistinguishedName parentDn) ldapFilter attributes = async {
        try
            let! response =
                SearchRequest(parentDn, ldapFilter, SearchScope.Subtree, attributes)
                |> search connection
            return
                response.Entries
                |> Seq.cast<SearchResultEntry>
                |> Seq.toList
        with :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.NoSuchObject ->
            return []
    }
    let findDescendantUsers connection parentOU attributes =
        findDescendants connection parentOU "(&(objectCategory=person)(objectClass=user))" attributes
    let findDescendantComputers connection parentOU attributes =
        findDescendants connection parentOU "(objectCategory=computer)" attributes
    let private createNodeIfNotExists connection (DistinguishedName nodeDn) nodeType properties = async {
        let attributes =
            [|
                DirectoryAttribute("objectClass", NodeType.toString nodeType)
                yield! directoryAttributes properties
            |]

        try
            do!
                AddRequest(nodeDn, attributes)
                |> add connection
                |> Async.Ignore
            return true
        with
            | :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.EntryAlreadyExists ->
                return false
            | e -> return failwith $"Error while creating \"%s{nodeDn}\" with attributes \"%A{attributes}\": %s{e.Message}"
    }
    let private createParents connection node = async {
        let! createdParents =
            DN.parentsAndSelf node
            |> List.filter DN.isOU
            |> List.filter ((<>) node)
            |> List.map (fun path -> async {
                let! isNew = createNodeIfNotExists connection path ADOrganizationalUnit []
                if isNew then return Some path
                else return None
            })
            |> Async.Sequential
        return
            createdParents
            |> Array.choose id
            |> Array.toList
    }
    let createNodeAndParents connection node nodeType properties = async {
        let! parentNodes = createParents connection node
        let! isNew = createNodeIfNotExists connection node nodeType properties
        if isNew then return parentNodes @ [ node ]
        else return parentNodes
    }
    let moveNode connection (DistinguishedName source) target = async {
        let (DistinguishedName targetParentDn) = DN.parent target
        let newName = DN.head target |> uncurry (sprintf "%s=%s")
        do! createParents connection target |> Async.Ignore
        try
            do!
                ModifyDNRequest(source, targetParentDn, newName)
                |> modifyDN connection
                |> Async.Ignore
        with e -> failwith $"Error while moving \"{DistinguishedName source}\" to \"{target}\": {e.Message}"
    }
    let setNodeProperties connection (DistinguishedName node) properties = async {
        try
            do!
                ModifyRequest(node, directoryAttributeModifications DirectoryAttributeOperation.Replace properties)
                |> modify connection
                |> Async.Ignore
        with e -> failwith $"Error while setting node properties {properties} of \"{node}\": {e.Message}"
    }
    let replaceTextInNodePropertyValues connection node (properties: {| Name: string; Pattern: Regex; Replacement: string |} list) = async {
        let propertyNames = properties |> List.map (fun v -> v.Name) |> List.toArray
        let! user = async {
            return! findObjectByDn connection node propertyNames
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
        do! setNodeProperties connection node properties
    }
    let deleteNode connection (DistinguishedName node) = async {
        try
            do!
                DeleteRequest(node)
                |> delete connection
                |> Async.Ignore
        with
            | :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.NoSuchObject -> ()
            | e -> failwith $"Error while deleting \"%s{node}\": {e.Message}"
    }

    let disableAccount connection userDn = async {
        let! user = findObjectByDn connection userDn [| "userAccountControl" |]
        let userAccountControl =
            SearchResultEntry.getIntAttributeValue "userAccountControl" user
        let properties = [
            ("userAccountControl", Text $"{userAccountControl ||| UserAccountControl.ACCOUNTDISABLE}")
        ]
        do! setNodeProperties connection userDn properties
    }

    let enableAccount connection userDn = async {
        let! user = findObjectByDn connection userDn [| "userAccountControl" |]
        let userAccountControl =
            SearchResultEntry.getIntAttributeValue "userAccountControl" user
        let properties = [
            ("userAccountControl", Text $"{userAccountControl &&& ~~~UserAccountControl.ACCOUNTDISABLE}")
        ]
        do! setNodeProperties connection userDn properties
    }
    let addObjectToGroup connection (DistinguishedName group) (DistinguishedName object) = async {
        let modification = directoryAttributeModification "member" DirectoryAttributeOperation.Add (Text object)
        try
            do!
                ModifyRequest(group, modification)
                |> modify connection
                |> Async.Ignore
        with
            | :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.EntryAlreadyExists -> ()
            | e -> failwith $"Error while adding \"%s{object}\" to group \"%s{group}\": {e.Message}"
    }
    let removeObjectFromGroup connection (DistinguishedName group) (DistinguishedName object) = async {
        let modification = directoryAttributeModification "member" DirectoryAttributeOperation.Delete (Text object)
        try
            do!
                ModifyRequest(group, modification)
                |> modify connection
                |> Async.Ignore
        with
            | :? DirectoryOperationException as e when e.Response.ResultCode = ResultCode.UnwillingToPerform -> ()
            | e -> failwith $"Error while removing \"%s{object}\" from group \"%s{group}\": {e.Message}"
    }

    let removeGroupMemberships connection nodeDn = async {
        let! node = findObjectByDn connection nodeDn [| "memberOf" |]
        do!
            SearchResultEntry.getStringAttributeValues "memberOf" node
            |> List.map (DistinguishedName >> fun groupDn -> async {
                do! removeObjectFromGroup connection groupDn nodeDn
            })
            |> Async.Sequential
            |> Async.Ignore
    }
