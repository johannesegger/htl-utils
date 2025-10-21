[<AutoOpen>]
module MicrosoftGraphExtensions

open Azure.Core
open Azure.Identity
open Domain
open Microsoft.Graph.Beta
open Microsoft.Kiota.Abstractions.Serialization
open System
open System.IO
open System.Threading.Tasks

module GraphServiceClientFactory =
    let create (config: AAD.Configuration.OidcConfig) scopes =
        // let tokenCredential = ClientSecretCredential(
        //     config.TenantId,
        //     config.AppId,
        //     config.AppSecret
        // )
        let tokenCredential =
            let opts = DeviceCodeCredentialOptions (
                ClientId = config.AppId,
                TenantId = config.TenantId,
                TokenCachePersistenceOptions = TokenCachePersistenceOptions(Name = "HtlUtils.UpdateGrpEltern"),
                DeviceCodeCallback = (fun code ct ->
                    Console.ForegroundColor <- ConsoleColor.Yellow
                    printfn $"%s{code.Message}"
                    Console.ResetColor()
                    Task.CompletedTask
                )
            )
            let authTokenPath = Path.Combine(Path.GetTempPath(), $"%s{opts.TokenCachePersistenceOptions.Name}.token")
            if File.Exists(authTokenPath) then
                use fileStream = File.OpenRead(authTokenPath)
                opts.AuthenticationRecord <- AuthenticationRecord.DeserializeAsync(fileStream) |> Async.AwaitTask |> Async.RunSynchronously
                DeviceCodeCredential(opts)
            else
                let deviceCodeCredential = DeviceCodeCredential(opts)
                let authenticationRecord = deviceCodeCredential.AuthenticateAsync(TokenRequestContext(scopes)) |> Async.AwaitTask |> Async.RunSynchronously
                use fileStream = File.OpenWrite(authTokenPath)
                authenticationRecord.SerializeAsync(fileStream) |> Async.AwaitTask |> Async.RunSynchronously
                deviceCodeCredential
        new GraphServiceClient(tokenCredential, scopes)

module GraphServiceClient =
    let formatError errorTitle wf = async {
        try
            return! wf
        with
            | :? AggregateException as e ->
                match e.InnerException with
                | :? Models.ODataErrors.ODataError as e ->
                    return failwith $"%s{errorTitle}: %s{e.Error.Message}"
                | _ -> return raise e.InnerException
            | e -> return raise e
    }

[<AutoOpen>]
module TypeExtensions =
    type GraphServiceClient with
        member this.ReadAll<'a, 'b when 'a: (new: unit -> 'a) and 'a :> IParsable and 'a :> IAdditionalDataHolder> (query: Task<'a>) = async {
            let result = Collections.Generic.List<_>()
            let! firstResponse = query |> Async.AwaitTask
            let iterator =
                Microsoft.Graph.PageIterator<'b, 'a>
                    .CreatePageIterator(
                        this,
                        firstResponse,
                        (fun item ->
                            result.Add(item)
                            true // continue iteration
                        )
                    )
            do! iterator.IterateAsync() |> Async.AwaitTask
            return result
        }

        member this.GetDirectoryObjectReference (obj: Models.Group) =
            Models.ReferenceCreate(OdataId = $"{this.RequestAdapter.BaseUrl}/groups/{obj.Id}")

        member this.GetDirectoryObjectReference (obj: Models.User) =
            Models.ReferenceCreate(OdataId = $"{this.RequestAdapter.BaseUrl}/users/{obj.Id}")

        member this.GetParentGroups() = async {
            let parseGroupMember (user: Models.User) =
                let userType =
                    if user.UserType = "Member" then MemberUser
                    elif user.UserType = "Guest" then GuestUser
                    else failwith $"User %s{user.DisplayName} has unknown user type: %s{user.UserType}"
                { UserId = user.Id; MailAddress = user.Mail; UserType = userType }

            let! aadParentGroups =
                this.Groups.GetAsync(fun v -> v.QueryParameters.Filter <- "startsWith(displayName, 'GrpEltern')")
                |> this.ReadAll<_, Models.Group>
                |> GraphServiceClient.formatError "Error while retrieving parent groups"
            let! aadParentGroupsWithMembers =
                aadParentGroups
                |> Seq.filter (fun v -> v.DisplayName <> "GrpEltern")
                |> Seq.map (fun group -> async {
                    let! groupMembers =
                        this.Groups.[group.Id].Members.GraphUser.GetAsync(fun v ->
                            v.QueryParameters.Select <- [| "displayName"; "mail"; "userType"|]
                        )
                        |> this.ReadAll<_, Models.User>
                    let members = groupMembers |> Seq.map parseGroupMember |> Seq.toList
                    return { GroupId = group.Id; Name = group.DisplayName; Members = members }
                })
                |> Async.Parallel
            return aadParentGroupsWithMembers |> List.ofArray
        }

        member this.CreateParentUser (mailAddress: string) = async {
            let invitation =
                Models.Invitation(
                    InvitedUserEmailAddress = mailAddress,
                    InviteRedirectUrl = "https://htlvb.at",
                    InvitedUserType = "Guest",
                    SendInvitationMessage = false
                )
            return!
                this.Invitations.PostAsync(invitation) |> Async.AwaitTask
                |> GraphServiceClient.formatError $"Error while adding %s{mailAddress}"
        }

        member this.CreateParentGroup (groupName: string) = async {
            // TODO this might be inefficient if we create multiple parent groups
            let! teachersGroup = async {
                let! groups =
                    this.Groups.GetAsync(fun v -> v.QueryParameters.Filter <- "displayName eq 'GrpLehrer'")
                    |> this.ReadAll<_, Models.Group>
                return groups |> Seq.exactlyOne
            }
            // let! adminUser = async {
            //     let! users =
            //         this.Users.GetAsync(fun v -> v.QueryParameters.Filter <- "userPrincipalName eq 'admin@htlvb.at'")
            //         |> this.ReadAll<_, Models.User>
            //     return users |> Seq.exactlyOne
            // }

            let group = Models.Group(
                DisplayName = groupName,
                GroupTypes = Collections.Generic.List<_>([ "Unified" ]),
                MailEnabled = true,
                MailNickname = groupName,
                ResourceBehaviorOptions = Collections.Generic.List [ "SubscribeNewGroupMembers"; "WelcomeEmailDisabled" ],
                SecurityEnabled = false,
                Visibility = "HiddenMembership"
            )
            let patch = Models.Group(
                // AcceptedSenders = Collections.Generic.List<_>([ teachersGroup :> Models.DirectoryObject ]),
                AccessType = Models.GroupAccessType.Private
            )
            let! aadGroup =
                this.Groups.PostAsync(group) |> Async.AwaitTask
                |> GraphServiceClient.formatError $"Error while adding %s{groupName}"
            // let! aadGroup = this.Groups.["b958388f-0b84-42e8-abad-fd59d58aeefe"].GetAsync() |> Async.AwaitTask
            // let! aadGroup = Async.retryIfError (async {
            //     return! this.Groups.[aadGroup.Id].GetAsync() |> Async.AwaitTask
            // })
            do! Async.retryIfErrorWithTimeout (TimeSpan.FromSeconds 5.) (async {
                do!
                    this.Groups.[aadGroup.Id].PatchAsync(patch) |> Async.AwaitTask |> Async.Ignore
                    |> GraphServiceClient.formatError $"Error while patching %s{groupName}"
            })
            // do! this.Groups.[aadGroup.Id].Owners.[aadGroup.Owners.[0].Id].Ref.DeleteAsync() |> Async.AwaitTask |> GraphServiceClient.formatError "Error while removing default owner"
            // do! this.Groups.[aadGroup.Id].Owners.Ref.PostAsync(adminUser.GetDirectoryObjectReference()) |> Async.AwaitTask |> GraphServiceClient.formatError "Error while adding group owner"
            do! Async.retryIfErrorWithTimeout (TimeSpan.FromSeconds 5.) (async {
                do!
                    this.Groups.[aadGroup.Id].AcceptedSenders.Ref.PostAsync(this.GetDirectoryObjectReference(teachersGroup)) |> Async.AwaitTask
                    |> GraphServiceClient.formatError $"Error while adding accepted senders to %s{groupName}"
            })
            return aadGroup
        }
