module CreateStudentDirectories

open Elmish
open Elmish.Streams
open Fable.FontAwesome
open Fable.React
open FSharp.Control
open Fulma
open Fulma.Extensions.Wikiki
open Thoth.Elmish
open Thoth.Fetch
open Thoth.Json
open Classes
open Directories
open Shared.Common
open Shared.CreateStudentDirectories

type Model =
    {
        ClassList: ClassList
        SelectedClass: string option
        Directory: Directory
        NewDirectoryNames: Map<DirectoryPath, string>
    }

type Msg =
    | LoadClassList
    | LoadClassListResponse of Result<string list, exn>
    | SelectClass of string
    | SelectDirectory of DirectoryPath
    | LoadChildDirectoriesResponse of Result<DirectoryPath * string list, DirectoryPath * exn>
    | UpdateNewDirectoryValue of DirectoryPath * string
    | AddChildDirectory of string * DirectoryPath
    | CreateDirectories of CreateDirectoriesData
    | CreateDirectoriesResponse of Result<unit, exn>

let rec update msg model =
    match msg with
    | LoadClassList ->
        { model with ClassList = NotLoadedClassList }
    | LoadClassListResponse (Ok classList) ->
        { model with ClassList = LoadedClassList (Classes.groupAndSort classList) }
    | LoadClassListResponse (Error e) ->
        { model with ClassList = FailedToLoadClassList }
    | SelectClass name ->
        { model with SelectedClass = Some name }
    | SelectDirectory path ->
        { model with Directory = selectDirectory path model.Directory }
    | LoadChildDirectoriesResponse (Ok (path, childDirectories)) ->
        { model with Directory = setChildDirectories path childDirectories model.Directory }
    | LoadChildDirectoriesResponse (Error (path, e)) ->
        { model with Directory = setChildDirectoriesFailedToLoad path model.Directory }
    | UpdateNewDirectoryValue (path, value) ->
        { model with NewDirectoryNames = Map.add path value model.NewDirectoryNames }
    | AddChildDirectory (name, path) ->
        { model with
            Directory = addChildDirectory path name model.Directory
            NewDirectoryNames = Map.remove path model.NewDirectoryNames }
    | CreateDirectories data -> model
    | CreateDirectoriesResponse (Ok ()) -> model
    | CreateDirectoriesResponse (Error e) -> model

let init =
    {
        ClassList = NotLoadedClassList
        SelectedClass = None
        Directory =
            {
                Path = DirectoryPath.empty
                IsSelected = true
                Children = NotLoadedDirectoryChildren
            }
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
            Container.container []
                [ for group in classList ->
                    Button.list []
                        [ for name in group ->
                            Button.button
                                [ yield Button.OnClick (fun _ev -> dispatch (SelectClass name))
                                  match model.SelectedClass with
                                  | Some n when n = name -> yield Button.Color IsLink
                                  | _ -> () ]
                                [ str name ] ] ]

    let directoryLevelItem directory =
        Button.button
            [
                Button.Color (if directory.IsSelected then IsLink else NoColor)
                Button.OnClick (fun _ev -> directory.Path |> SelectDirectory |> dispatch)
            ]
            [ str (DirectoryPath.getName directory.Path) ]

    let directoryView level directory =
        match directory.Children with
        | NotLoadedDirectoryChildren ->
            Progress.progress [ Progress.Color IsInfo ] []
        | FailedToLoadDirectoryChildren ->
            Views.errorWithRetryButton "Error while loading directory children" (fun () -> dispatch (SelectDirectory directory.Path))
        | LoadedDirectoryChildren children ->
            Container.container []
                [ if level > 0 then
                    let newDirectoryName = model.NewDirectoryNames |> Map.tryFind directory.Path
                    yield Field.div [ Field.HasAddons ]
                        [ Control.div []
                            [ Input.text
                                [ Input.Value (Option.defaultValue "" newDirectoryName)
                                  Input.OnChange (fun ev -> dispatch (UpdateNewDirectoryValue (directory.Path, ev.Value))) ] ]
                          Control.div []
                            [ Button.button
                                [ Button.Disabled newDirectoryName.IsNone
                                  Button.OnClick (fun _ev ->
                                    let name = Map.find directory.Path model.NewDirectoryNames
                                    dispatch (AddChildDirectory (name, directory.Path))) ]
                                [ Icon.icon [] [ Fa.i [ Fa.Solid.Plus ] [] ] ] ] ]
                  yield Button.list [] [ yield! List.map directoryLevelItem children ] ]

    let isAnyDirectorySelected =
        getSelectedDirectory model.Directory
        |> Option.map ((fun d -> d.Path) >> DirectoryPath.isRoot >> not)
        |> Option.defaultValue false

    Container.container [] [
        yield classListView

        yield Divider.divider []

        match model.Directory.Children with
        | LoadedDirectoryChildren _ ->
            yield!
                mapDirectory directoryView model.Directory
                |> List.intersperse (Divider.divider [])
        | NotLoadedDirectoryChildren ->
            yield Notification.notification [ Notification.Color IsWarning ]
                [ Icon.icon [] [ Fa.i [ Fa.Solid.ExclamationTriangle ] [] ]
                  span [] [ str "Sign in to view directories" ] ]
        | FailedToLoadDirectoryChildren ->
            yield Views.errorWithRetryButton "Error while loading directory children" (fun () -> dispatch (SelectDirectory model.Directory.Path))

        yield Divider.divider []

        yield Button.button
            [
                yield Button.Color IsSuccess
                yield Button.Disabled (not isAnyDirectorySelected || model.SelectedClass.IsNone)
                match getSelectedDirectory model.Directory, model.SelectedClass with
                | Some selectedDirectory, Some className ->
                    let data = { ClassName = className; Path = selectedDirectory.Path }
                    yield Button.OnClick (fun _ev -> dispatch (CreateDirectories data))
                | _ -> ()
            ]
            [ str "Create" ]
    ]

let stream authHeader states msgs =
    authHeader
    |> AsyncRx.choose id
    |> AsyncRx.flatMapLatest (fun authHeader ->
        [
            yield msgs

            let loadClassesResponseToast response =
                match response with
                | Ok _ -> Cmd.none
                | Error (e: exn) ->
                    Toast.toast "Loading list of classes failed" e.Message
                    |> Toast.error
            let loadClassList =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        return! Fetch.``get``("/api/classes", Decode.list Decode.string)
                    })
                    |> AsyncRx.map Ok
                    |> AsyncRx.catch (Error >> AsyncRx.single)
                )
            yield
                msgs
                |> AsyncRx.choose (function | LoadClassList -> Some loadClassList | _ -> None)
                |> AsyncRx.startWith [ loadClassList ]
                |> AsyncRx.switchLatest
                |> AsyncRx.showToast loadClassesResponseToast
                |> AsyncRx.map LoadClassListResponse

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
                        let data = DirectoryPath.encode DirectoryPath.empty
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post(url, data, Decode.list Decode.string, requestProperties)
                    })
                    |> AsyncRx.map (fun children -> Ok (DirectoryPath.empty, children))
                    |> AsyncRx.catch ((fun e -> DirectoryPath.empty, e) >> Error >> AsyncRx.single)
                )
                |> AsyncRx.showToast loadRootDirectoriesResponseToast
                |> AsyncRx.map LoadChildDirectoriesResponse

            let loadChildDirectories path =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        let url = "/api/child-directories"
                        let data = DirectoryPath.encode path
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
                    | AddChildDirectory (name, path) -> Some (loadChildDirectories (DirectoryPath.combine path [ name ]))
                    | _ -> None
                )
                |> AsyncRx.switchLatest
                |> AsyncRx.showToast loadChildDirectoriesResponseToast
                |> AsyncRx.map LoadChildDirectoriesResponse

            yield
                msgs
                |> AsyncRx.choose (function
                    | AddChildDirectory (name, path) -> Some (SelectDirectory (DirectoryPath.combine path [ name ]))
                    | _ -> None
                )

            let createDirectories data =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        let url = "/api/create-student-directories"
                        let data =
                            Encode.object
                                [
                                    "className", Encode.string data.ClassName
                                    "path", DirectoryPath.encode data.Path
                                ]
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post(url, data, requestProperties)
                    })
                    |> AsyncRx.map (ignore >> Ok)
                    |> AsyncRx.catch (Error >> AsyncRx.single)
                )
            let createDirectoriesResponseToast response =
                match response with
                | Ok () ->
                    Toast.toast "Creating student directories" "Successfully created student directories"
                    |> Toast.success
                | Error (e: exn) ->
                    Toast.toast "Creating student directories" e.Message
                    |> Toast.error
            yield
                msgs
                |> AsyncRx.choose (function CreateDirectories data -> Some (createDirectories data) | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showToast createDirectoriesResponseToast
                |> AsyncRx.map CreateDirectoriesResponse
        ]
        |> AsyncRx.mergeSeq
    )