open AAD
open Sokrates

let phoneNumbers =
    [
        "EINA", "0660 123 45 67"
    ]
    |> Map.ofList

[<EntryPoint>]
let main args =
    async {
        match args with
        | [| userName |] ->
            let aadConfig = AAD.Configuration.Config.fromEnvironment ()
            use graphServiceClient = GraphServiceClientFactory.createWithAppSecret aadConfig.OidcConfig
            let! aadUsers = AAD.Core.getUsers graphServiceClient

            let sokratesApi = SokratesApi.FromEnvironment()
            let! sokratesTeachers = sokratesApi.FetchTeachers

            let photoLibraryConfig = PhotoLibrary.Configuration.Config.fromEnvironment ()
            let teacherPhotos = PhotoLibrary.Core.getTeacherPhotos (Some 200, Some 200) |> Reader.run photoLibraryConfig

            let contacts =
                let aadUserMap =
                    aadUsers
                    |> List.map (fun user -> CIString user.UserName, user)
                    |> Map.ofList
                let photoLibraryTeacherMap =
                    teacherPhotos
                    |> List.map (fun photo -> CIString photo.PersonId, photo.Data)
                    |> Map.ofList
                sokratesTeachers
                |> List.choose (fun sokratesTeacher ->
                    let aadUser = Map.tryFind (CIString sokratesTeacher.ShortName) aadUserMap
                    let photo = Map.tryFind (CIString sokratesTeacher.ShortName) photoLibraryTeacherMap
                    match aadUser with
                    | Some aadUser ->
                        Some {
                            AAD.Domain.Contact.FirstName = sokratesTeacher.FirstName
                            AAD.Domain.Contact.LastName = sokratesTeacher.LastName
                            AAD.Domain.Contact.DisplayName =
                                sprintf "%s %s (%s)" sokratesTeacher.LastName sokratesTeacher.FirstName sokratesTeacher.ShortName
                            AAD.Domain.Contact.Birthday = Some sokratesTeacher.DateOfBirth
                            AAD.Domain.Contact.HomePhones =
                                sokratesTeacher.Phones
                                |> List.choose (function
                                    | Sokrates.Home number -> Some number
                                    | Sokrates.Mobile _ -> None
                                )
                            AAD.Domain.Contact.MobilePhone =
                                sokratesTeacher.Phones
                                |> List.tryPick (function
                                    | Sokrates.Home _ -> None
                                    | Sokrates.Mobile number -> Some number
                                )
                                |> Option.defaultValue (phoneNumbers |> Map.find sokratesTeacher.ShortName)
                                |> Some
                            AAD.Domain.Contact.MailAddresses = List.take 1 aadUser.MailAddresses
                            AAD.Domain.Contact.Photo =
                                photo
                                |> Option.map (fun (PhotoLibrary.Domain.Base64EncodedJpgImage data) ->
                                    AAD.Domain.Base64EncodedImage data
                                )
                        }
                    | None -> None
                )

            do! AAD.Core.updateAutoContacts graphServiceClient (AAD.Domain.UserId userName) contacts
            return 0
        | _ ->
            eprintfn "Usage: dotnet run -- <user-principal-name>"
            return 1
    } |> Async.RunSynchronously
