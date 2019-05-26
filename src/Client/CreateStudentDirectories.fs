module CreateStudentDirectories

open Elmish
open Fable.FontAwesome
open Fable.React
open Fulma
open Fulma.Extensions.Wikiki
open Thoth.Elmish
open Thoth.Fetch
open Thoth.Json
open Directories
open Shared.CreateStudentDirectories

type Model =
    {
        ClassList: string list list
        SelectedClass: string option
        Directory: Directory
        NewDirectoryNames: Map<string list, string>
    }

type Msg =
    | Init
    | LoadClassList
    | LoadClassListResponse of Result<string list, exn>
    | SelectClass of string
    | SelectDirectory of string list
    | SelectDirectoryResponse of Result<string list * string list, exn>
    | UpdateNewDirectoryValue of string list * string
    | AddChildDirectory of string list
    | CreateDirectories
    | CreateDirectoriesResponse of Result<unit, exn>

let rec update authHeaderOptFn msg model =
    match msg with
    | Init ->
        let model', loadChildDirectoriesCmd = update authHeaderOptFn (SelectDirectory []) model
        let model'', loadClassListCmd = update authHeaderOptFn LoadClassList model'
        model'', Cmd.batch [ loadChildDirectoriesCmd; loadClassListCmd ]
    | LoadClassList ->
        let cmd =
            Cmd.OfPromise.either
                (fun () -> Fetch.get("/api/classes", Decode.list Decode.string))
                ()
                (Ok >> LoadClassListResponse)
                (Error >> LoadClassListResponse)
        model, cmd
    | LoadClassListResponse (Ok classList) ->
        let model' = { model with ClassList = Classes.groupAndSort classList }
        model', Cmd.none
    | LoadClassListResponse (Error e) ->
        let cmd =
            Toast.toast "Loading list of classes failed" e.Message
            |> Toast.error
        model, cmd
    | SelectClass name ->
        let model' = { model with SelectedClass = Some name }
        model', Cmd.none
    | SelectDirectory path ->
        let model' = { model with Directory = selectDirectory path model.Directory }
        // TODO don't load if already loaded?
        let cmd =
            match authHeaderOptFn with
            | Some getAuthHeader ->
                Cmd.OfPromise.either
                    (fun (path, getAuthHeader) -> promise {
                        let url = "/api/child-directories"
                        let data = List.rev path |> List.map Encode.string |> Encode.list
                        let! authHeader = getAuthHeader ()
                        let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                        return! Fetch.post(url, data, Decode.list Decode.string, requestProperties)
                    })
                    (path, getAuthHeader)
                    ((fun r -> path, r) >> Ok >> SelectDirectoryResponse)
                    (Error >> SelectDirectoryResponse)
            | None -> Cmd.none
        model', cmd
    | SelectDirectoryResponse (Ok (path, childDirectories)) ->
        let model' = { model with Directory = setChildDirectories path childDirectories model.Directory }
        model', Cmd.none
    | SelectDirectoryResponse (Error e) ->
        let cmd =
            Toast.toast "Loading directories failed" e.Message
            |> Toast.error
        model, cmd
    | UpdateNewDirectoryValue (path, value) ->
        let model' = { model with NewDirectoryNames = model.NewDirectoryNames |> Map.add path value }
        model', Cmd.none
    | AddChildDirectory path ->
        let name = Map.find path model.NewDirectoryNames
        let model' =
            { model with
                Directory = addChildDirectory path name model.Directory
                NewDirectoryNames = model.NewDirectoryNames |> Map.remove path }
        update authHeaderOptFn (SelectDirectory (name :: path)) model'
    | CreateDirectories ->
        match getSelectedDirectory model.Directory, model.SelectedClass with
        | Some selectedDirectory, Some className ->
            match List.rev selectedDirectory.Path with
            | baseDir :: path ->
                let input = { ClassName = className; Path = baseDir, path }
                match authHeaderOptFn with
                | Some getAuthHeader ->
                    let cmd =
                        Cmd.OfPromise.either
                            (fun (getAuthHeader, input) -> promise {
                                let url = "/api/create-student-directories"
                                let data =
                                    Encode.object
                                        [
                                            "className", Encode.string input.ClassName
                                            "path", Encode.tuple2 Encode.string (List.map Encode.string >> Encode.list) input.Path
                                        ]
                                let! authHeader = getAuthHeader ()
                                let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                                return! Fetch.post(url, data, requestProperties)
                            })
                            (getAuthHeader, input)
                            (ignore >> Ok >> CreateDirectoriesResponse)
                            (Error >> CreateDirectoriesResponse)
                    model, cmd
                | None ->
                    let msg = exn "Please sign in using your Microsoft account." |> Error |> CreateDirectoriesResponse
                    update authHeaderOptFn msg model
            | _ -> model, Cmd.none
        | _ -> model, Cmd.none
    | CreateDirectoriesResponse (Ok ()) ->
        let cmd =
            Toast.toast "Creating student directories" "Successfully created student directories"
            |> Toast.success
        model, cmd
    | CreateDirectoriesResponse (Error e) ->
        let cmd =
            Toast.toast "Creating student directories" e.Message
            |> Toast.success
        model, cmd

let init authHeaderOptFn =
    let model =
        { ClassList = []
          SelectedClass = None
          Directory =
            { Path = []
              IsSelected = true
              Children = NotLoadedDirectoryChildren }
          NewDirectoryNames = Map.empty }
    update authHeaderOptFn Init model

let view model dispatch =
    let classListView =
        Container.container []
            [ for group in model.ClassList ->
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
            [ str (List.tryHead directory.Path |> Option.defaultValue "<none>") ]

    let directoryView level directory =
        match directory.Children with
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
                                  Button.OnClick (fun _ev -> dispatch (AddChildDirectory directory.Path)) ]
                                [ Icon.icon [] [ Fa.i [ Fa.Solid.Plus ] [] ] ] ] ]
                  yield Button.list [] [ yield! List.map directoryLevelItem children ] ]
            |> Some
        | _ -> None

    let isAnyDirectorySelected =
        getSelectedDirectory model.Directory
        |> Option.map (fun d -> d.Path |> List.isEmpty |> not)
        |> Option.defaultValue false

    Container.container []
        [ yield classListView
          yield Divider.divider []
          match model.Directory.Children with
          | LoadedDirectoryChildren _ ->
            yield!
                mapDirectory directoryView model.Directory
                |> List.choose id
                |> List.intersperse (Divider.divider [])
          | NotLoadedDirectoryChildren ->
            yield Notification.notification [ Notification.Color IsWarning ]
                [ Icon.icon [] [ Fa.i [ Fa.Solid.ExclamationTriangle ] [] ]
                  span [] [ str "Sign in to view directories" ] ]
          yield Divider.divider []
          yield Button.button
            [ Button.Color IsSuccess
              Button.Disabled (not isAnyDirectorySelected || model.SelectedClass.IsNone)
              Button.OnClick (fun _ev -> dispatch CreateDirectories) ]
            [ str "Create" ] ]