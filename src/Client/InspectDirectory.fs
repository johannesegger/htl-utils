module InspectDirectory

open Elmish
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Fulma
open Fulma.Extensions.Wikiki
open Thoth.Elmish
open Thoth.Fetch
open Thoth.Json
open Directories
open Shared.InspectDirectory

type Model =
    {
        Directory: Directory
        DirectoryInfo: DirectoryInfo option
    }

type Msg =
    | Init
    | SelectDirectory of string list
    | LoadChildDirectoriesResponse of Result<string list * string list, exn>
    | LoadDirectoryInfoResponse of Result<DirectoryInfo, exn>

let rec update authHeaderOptFn msg model =
    match msg with
    | Init ->
        update authHeaderOptFn (SelectDirectory []) model
    | SelectDirectory path ->
        let model' = { model with Directory = selectDirectory path model.Directory }
        // TODO don't load if already loaded?
        let loadChildDirectoriesCmd =
            match authHeaderOptFn with
            | Some getAuthHeader ->
                Cmd.OfPromise.either
                    (fun (path, getAuthHeader) -> promise {
                        let url = "/api/child-directories"
                        let data = (List.map Encode.string >> Encode.list) (List.rev path)
                        let! authHeader = getAuthHeader ()
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post(url, data, Decode.list Decode.string, requestProperties)
                    })
                    (path, getAuthHeader)
                    ((fun r -> path, r) >> Ok >> LoadChildDirectoriesResponse)
                    (Error >> LoadChildDirectoriesResponse)
            | None -> Cmd.none
        let loadDirectoryInfoCmd =
            match path, authHeaderOptFn with
            | x::xs, Some getAuthHeader ->
                Cmd.OfPromise.either
                    (fun (path, getAuthHeader) -> promise {
                        let url = "/api/directory-info"
                        let data = (List.map Encode.string >> Encode.list) (List.rev path)
                        let! authHeader = getAuthHeader ()
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post(url, data, DirectoryInfo.decode, requestProperties)
                    })
                    (path, getAuthHeader)
                    (Ok >> LoadDirectoryInfoResponse)
                    (Error >> LoadDirectoryInfoResponse)
            | _ -> Cmd.none
        model', Cmd.batch [ loadChildDirectoriesCmd; loadDirectoryInfoCmd ]
    | LoadChildDirectoriesResponse (Ok (path, childDirectories)) ->
        let model' = { model with Directory = setChildDirectories path childDirectories model.Directory }
        model', Cmd.none
    | LoadChildDirectoriesResponse (Error e) ->
        let cmd =
            Toast.toast "Loading directories failed" e.Message
            |> Toast.error
        model, cmd
    | LoadDirectoryInfoResponse (Ok directoryInfo) ->
        let model' = { model with DirectoryInfo = Some directoryInfo }
        model', Cmd.none
    | LoadDirectoryInfoResponse (Error e) ->
        let cmd =
            Toast.toast "Loading directory info failed" e.Message
            |> Toast.error
        model, cmd

let init authHeaderOptFn =
    let model =
        {
            Directory =
                {
                    Path = []
                    IsSelected = true
                    Children = NotLoadedDirectoryChildren
                }
            DirectoryInfo = None
        }
    update authHeaderOptFn Init model

let view model dispatch =
    let mapDirectory fn directory =
        let rec mapDirectory' fn level directory =
            match directory with
            | { Children = LoadedDirectoryChildren children } as dir when dir.IsSelected ->
                List.append
                    [ fn level directory ]
                    (List.collect (mapDirectory' fn (level + 1)) children)
            | _ -> []
        mapDirectory' fn 0 directory

    let directoryLevelItem directory =
        Button.button
            [
                Button.Color (if directory.IsSelected then IsLink else NoColor)
                Button.OnClick (fun _ev -> directory.Path |> SelectDirectory |> dispatch)
            ]
            [ str (List.tryHead directory.Path |> Option.defaultValue "<none>") ]

    let directoryView level directory =
        match directory.Children with
        | NotLoadedDirectoryChildren
        | LoadedDirectoryChildren [] -> None
        | LoadedDirectoryChildren children ->
            Container.container []
                [ Button.list [] [ yield! List.map directoryLevelItem children ] ]
            |> Some

    let rec foldDirectoryInfo fn state directoryInfo =
        let state' = fn state directoryInfo
        List.fold (foldDirectoryInfo fn) state' directoryInfo.Directories

    let directoryStatistics size directoryInfo =
        let data =
            [
                ("Directories", foldDirectoryInfo (fun sum dir -> sum + 1) 0 directoryInfo - 1)
                ("Files", foldDirectoryInfo (fun sum dir -> sum + (List.length dir.Files)) 0 directoryInfo)
            ]

        let sizeProp =
            size
            |> Option.map Tag.Size
            |> Option.toList

        Field.div [ Field.IsGrouped ]
            [
                for (key, value) in data ->
                    Control.div []
                        [
                            Tag.list [ Tag.List.HasAddons ]
                                [
                                    Tag.tag [ yield Tag.Color IsDark; yield! sizeProp ] [ str key ]
                                    Tag.tag [ yield Tag.Color IsInfo; yield! sizeProp ] [ str (sprintf "%d" value) ]
                                ]
                        ]
            ]

    let directoryInfoHeading directoryInfo =
        Level.level []
            [
                Level.left [] [ Heading.h3 [] [ str (String.concat "\\" directoryInfo.Path) ] ]
                Level.right [] [ directoryStatistics (Some IsMedium) directoryInfo ]
            ]

    let fileStatistics fileInfo =
        let dateToString (date: System.DateTime) =
            sprintf "%s %s" (date.ToString("D")) (date.ToString("T"))

        let data =
            [
                ("Size", Bytes.toHumanReadable fileInfo.Size, if fileInfo.Size = Bytes 0L then IsDanger else IsSuccess)
                ("Creation time", dateToString fileInfo.CreationTime, IsLink)
                ("Last access time", dateToString fileInfo.LastAccessTime, IsLink)
                ("Last write time", dateToString fileInfo.LastWriteTime, IsLink)
            ]
        Field.div [ Field.IsGrouped ]
            [
                for (key, value, valueColor) in data ->
                    Control.div []
                        [
                            Tag.list [ Tag.List.HasAddons ]
                                [
                                    Tag.tag [ Tag.Color IsDark ] [ str key ]
                                    Tag.tag [ Tag.Color valueColor ] [ str value ]
                                ]
                        ]
            ]

    Container.container []
        [
            match model.Directory.Children with
            | LoadedDirectoryChildren _ ->
                yield!
                    mapDirectory directoryView model.Directory
                    |> List.choose id
                    |> List.intersperse (Divider.divider [])
            | NotLoadedDirectoryChildren ->
                yield Notification.notification [ Notification.Color IsLink ]
                    [
                        Icon.icon [] [ Fa.i [ Fa.Solid.ExclamationTriangle ] [] ]
                        span [] [ str "Sign in to view directories" ]
                    ]

            match model.DirectoryInfo with
            | Some directoryInfo ->
                yield Divider.divider [ Divider.Label (sprintf "Directory info for %s" (String.concat "\\" directoryInfo.Path)) ]

                yield
                    Panel.panel []
                        [
                            yield Panel.heading [] [ directoryInfoHeading directoryInfo ]
                            for childDirectory in directoryInfo.Directories ->
                                Panel.block [ Panel.Block.Props [ Style [ JustifyContent "space-between" ] ] ]
                                    [
                                        Panel.icon [] [ Fa.i [ Fa.Solid.Folder ] [] ]
                                        str (List.last childDirectory.Path)
                                        span [ Style [ FlexGrow 1 ] ] []
                                        directoryStatistics None childDirectory
                                    ]
                            for file in directoryInfo.Files ->
                                Panel.block [ Panel.Block.Props [ Style [ JustifyContent "space-between" ] ] ]
                                    [
                                        Panel.icon [] [ Fa.i [ Fa.Solid.File ] [] ]
                                        str (List.last file.Path)
                                        span [ Style [ FlexGrow 1 ] ] []
                                        fileStatistics file
                                    ]
                        ]
            | None -> ()
        ]