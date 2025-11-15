[<AutoOpen>]
module MicrosoftGraphExtensions

open AAD
open Domain
open Microsoft.Graph.Beta
open System

[<AutoOpen>]
module TypeExtensions =
    type GraphServiceClient with
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