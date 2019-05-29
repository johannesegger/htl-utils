module InspectDirectory

open Elmish
open Elmish.Streams
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open FSharp.Control
open Fulma
open Fulma.Extensions.Wikiki
open Thoth.Elmish
open Thoth.Fetch
open Thoth.Json
open Directories
open Shared.InspectDirectory

[<Fable.Core.StringEnum>]
type FilterId =
    | All
    | IsEmpty
    | HasEmptyFiles

type DirectoryFilter = FilterId * string

let private directoryFilters =
    [
        All, "All"
        IsEmpty, "Empty directories"
        HasEmptyFiles, "Directories with empty files"
    ]

type Model =
    {
        Directory: Directory
        DirectoryInfo: DirectoryInfo option
        ActiveDirectoryFilter: FilterId
        AutoRefreshEnabled: bool
        AutoRefreshInterval: System.TimeSpan
    }

type Msg =
    | SelectDirectory of string list
    | LoadChildDirectoriesResponse of Result<string list * string list, string list * exn>
    | LoadDirectoryInfoResponse of Result<DirectoryInfo, exn>
    | ApplyFilter of FilterId
    | ToggleAutoRefresh
    | SetAutoRefreshInterval of System.TimeSpan

let rec update msg model =
    match msg with
    | SelectDirectory path ->
        { model with Directory = selectDirectory path model.Directory }
    | LoadChildDirectoriesResponse (Ok (path, childDirectories)) ->
        { model with Directory = setChildDirectories path childDirectories model.Directory }
    | LoadChildDirectoriesResponse (Error (path, e)) ->
        { model with Directory = setChildDirectoriesFailedToLoad path model.Directory }
    | LoadDirectoryInfoResponse (Ok directoryInfo) ->
        { model with DirectoryInfo = Some directoryInfo }
    | LoadDirectoryInfoResponse (Error e) ->
        model // TODO set to error
    | ApplyFilter filterId ->
        { model with ActiveDirectoryFilter = filterId }
    | ToggleAutoRefresh ->
        { model with AutoRefreshEnabled = not model.AutoRefreshEnabled }
    | SetAutoRefreshInterval interval ->
        { model with AutoRefreshInterval = interval }

let init =
    {
        Directory =
            {
                Path = []
                IsSelected = true
                Children = NotLoadedDirectoryChildren
            }
        DirectoryInfo = None
        ActiveDirectoryFilter = directoryFilters |> List.head |> fst
        AutoRefreshEnabled = false
        AutoRefreshInterval = System.TimeSpan.FromSeconds 5.
    }

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
        | NotLoadedDirectoryChildren // TODO show progress bar
        | FailedToLoadDirectoryChildren // TODO show error and retry button
        | LoadedDirectoryChildren [] -> None
        | LoadedDirectoryChildren children ->
            Container.container []
                [ Button.list [] [ yield! List.map directoryLevelItem children ] ]
            |> Some

    let directoryStatistics size directoryInfo =
        let data =
            [
                ("Directories", DirectoryInfo.fold (fun sum dir -> sum + 1) 0 directoryInfo - 1)
                ("Files", DirectoryInfo.fold (fun sum dir -> sum + (List.length dir.Files)) 0 directoryInfo)
            ]

        let sizeProp =
            size
            |> Option.map Tag.Size
            |> Option.toList

        Field.div [ Field.IsGrouped ]
            [
                for (key, value) in data ->
                    let color = match value with | 0 -> IsDanger | 1 -> IsWarning | _ -> IsSuccess
                    Control.div []
                        [
                            Tag.list [ Tag.List.HasAddons ]
                                [
                                    Tag.tag [ yield Tag.Color IsDark; yield! sizeProp ] [ str key ]
                                    Tag.tag [ yield Tag.Color color; yield! sizeProp ] [ str (sprintf "%d" value) ]
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

    let applyFilter directory =
        match model.ActiveDirectoryFilter with
        | All -> directory
        | IsEmpty ->
            let childDirectories = List.filter (fun d -> List.isEmpty d.Directories && List.isEmpty d.Files) directory.Directories
            { directory with Directories = childDirectories; Files = [] }
        | HasEmptyFiles ->
            let childDirectories =
                directory.Directories
                |> List.filter (fun d ->
                    (false, d)
                    ||> DirectoryInfo.fold (fun hasEmpty d -> hasEmpty || List.exists (fun f -> f.Size = Bytes 0L) d.Files)
                )
            { directory with Directories = childDirectories; Files = [] }

    Container.container []
        [
            match model.Directory.Children with
            | LoadedDirectoryChildren _ ->
                yield!
                    mapDirectory directoryView model.Directory
                    |> List.choose id
                    |> List.intersperse (Divider.divider [])
            | NotLoadedDirectoryChildren ->
                yield Notification.notification [ Notification.Color IsWarning ]
                    [
                        Icon.icon [] [ Fa.i [ Fa.Solid.ExclamationTriangle ] [] ]
                        span [] [ str "Sign in to view directories" ]
                    ]
            | FailedToLoadDirectoryChildren ->
                yield
                    Notification.notification [ Notification.Color IsDanger ]
                        [
                            Level.level []
                                [
                                    Level.left []
                                        [
                                            Level.item []
                                                [
                                                    Icon.icon [] [ Fa.i [ Fa.Solid.ExclamationTriangle ] [] ]
                                                    span [] [ str "Error while loading directory children" ]
                                                ]
                                            Level.item []
                                                [
                                                    Button.button
                                                        [
                                                            Button.Color IsSuccess
                                                            Button.OnClick (fun _ev -> dispatch (SelectDirectory model.Directory.Path))
                                                        ]
                                                        [
                                                            Icon.icon [] [ Fa.i [ Fa.Solid.Sync ] [] ]
                                                            span [] [ str "Retry" ]
                                                        ]
                                                ]
                                        ]
                                ]
                        ]

            match Option.map applyFilter model.DirectoryInfo with
            | Some directoryInfo ->
                yield Divider.divider [ Divider.Label (sprintf "Directory info for %s" (String.concat "\\" directoryInfo.Path)) ]

                yield
                    Panel.panel []
                        [
                            yield Panel.heading [] [ directoryInfoHeading directoryInfo ]
                            yield Panel.tabs []
                                [
                                    for (filterId, filterName) in directoryFilters ->
                                        Panel.tab
                                            [
                                                Panel.Tab.IsActive (filterId = model.ActiveDirectoryFilter)
                                                Panel.Tab.Props [ OnClick (fun _ev -> dispatch (ApplyFilter filterId)) ]
                                            ]
                                            [ str filterName ]
                                ]
                            yield Panel.block []
                                [
                                    Field.div [ Field.IsGrouped ]
                                        [
                                            yield Control.div []
                                                [
                                                    Checkbox.checkbox []
                                                        [
                                                            Checkbox.input
                                                                [
                                                                    Props
                                                                        [
                                                                            Checked model.AutoRefreshEnabled
                                                                            OnChange (fun _ev -> dispatch ToggleAutoRefresh)
                                                                        ]
                                                                ]
                                                            str "Auto-refresh"
                                                        ]
                                                ]
                                            if model.AutoRefreshEnabled then
                                                yield
                                                    Control.div []
                                                        [
                                                            let refreshIntervals =
                                                                [
                                                                    "1 second", System.TimeSpan.FromSeconds 1.
                                                                    "5 seconds", System.TimeSpan.FromSeconds 5.
                                                                    "10 seconds", System.TimeSpan.FromSeconds 10.
                                                                    "30 seconds", System.TimeSpan.FromSeconds 30.
                                                                    "1 minute", System.TimeSpan.FromMinutes 1.
                                                                ]
                                                            for (name, interval) in refreshIntervals ->
                                                                Radio.radio [ ]
                                                                    [
                                                                        Radio.input
                                                                            [
                                                                                Radio.Input.Name "auto-refresh-interval"
                                                                                Radio.Input.Props
                                                                                    [
                                                                                        Style [ MarginRight "0.5em" ]
                                                                                        Checked (interval = model.AutoRefreshInterval)
                                                                                        OnChange (fun _ev -> dispatch (SetAutoRefreshInterval interval))
                                                                                    ]
                                                                            ]
                                                                        str name
                                                                    ]
                                                        ]
                                        ]
                                ]
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

let stream authHeader states msgs =
    authHeader
    |> AsyncRx.choose id
    |> AsyncRx.flatMapLatest (fun authHeader ->
        [
            yield msgs

            let loadRootDirectoriesResponseToast response =
                match response with
                | Ok _ -> Cmd.none
                | Error (_, e: exn) ->
                    Toast.toast "Loading root directories failed" e.Message
                    |> Toast.error
            yield
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        let url = "/api/child-directories"
                        let data = Encode.list []
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post(url, data, Decode.list Decode.string, requestProperties)
                    })
                    |> AsyncRx.map (fun children -> Ok ([], children))
                    |> AsyncRx.catch ((fun e -> [], e) >> Error >> AsyncRx.single)
                )
                |> AsyncRx.showToast loadRootDirectoriesResponseToast
                |> AsyncRx.map LoadChildDirectoriesResponse

            let loadChildDirectories path =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        let url = "/api/child-directories"
                        let data = List.rev path |> List.map Encode.string |> Encode.list
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post(url, data, Decode.list Decode.string, requestProperties)
                    })
                    |> AsyncRx.map (fun children -> Ok (path, children))
                    |> AsyncRx.catch ((fun e -> path, e) >> Error >> AsyncRx.single)
                )
            let loadChildDirectoriesResponseToast response =
                match response with
                | Ok _ -> Cmd.none
                | Error (path, e: exn) ->
                    Toast.toast "Loading child directories failed" e.Message
                    |> Toast.error
            yield
                msgs
                |> AsyncRx.choose (function
                    | SelectDirectory path -> Some (loadChildDirectories path)
                    | _ -> None
                )
                |> AsyncRx.switchLatest
                |> AsyncRx.showToast loadChildDirectoriesResponseToast
                |> AsyncRx.map LoadChildDirectoriesResponse

            let loadDirectoryInfo path =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        let url = "/api/directory-info"
                        let data = (List.map Encode.string >> Encode.list) (List.rev path)
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post(url, data, DirectoryInfo.decode, requestProperties)
                    })
                    |> AsyncRx.map Ok
                    |> AsyncRx.catch (Error >> AsyncRx.single)
                )
            let loadDirectoryInfoResponseToast response =
                match response with
                | Ok _ -> Cmd.none
                | Error (e: exn) ->
                    Toast.toast "Loading directory info failed" e.Message
                    |> Toast.error
            let autoRefresh =
                states
                |> AsyncRx.map (fun state ->
                    match getSelectedDirectory state.Directory, state.AutoRefreshEnabled with
                    | Some dir, true -> Some (dir.Path, int state.AutoRefreshInterval.TotalMilliseconds)
                    | _ -> None
                )
                |> AsyncRx.distinctUntilChanged
                |> AsyncRx.flatMapLatest (function
                    | Some (path, interval) ->
                        AsyncRx.interval 0 interval
                        |> AsyncRx.map (fun _ -> SelectDirectory path)
                    | None -> AsyncRx.never ()
                )
            yield
                msgs
                |> AsyncRx.merge autoRefresh
                |> AsyncRx.choose (function
                    | SelectDirectory path -> Some (loadDirectoryInfo path)
                    | _ -> None
                )
                |> AsyncRx.switchLatest
                |> AsyncRx.showToast loadDirectoryInfoResponseToast
                |> AsyncRx.map LoadDirectoryInfoResponse
        ]
        |> AsyncRx.mergeSeq
    )
