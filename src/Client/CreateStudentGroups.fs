module CreateStudentGroups

open System
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
open Classes

type Student =
    { Id: Guid
      LastName: string
      FirstName: string }

type ClassStudents =
    | NotLoaded
    | Loaded of Student list

type Model =
    { ClassList: ClassList
      SelectedClass: (string * ClassStudents) option
      GroupSize: int
      Groups: Student list list }

type Msg =
    | LoadClassList
    | LoadClassListResponse of Result<string list, exn>
    | SelectClass of string
    | LoadClassStudentsResponse of Result<(string * string) list, exn>
    | SetGroupSize of int
    | CreateShuffledGroups
    | RemoveStudent of Guid

let private shuffle =
    let rand = Random()

    let swap (a: _[]) x y =
        let tmp = a.[x]
        a.[x] <- a.[y]
        a.[y] <- tmp

    fun l ->
        let a = Array.ofList l
        Array.iteri (fun i _ -> swap a i (rand.Next(i, Array.length a))) a
        List.ofArray a

let private group size list =
    Seq.chunkBySize size list
    |> Seq.map Seq.toList
    |> Seq.toList

let rec update msg model =
    match msg with
    | LoadClassList ->
        { model with ClassList = NotLoadedClassList }
    | LoadClassListResponse (Ok classList) ->
        { model with ClassList = LoadedClassList (Classes.groupAndSort classList) }
    | LoadClassListResponse (Error e) ->
        { model with ClassList = FailedToLoadClassList }
    | SelectClass name ->
        { model with SelectedClass = Some (name, NotLoaded) }
    | LoadClassStudentsResponse (Ok students) ->
        match model.SelectedClass with
        | Some (className, _) ->
            let students' =
                students
                |> List.map (fun (lastName, firstName) ->
                    {
                        Id = Guid.NewGuid()
                        LastName = lastName
                        FirstName = firstName
                    }
                )
            { model with
                SelectedClass = Some (className, Loaded students')
                Groups = group model.GroupSize students' }
        | None -> model
    | LoadClassStudentsResponse (Error e) ->
        model // TODO set to error
    | SetGroupSize size ->
        { model with
            GroupSize = size
            Groups =
                model.Groups
                |> List.collect id
                |> group size }
    | CreateShuffledGroups ->
        { model with
            Groups =
                model.Groups
                |> List.collect id
                |> shuffle
                |> group model.GroupSize }
    | RemoveStudent studentId ->
        { model with
            Groups =
                model.Groups
                |> List.map (fun group ->
                    group
                    |> List.filter (fun student -> student.Id <> studentId)
                )
                |> List.collect id
                |> group model.GroupSize }

let init =
    {
        ClassList = NotLoadedClassList
        SelectedClass = None
        GroupSize = 2
        Groups = []
    }

let view model dispatch =
    let classListView =
        match model.ClassList with
        | NotLoadedClassList ->
            Progress.progress [ Progress.Color IsInfo ] []
        | FailedToLoadClassList ->
            Notification.notification [ Notification.Color IsDanger ]
                [
                    Level.level []
                        [
                            Level.left []
                                [
                                    Level.item []
                                        [
                                            Icon.icon [] [ Fa.i [ Fa.Solid.ExclamationTriangle ] [] ]
                                            span [] [ str "Error while loading class list" ]
                                        ]
                                    Level.item []
                                        [
                                            Button.button
                                                [
                                                    Button.Color IsSuccess
                                                    Button.OnClick (fun _ev -> dispatch LoadClassList)
                                                ]
                                                [
                                                    Icon.icon [] [ Fa.i [ Fa.Solid.Sync ] [] ]
                                                    span [] [ str "Retry" ]
                                                ]
                                        ]
                                ]
                        ]
                ]
        | LoadedClassList classList ->
            Container.container []
                [ for group in classList ->
                    Button.list []
                        [ for name in group ->
                            Button.button
                                [ yield Button.OnClick (fun _ev -> dispatch (SelectClass name))
                                  match model.SelectedClass with
                                  | Some (className, _) when className = name -> yield Button.Color IsLink
                                  | _ -> () ]
                                [ str name ] ] ]

    let canShuffle =
        model.SelectedClass
        |> Option.map (snd >> function | NotLoaded -> false | Loaded _ -> true)
        |> Option.defaultValue false

    let colors = [| IsPrimary; IsLink; IsLight; IsInfo; IsDark; IsSuccess; IsWarning; IsDanger |]

    Container.container []
        [ yield classListView

          yield Divider.divider []

          yield Field.div [ Field.IsHorizontal ]
            [ Field.label [ Field.Label.IsNormal ] [ Label.label [] [ str "Group size: " ] ]
              Field.body []
                [ Button.list []
                    [ for i in [ 1..20 ] ->
                        Button.button
                            [ yield Button.OnClick (fun _ev -> dispatch (SetGroupSize i))
                              if model.GroupSize = i
                              then yield Button.Color IsLink ]
                            [ str (string i) ] ] ] ]
          yield Button.button
            [ Button.Color IsSuccess
              Button.Disabled (not canShuffle)
              Button.OnClick (fun _ev -> dispatch CreateShuffledGroups) ]
            [ str "Shuffle" ]

          yield Divider.divider []

          yield!
                [ for (idx, group) in List.indexed model.Groups do
                    let color = colors.[idx % colors.Length]
                    yield
                        Field.div [ Field.IsGrouped; Field.IsHorizontal ]
                            [ Field.label [ Field.Label.IsNormal ]
                                [ Label.label [] [ str (sprintf "Group #%i" (idx + 1)) ] ]
                              Field.body [ ]
                                [ Field.div [ Field.IsGrouped; Field.IsGroupedMultiline ]
                                    [ for student in group ->
                                        Control.div []
                                            [ Tag.list [ Tag.List.HasAddons ]
                                                [ Tag.tag [ Tag.Size IsMedium; Tag.Color color ]
                                                    [ str (sprintf "%s %s" (student.LastName.ToUpper()) student.FirstName) ]
                                                  Tag.delete
                                                    [ Tag.Size IsMedium
                                                      Tag.Props [ OnClick (fun _ev -> dispatch (RemoveStudent student.Id)) ] ]
                                                    [] ] ] ] ] ]
                ] ]

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

            let loadStudents name =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        return! Fetch.``get``(sprintf "/api/classes/%s/students" name, Decode.list (Decode.tuple2 Decode.string Decode.string))
                    })
                    |> AsyncRx.map Ok
                    |> AsyncRx.catch (Error >> AsyncRx.single)
                )
            let loadStudentsResponseToast response =
                match response with
                | Ok _ -> Cmd.none
                | Error (e: exn) ->
                    Toast.toast "Loading students failed" e.Message
                    |> Toast.error
            yield
                msgs
                |> AsyncRx.choose (function | SelectClass name -> Some (loadStudents name) | _ -> None)
                |> AsyncRx.switchLatest
                |> AsyncRx.showToast loadStudentsResponseToast
                |> AsyncRx.map LoadClassStudentsResponse
        ]
        |> AsyncRx.mergeSeq
    )