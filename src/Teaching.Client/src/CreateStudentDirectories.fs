module CreateStudentDirectories

open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Fable.Reaction
open Fetch.Types
open FSharp.Control
open Fulma
open Fulma.Extensions.Wikiki
open Shared.CreateStudentDirectories
open Thoth.Fetch
open Thoth.Json

type Model =
    {
        IsEnabled: bool
        ClassList: LoadableClassList
        SelectedClass: string option
        Directory: Directory
        NewDirectoryNames: Map<StoragePath, string>
    }

type Msg =
    | Enable
    | Disable
    | LoadClassList
    | LoadClassListResponse of Result<string list, exn>
    | SelectClass of string
    | SelectDirectory of StoragePath
    | LoadChildDirectoriesResponse of Result<StoragePath * string list, StoragePath * exn>
    | UpdateNewDirectoryValue of StoragePath * string
    | AddChildDirectory of string * StoragePath
    | CreateDirectories of CreateDirectoriesData
    | CreateDirectoriesResponse of Result<unit, exn>

let rec update msg model =
    match msg with
    | Enable ->
        { model with IsEnabled = true }
    | Disable ->
        { model with IsEnabled = false }
    | LoadClassList ->
        { model with ClassList = NotLoadedClassList }
    | LoadClassListResponse (Ok classList) ->
        { model with ClassList = LoadedClassList (Classes.groupAndSort classList) }
    | LoadClassListResponse (Error e) ->
        { model with ClassList = FailedToLoadClassList }
    | SelectClass name ->
        { model with SelectedClass = Some name }
    | SelectDirectory path ->
        { model with Directory = Directory.select path model.Directory |> Directory.setLoading path true }
    | LoadChildDirectoriesResponse (Ok (path, childDirectories)) ->
        { model with Directory = Directory.setChildDirectories path childDirectories model.Directory |> Directory.setLoading path false }
    | LoadChildDirectoriesResponse (Error (path, e)) ->
        { model with Directory = Directory.setChildDirectoriesFailedToLoad path model.Directory |> Directory.setLoading path false }
    | UpdateNewDirectoryValue (path, value) ->
        { model with NewDirectoryNames = Map.add path value model.NewDirectoryNames }
    | AddChildDirectory (name, path) ->
        { model with
            Directory = Directory.addChildDirectory path name model.Directory
            NewDirectoryNames = Map.remove path model.NewDirectoryNames }
    | CreateDirectories data -> model
    | CreateDirectoriesResponse (Ok ()) -> model
    | CreateDirectoriesResponse (Error e) -> model

let init =
    {
        IsEnabled = false
        ClassList = NotLoadedClassList
        SelectedClass = None
        Directory = Directory.root
        NewDirectoryNames = Map.empty
    }

let view model dispatch =
    let classListView =
        match model.ClassList with
        | NotLoadedClassList ->
            Progress.progress [ Progress.Color IsInfo ] []
        | FailedToLoadClassList ->
            Views.errorWithRetryButton "Error while loading class list" (fun () -> dispatch LoadClassList)
        | LoadedClassList classList ->
            Container.container [] [
                for group in classList ->
                    Button.list []
                        [
                            for name in group ->
                                let color =
                                    match model.SelectedClass with
                                    | Some n when n = name -> IsLink
                                    | _ -> NoColor
                                Button.button
                                    [
                                        Button.OnClick (fun _ev -> dispatch (SelectClass name))
                                        Button.Color color
                                    ]
                                    [ str name ]
                        ]
                ]

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
        | FailedToLoadDirectoryChildren ->
            Views.errorWithRetryButton "Error while loading directory children" (fun () -> dispatch (SelectDirectory directory.Path))
        | LoadedDirectoryChildren children ->
            Container.container [] [
                if level > 0 then
                    let newDirectoryName = model.NewDirectoryNames |> Map.tryFind directory.Path
                    Field.div [ Field.HasAddons ] [
                        Control.div [] [
                            Input.text [
                                Input.Value (Option.defaultValue "" newDirectoryName)
                                Input.OnChange (fun ev -> dispatch (UpdateNewDirectoryValue (directory.Path, ev.Value)))
                            ]
                        ]
                        Control.div [] [
                            Button.button
                                [
                                    Button.Disabled newDirectoryName.IsNone
                                    Button.OnClick (fun _ev ->
                                        let name = Map.find directory.Path model.NewDirectoryNames
                                        dispatch (AddChildDirectory (name, directory.Path)))
                                ]
                                [ Icon.icon [] [ Fa.i [ Fa.Solid.Plus ] [] ] ]
                        ]
                    ]
                Button.list [] <| List.map directoryLevelItem children ]

    Container.container [] [
        h2 [ Class "title" ] [ str "Create student directories" ]

        if model.IsEnabled then
            classListView

            Divider.divider []

            match model.Directory.Children with
            | LoadedDirectoryChildren _ ->
                yield!
                    Directory.mapSelected directoryView model.Directory
                    |> List.intersperse (Divider.divider [])
            | NotLoadedDirectoryChildren ->
                Progress.progress [ Progress.Color IsInfo ] []
            | FailedToLoadDirectoryChildren ->
                Views.errorWithRetryButton "Error while loading directory children" (fun () -> dispatch (SelectDirectory model.Directory.Path))

            Divider.divider []

            Button.button
                [
                    Button.Color IsSuccess
                    match Directory.getSelectedDirectory model.Directory, model.SelectedClass with
                    | Some selectedDirectory, Some className when not <| StoragePath.isRoot selectedDirectory.Path ->
                        let data = { ClassName = className; Path = StoragePath.toString selectedDirectory.Path }
                        Button.OnClick (fun _ev -> dispatch (CreateDirectories data))
                    | _ -> Button.Disabled true
                ]
                [ str "Create" ]
        else
            Notification.notification [ Notification.Color IsWarning ]
                [
                    Icon.icon [] [ Fa.i [ Fa.Solid.ExclamationTriangle ] [] ]
                    span [] [ str "Sign in to view directories" ]
                ]
    ]

let stream (authHeader: IAsyncObservable<HttpRequestHeaders option>) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    authHeader
    |> AsyncRx.flatMapLatest (function
        | Some authHeader ->
            [
                AsyncRx.single Enable

                msgs

                let loadClassList =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofPromise (promise {
                            return! Fetch.``get``("/api/classes", Decode.list Decode.string)
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )
                msgs
                |> AsyncRx.choose (function | LoadClassList -> Some loadClassList | _ -> None)
                |> AsyncRx.startWith [ loadClassList ]
                |> AsyncRx.switchLatest
                |> AsyncRx.showErrorToast (fun e -> "Loading list of classes failed", e.Message)
                |> AsyncRx.map LoadClassListResponse

                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        let url = "/api/child-directories"
                        let data = StoragePath.toString StoragePath.empty |> Encode.string
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post(url, data, Decode.list Decode.string, requestProperties)
                    })
                    |> AsyncRx.map (fun children -> Ok (StoragePath.empty, children))
                    |> AsyncRx.catch ((fun e -> StoragePath.empty, e) >> Error >> AsyncRx.single)
                )
                |> AsyncRx.showErrorToast (fun (_, e) -> "Loading root directories failed", e.Message)
                |> AsyncRx.map LoadChildDirectoriesResponse

                let loadChildDirectories path =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofPromise (promise {
                            let url = "/api/child-directories"
                            let data = StoragePath.toString path |> Encode.string
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            return! Fetch.post(url, data, Decode.list Decode.string, requestProperties)
                        })
                        |> AsyncRx.map (fun children -> Ok (path, children))
                        |> AsyncRx.catch ((fun e -> path, e) >> Error >> AsyncRx.single)
                    )
                msgs
                |> AsyncRx.flatMap (function
                    | SelectDirectory path -> loadChildDirectories path
                    | AddChildDirectory (name, path) -> loadChildDirectories (StoragePath.combine path [ name ])
                    | _ -> AsyncRx.empty ()
                )
                |> AsyncRx.showErrorToast (fun (path, e) -> "Loading child directories failed", e.Message)
                |> AsyncRx.map LoadChildDirectoriesResponse

                msgs
                |> AsyncRx.choose (function
                    | AddChildDirectory (name, path) -> Some (SelectDirectory (StoragePath.combine path [ name ]))
                    | _ -> None
                )

                let createDirectories data =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofPromise (promise {
                            let url = "/api/create-student-directories"
                            let data = CreateDirectoriesData.encode data
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            return! Fetch.post(url, data, requestProperties)
                        })
                        |> AsyncRx.map (ignore >> Ok)
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )
                msgs
                |> AsyncRx.choose (function CreateDirectories data -> Some (createDirectories data) | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showSuccessToast (fun () -> "Creating student directories", "Successfully created student directories")
                |> AsyncRx.showErrorToast (fun e -> "Creating student directories failed", e.Message)
                |> AsyncRx.map CreateDirectoriesResponse
            ]
            |> AsyncRx.mergeSeq
        | None ->
            AsyncRx.single Disable
    )