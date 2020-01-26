module CreateStudentGroups

open System
open Elmish
open Fable.React
open Fable.React.Props
open Fable.Reaction
open FSharp.Control
open Fulma
open Fulma.Extensions.Wikiki
open Thoth.Elmish
open Thoth.Fetch
open Thoth.Json

type Student =
    {
        Id: Guid
        Name: string
    }

type ClassStudents =
    | NotLoadedClassStudents
    | FailedToLoadClassStudents
    | LoadedClassStudents of Student list

type Model =
    {
        ClassList: LoadableClassList
        SelectedClass: (string * ClassStudents) option
        GroupSize: int
        Groups: Student list list
    }

type Msg =
    | LoadClassList
    | LoadClassListResponse of Result<string list, exn>
    | SelectClass of string
    | LoadClassStudentsResponse of Result<string list, exn>
    | SetGroupSize of int
    | CreateShuffledGroups
    | RemoveStudent of Guid

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
        { model with SelectedClass = Some (name, NotLoadedClassStudents) }
    | LoadClassStudentsResponse (Ok students) ->
        match model.SelectedClass with
        | Some (className, _) ->
            let students' =
                students
                |> List.map (fun name -> {
                    Id = Guid.NewGuid()
                    Name = name
                })
            { model with
                SelectedClass = Some (className, LoadedClassStudents students')
                Groups = group model.GroupSize students' }
        | None -> model
    | LoadClassStudentsResponse (Error e) ->
        match model.SelectedClass with
        | Some (className, _) ->
            { model with
                SelectedClass = Some (className, FailedToLoadClassStudents)
            }
        | None -> model
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
                |> List.shuffle
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
            Views.errorWithRetryButton "Error while loading class list" (fun () -> dispatch LoadClassList)
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

    let colors = [| IsPrimary; IsLink; IsLight; IsInfo; IsDark; IsSuccess; IsWarning; IsDanger |]

    Container.container [] [
        h2 [ Class "title" ] [ str "Create student groups" ]

        classListView

        Divider.divider []

        Field.div [ Field.IsHorizontal ] [
            Field.label [ Field.Label.IsNormal ] [ Label.label [] [ str "Group size: " ] ]
            Field.body [] [
                Button.list [] [
                    for i in [ 1..20 ] ->
                        Button.button
                            [
                                Button.OnClick (fun _ev -> dispatch (SetGroupSize i))
                                Button.Color (if model.GroupSize = i then IsLink else NoColor)
                            ]
                            [ str (string i) ]
                ]
            ]
        ]

        Divider.divider []

        match model.SelectedClass with
        | Some (_, NotLoadedClassStudents) ->
            Progress.progress [ Progress.Color IsInfo ] []
        | Some (className, FailedToLoadClassStudents) ->
            Views.errorWithRetryButton "Error while loading students" (fun () -> dispatch (SelectClass className))
        | Some (_, LoadedClassStudents _) ->
            yield! [
                for (idx, group) in List.indexed model.Groups ->
                    let color = colors.[idx % colors.Length]
                    Field.div [ Field.IsGrouped; Field.IsHorizontal ] [
                        Field.label [ Field.Label.IsNormal ] [
                            Label.label [] [ str (sprintf "Group #%i" (idx + 1)) ]
                        ]
                        Field.body [] [
                            Field.div [ Field.IsGrouped; Field.IsGroupedMultiline ] [
                                for student in group ->
                                    Control.div [] [
                                        Tag.list [ Tag.List.HasAddons ] [
                                            Tag.tag [ Tag.Size IsMedium; Tag.Color color ] [
                                                str student.Name
                                            ]
                                            Tag.delete
                                                [
                                                    Tag.Size IsMedium
                                                    Tag.Props [ OnClick (fun _ev -> dispatch (RemoveStudent student.Id)) ]
                                                ]
                                                []
                                        ]
                                    ]
                            ]
                        ]
                    ]
            ]
            div [ Style [ MarginBottom "2em" ] ] [
                Button.button
                    [
                        Button.Color IsSuccess
                        Button.OnClick (fun _ev -> dispatch CreateShuffledGroups)
                    ]
                    [ str "Shuffle" ]
            ]
        | None -> ()
    ]

let stream (pageActivated: IAsyncObservable<unit>) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    pageActivated
    |> AsyncRx.flatMapLatest (fun () ->
        [
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
            |> AsyncRx.showSimpleErrorToast (fun e -> "Loading list of classes failed", e.Message)
            |> AsyncRx.map LoadClassListResponse

            let rand = Random();
            let loadStudents className =
                AsyncRx.defer (fun () ->
                    AsyncRx.ofPromise (promise {
                        return! Fetch.``get``(sprintf "/api/classes/%s/students" className, Decode.list Decode.string)
                    })
                    |> AsyncRx.map Ok
                    |> AsyncRx.catch (Error >> AsyncRx.single)
                )
            msgs
            |> AsyncRx.choose (function | SelectClass className -> Some (loadStudents className) | _ -> None)
            |> AsyncRx.switchLatest
            |> AsyncRx.showSimpleErrorToast (fun e -> "Loading students failed", e.Message)
            |> AsyncRx.map LoadClassStudentsResponse
        ]
        |> AsyncRx.mergeSeq
    )