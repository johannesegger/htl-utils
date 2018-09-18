module CreateStudentDirectories

open Elmish
open Fable.Helpers.React
open Fable.PowerPack
open Fable.PowerPack.Fetch
open Fulma
open Fulma.Extensions
open Fulma.FontAwesome
open Thoth.Elmish
open Shared.CreateStudentDirectories

type DirectoryChildren =
    | LoadedDirectoryChildren of Directory list
    | NotLoadedDirectoryChildren
and Directory =
    { Path: string list
      IsSelected: bool
      Children: DirectoryChildren }

type Model =
    { ClassList: string list list
      SelectedClass: string option
      Directory: Directory
      NewDirectoryNames: Map<string list, string> }

type Msg =
    | LoadClassList
    | LoadClassListResponse of Result<string list, exn>
    | SelectClass of string
    | LoadChildDirectories of string list
    | LoadChildDirectoriesResponse of Result<string list * string list, exn>
    | SelectDirectory of string list
    | UpdateNewDirectoryValue of string list * string
    | AddChildDirectory of string list
    | CreateDirectories
    | CreateDirectoriesResponse of Result<unit, exn>

let private updateDirectory path fn directory =
    let rec updateDirectory' path directory =
        match path, directory with
        | [], dir ->
            fn dir
        | path :: xs, ({ Children = LoadedDirectoryChildren children } as dir) ->
            let childDirs =
                children
                |> List.map (fun childDir ->
                    if List.head childDir.Path = path
                    then updateDirectory' xs childDir
                    else childDir
                )
            { dir with Children = LoadedDirectoryChildren childDirs }
        | _ :: _, { Children = NotLoadedDirectoryChildren } ->
            // Should not happen
            directory
    updateDirectory' (List.rev path) directory

let private setChildDirectories path childDirectories directory =
    let fn dir =
        let childDirectories' =
            childDirectories
            |> List.map (fun n -> { Path = n :: dir.Path; IsSelected = false; Children = NotLoadedDirectoryChildren })
        { dir with Children = LoadedDirectoryChildren childDirectories' }

    updateDirectory path fn directory

let private selectDirectory path directory =
    let rec selectDirectory' directory =
        let isSelected = path |> List.rev |> List.truncate (directory.Path.Length) = List.rev directory.Path
        let children =
            match directory.Children with
            | LoadedDirectoryChildren children ->
                LoadedDirectoryChildren (List.map selectDirectory' children)
            | NotLoadedDirectoryChildren ->
                NotLoadedDirectoryChildren
        { directory with
            IsSelected = isSelected
            Children = children }
    selectDirectory' directory

let private addChildDirectory path name directory =
    let fn dir =
        match dir with
        | { Children = LoadedDirectoryChildren children } ->
            let children' =
                let child = { Path = name :: dir.Path; IsSelected = false; Children = NotLoadedDirectoryChildren }
                child :: children
            { dir with Children = LoadedDirectoryChildren children' }
        | x -> x
    updateDirectory path fn directory

let rec private getSelectedDirectory directory =
    if not directory.IsSelected
    then None
    else
        match directory with
        | { Children = LoadedDirectoryChildren children } ->
            children
            |> List.tryPick getSelectedDirectory
            |> Option.orElse (Some directory)
        | _ -> Some directory

let rec update authHeaderOptFn msg model =
    match msg with
    | LoadClassList ->
        let cmd =
            Cmd.ofPromise
                (fetchAs<string list> "/api/students/classes")
                []
                (Ok >> LoadClassListResponse)
                (Error >> LoadClassListResponse)
        model, cmd
    | LoadClassListResponse (Ok classList) ->
        let model' = { model with ClassList = classList |> List.groupBy (fun v -> v.[0]) |> List.map snd }
        model', Cmd.none
    | LoadClassListResponse (Error e) ->
        let cmd =
            Toast.toast "Loading list of classes failed" e.Message
            |> Toast.error
        model, cmd
    | SelectClass name ->
        let model' = { model with SelectedClass = Some name }
        model', Cmd.none
    | LoadChildDirectories path ->
        // TODO don't load if already loaded?
        let cmd =
            match authHeaderOptFn with
            | Some getAuthHeader ->
                Cmd.ofPromise
                    (getAuthHeader
                        >> Promise.bind (
                            List.singleton
                            >> requestHeaders
                            >> List.singleton
                            >> postRecord "/api/create-student-directories/child-directories" (List.rev path))
                        >> Promise.bind (fun r -> r.json<string list>()))
                    ()
                    ((fun r -> path, r) >> Ok >> LoadChildDirectoriesResponse)
                    (Error >> LoadChildDirectoriesResponse)
            | None -> Cmd.none
        model, cmd
    | LoadChildDirectoriesResponse (Ok (path, childDirectories)) ->
        let model' = { model with Directory = setChildDirectories path childDirectories model.Directory }
        update authHeaderOptFn (SelectDirectory path) model'
    | LoadChildDirectoriesResponse (Error e) ->
        let cmd =
            Toast.toast "Loading directories failed" e.Message
            |> Toast.error
        model, cmd
    | SelectDirectory path ->
        let model' = { model with Directory = selectDirectory path model.Directory }
        model', Cmd.none
    | UpdateNewDirectoryValue (path, value) ->
        let model' = { model with NewDirectoryNames = model.NewDirectoryNames |> Map.add path value }
        model', Cmd.none
    | AddChildDirectory path ->
        let name = Map.find path model.NewDirectoryNames
        let model' =
            { model with
                Directory = addChildDirectory path name model.Directory
                NewDirectoryNames = model.NewDirectoryNames |> Map.remove path }
        update authHeaderOptFn (LoadChildDirectories (name :: path)) model'
    | CreateDirectories ->
        let cmd =
            match getSelectedDirectory model.Directory, model.SelectedClass with
            | Some selectedDirectory, Some className ->
                match List.rev selectedDirectory.Path with
                | baseDir :: path ->
                    let record = { ClassName = className; Path = baseDir, path }
                    match authHeaderOptFn with
                    | Some getAuthHeader ->
                        Cmd.ofPromise
                            (getAuthHeader >> Promise.bind (List.singleton >> requestHeaders >> List.singleton >> postRecord "/api/create-student-directories/create" record))
                            ()
                            (ignore >> Ok >> CreateDirectoriesResponse)
                            (Error >> CreateDirectoriesResponse)
                    | None -> Cmd.none
                | _ -> Cmd.none
            | _ -> Cmd.none
        model, cmd
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
    let model', cmd' = update authHeaderOptFn (LoadChildDirectories []) model
    let model'', cmd'' = update authHeaderOptFn LoadClassList model'
    model'', Cmd.batch [ cmd'; cmd'' ]

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
        let props =
            if directory.IsSelected
            then [ Button.Color IsLink ]
            else [ Button.OnClick (fun _ev -> directory.Path |> LoadChildDirectories |> dispatch) ]
        Button.button props [ str (List.tryHead directory.Path |> Option.defaultValue "<none>") ]

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
                                [ Icon.faIcon []
                                    [ Fa.icon Fa.I.Plus ] ] ] ]
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
                [ Icon.faIcon [] [ Fa.icon Fa.I.ExclamationTriangle ]
                  span [] [ str "Sign in to view directories" ] ]
          yield Divider.divider []
          yield Button.button
            [ Button.Color IsSuccess
              Button.Disabled (not isAnyDirectorySelected || model.SelectedClass.IsNone)
              Button.OnClick (fun _ev -> dispatch CreateDirectories) ]
            [ str "Create" ] ]