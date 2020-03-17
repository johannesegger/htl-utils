module InspectDirectory

open Fable.Core
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Fable.Reaction
open Fetch.Types
open FSharp.Control
open Fulma
open Fulma.Extensions.Wikiki
open System
open Thoth.Fetch
open Thoth.Json
open Shared.InspectDirectory

type FileInfo = {
    Path: StoragePath
    Size: Bytes
    CreationTime: DateTime
    LastAccessTime: DateTime
    LastWriteTime: DateTime
}
module FileInfo =
    let rec fromDto (v: Shared.InspectDirectory.FileInfo) = {
        Path =
            v.Path.Split('\\', '/')
            |> List.ofArray
            |> StoragePath.combine StoragePath.empty
        Size = v.Size
        CreationTime = v.CreationTime
        LastAccessTime = v.LastAccessTime
        LastWriteTime = v.LastWriteTime
    }

type DirectoryInfo = {
    Path: StoragePath
    Directories: DirectoryInfo list
    Files: FileInfo list
}
module DirectoryInfo =
    let rec fold fn state directoryInfo =
        let state' = fn state directoryInfo
        List.fold (fold fn) state' directoryInfo.Directories
    let rec fromDto (v: Shared.InspectDirectory.DirectoryInfo) = {
        Path =
            v.Path.Split('\\', '/')
            |> List.ofArray
            |> StoragePath.combine StoragePath.empty
        Directories = v.Directories |> List.map fromDto
        Files = List.map FileInfo.fromDto v.Files
    }

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

type DirectoryInfoState =
    | DirectoryInfoNotAvailable
    | DirectoryInfoLoading
    | DirectoryInfoLoaded of DirectoryInfo
    | DirectoryInfoLoadError of exn

type Model =
    {
        Directory: Directory
        DirectoryInfo: DirectoryInfoState
        DirectoriesWithVisibleDetails: Set<StoragePath>
        ActiveDirectoryFilter: FilterId
        AutoRefreshEnabled: bool
        AutoRefreshInterval: System.TimeSpan
    }

type Msg =
    | SelectDirectory of StoragePath
    | LoadChildDirectoriesResponse of Result<StoragePath * string list, StoragePath * exn>
    | LoadDirectoryInfoResponse of Result<DirectoryInfo, exn>
    | ApplyFilter of FilterId
    | ToggleAutoRefresh
    | SetAutoRefreshInterval of System.TimeSpan
    | ToggleDetailsVisibility of StoragePath

let rec update msg model =
    match msg with
    | SelectDirectory path ->
        { model with
            Directory = Directory.select path model.Directory |> Directory.setLoading path true
            DirectoryInfo = DirectoryInfoLoading
            DirectoriesWithVisibleDetails = Set.empty }
    | LoadChildDirectoriesResponse (Ok (path, childDirectories)) ->
        { model with Directory = Directory.setChildDirectories path childDirectories model.Directory }
    | LoadChildDirectoriesResponse (Error (path, e)) ->
        { model with Directory = Directory.setChildDirectoriesFailedToLoad path model.Directory }
    | LoadDirectoryInfoResponse (Ok directoryInfo) ->
        { model with DirectoryInfo = DirectoryInfoLoaded directoryInfo }
    | LoadDirectoryInfoResponse (Error e) ->
        { model with DirectoryInfo = DirectoryInfoLoadError e }
    | ApplyFilter filterId ->
        { model with ActiveDirectoryFilter = filterId }
    | ToggleAutoRefresh ->
        { model with AutoRefreshEnabled = not model.AutoRefreshEnabled }
    | SetAutoRefreshInterval interval ->
        { model with AutoRefreshInterval = interval }
    | ToggleDetailsVisibility path ->
        { model with
            DirectoriesWithVisibleDetails =
                if Set.contains path model.DirectoriesWithVisibleDetails then
                    Set.remove path model.DirectoriesWithVisibleDetails
                else Set.add path model.DirectoriesWithVisibleDetails
        }

let init =
    {
        Directory = Directory.root
        DirectoryInfo = DirectoryInfoNotAvailable
        DirectoriesWithVisibleDetails = Set.empty
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
                Button.IsLoading directory.IsLoading
            ]
            [ str (StoragePath.getName directory.Path) ]

    let directoryView level directory =
        match directory.Children with
        | NotLoadedDirectoryChildren ->
            Progress.progress [ Progress.Color IsInfo ] []
            |> Some
        | FailedToLoadDirectoryChildren ->
            Views.errorWithRetryButton "Error while loading directory children" (fun () -> dispatch (SelectDirectory directory.Path))
            |> Some
        | LoadedDirectoryChildren [] -> None
        | LoadedDirectoryChildren children ->
            Container.container [] [
                Button.list [] [ yield! List.map directoryLevelItem children ]
            ]
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
                Level.left [] [ Heading.h3 [] [ str (StoragePath.toString directoryInfo.Path) ] ]
                Level.right [] [ Field.div [ Field.IsGrouped ] (directoryStatistics (Some IsMedium) directoryInfo) ]
            ]

    let fileStatistics fileInfo =
        let dateToString (date: System.DateTime) =
            sprintf "%s %s" (date.ToString("D")) (date.ToString("T"))

        let data =
            [
                ("Size", Bytes.toHumanReadable fileInfo.Size, if fileInfo.Size = Bytes 0L then IsDanger else IsSuccess)
                // ("Creation time", dateToString fileInfo.CreationTime, IsLink)
                // ("Last access time", dateToString fileInfo.LastAccessTime, IsLink)
                // ("Last write time", dateToString fileInfo.LastWriteTime, IsLink)
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
                yield Progress.progress [ Progress.Color IsInfo ] []
            | FailedToLoadDirectoryChildren ->
                yield Views.errorWithRetryButton "Error while loading directory children" (fun () -> dispatch (SelectDirectory model.Directory.Path))

            match model.DirectoryInfo with
            | DirectoryInfoNotAvailable -> ()
            | DirectoryInfoLoading ->
                yield Section.section [] [ Progress.progress [ Progress.Color IsInfo ] [] ]
            | DirectoryInfoLoaded directoryInfo ->
                let directoryInfo = applyFilter directoryInfo
                yield Divider.divider [ Divider.Label (sprintf "Directory info for %s" (StoragePath.toString directoryInfo.Path)) ]

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
                            for childDirectory in directoryInfo.Directories do
                                let showDetails = Set.contains childDirectory.Path model.DirectoriesWithVisibleDetails
                                yield
                                    Panel.block [ Panel.Block.Props [ Style [ JustifyContent "space-between" ] ] ]
                                        [
                                            Panel.icon [] [ Fa.i [ Fa.Solid.Folder ] [] ]
                                            str (StoragePath.getName childDirectory.Path)
                                            span [ Style [ FlexGrow 1 ] ] []
                                            Field.div [ Field.IsGrouped ]
                                                [
                                                    yield! directoryStatistics None childDirectory
                                                    yield Control.div []
                                                        [
                                                            Button.a
                                                                [
                                                                    Button.Color IsLink
                                                                    Button.Size IsSmall
                                                                    Button.OnClick (fun _ev -> dispatch (ToggleDetailsVisibility childDirectory.Path))
                                                                ]
                                                                [
                                                                    Icon.icon [] [ Fa.i [ (if showDetails then Fa.Solid.AngleDown else Fa.Solid.AngleRight) ] [] ]
                                                                    span [] [ str "Details" ]
                                                                ]
                                                        ]
                                                ]
                                        ]
                                if showDetails then
                                    yield
                                        Panel.block []
                                            [
                                                Panel.panel [ Props [ Style [ FlexGrow "1" ] ] ]
                                                    [
                                                        let relativeName path =
                                                            path
                                                            |> StoragePath.skip childDirectory.Path
                                                            |> StoragePath.toString
                                                        let fileView (file: FileInfo) =
                                                            Panel.block [ Panel.Block.Props [ Style [ JustifyContent "space-between" ] ] ]
                                                                [
                                                                    Panel.icon [] [ Fa.i [ Fa.Solid.File ] [] ]
                                                                    str (relativeName file.Path)
                                                                    span [ Style [ FlexGrow 1 ] ] []
                                                                    fileStatistics file
                                                                ]
                                                        let rec subDirectoryView dir =
                                                            [
                                                                yield
                                                                    Panel.block [ Panel.Block.Props [ Style [ JustifyContent "space-between" ] ] ]
                                                                        [
                                                                            Panel.icon [] [ Fa.i [ Fa.Solid.Folder ] [] ]
                                                                            str (relativeName dir.Path)
                                                                            span [ Style [ FlexGrow 1 ] ] []
                                                                            Field.div [ Field.IsGrouped ] (directoryStatistics None dir)
                                                                        ]
                                                                yield! List.map fileView dir.Files
                                                                yield! List.collect subDirectoryView dir.Directories
                                                            ]
                                                        yield! List.map fileView childDirectory.Files
                                                        yield! List.collect subDirectoryView childDirectory.Directories

                                                    ]
                                            ]
                            for file in directoryInfo.Files ->
                                Panel.block [ Panel.Block.Props [ Style [ JustifyContent "space-between" ] ] ]
                                    [
                                        Panel.icon [] [ Fa.i [ Fa.Solid.File ] [] ]
                                        str (StoragePath.getName file.Path)
                                        span [ Style [ FlexGrow 1 ] ] []
                                        fileStatistics file
                                    ]
                        ]
            | DirectoryInfoLoadError e ->
                yield Views.errorWithRetryButton "Error while loading directory info" (fun () -> dispatch (SelectDirectory model.Directory.Path))
        ]

let stream (getAuthRequestHeader, (pageActive: IAsyncObservable<bool>)) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    pageActive
    |> AsyncRx.flatMapLatest (function
        | true ->
            [
                msgs

                AsyncRx.defer (fun () ->
                    AsyncRx.ofAsync' (async {
                        let url = "/api/child-directories"
                        let data = StoragePath.toString StoragePath.empty
                        let! authHeader = getAuthRequestHeader ()
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post(url, data, Decode.list Decode.string, requestProperties) |> Async.AwaitPromise
                    })
                    |> AsyncRx.map (fun children -> Ok (StoragePath.empty, children))
                    |> AsyncRx.catch ((fun e -> StoragePath.empty, e) >> Error >> AsyncRx.single)
                )
                |> AsyncRx.showSimpleErrorToast (snd >> fun e -> "Loading root directories failed", e.Message)
                |> AsyncRx.map LoadChildDirectoriesResponse

                let loadChildDirectories path =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync' (async {
                            let url = "/api/child-directories"
                            let data = StoragePath.toString path
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            return! Fetch.post(url, data, Decode.list Decode.string, requestProperties) |> Async.AwaitPromise
                        })
                        |> AsyncRx.map (fun children -> Ok (path, children))
                        |> AsyncRx.catch ((fun e -> path, e) >> Error >> AsyncRx.single)
                    )
                msgs
                |> AsyncRx.choose (function
                    | SelectDirectory path -> Some (loadChildDirectories path)
                    | _ -> None
                )
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (snd >> fun e -> "Loading child directories failed", e.Message)
                |> AsyncRx.map LoadChildDirectoriesResponse

                let loadDirectoryInfo path =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync' (async {
                            let url = "/api/directory-info"
                            let data = StoragePath.toString path
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            return! Fetch.post(url, data, DirectoryInfo.decoder, requestProperties) |> Async.AwaitPromise
                        })
                        |> AsyncRx.map (DirectoryInfo.fromDto >> Ok)
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )
                let autoRefresh =
                    states
                    |> AsyncRx.map (snd >> fun state ->
                        match Directory.getSelectedDirectory state.Directory, state.AutoRefreshEnabled with
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
                msgs
                |> AsyncRx.merge autoRefresh
                |> AsyncRx.choose (function
                    | SelectDirectory path -> Some (loadDirectoryInfo path)
                    | _ -> None
                )
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Loading directory info failed", e.Message)
                |> AsyncRx.map LoadDirectoryInfoResponse
            ]
            |> AsyncRx.mergeSeq
        | false ->
            AsyncRx.empty ()
    )
