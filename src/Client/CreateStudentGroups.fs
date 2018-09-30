module CreateStudentGroups

open System
open Elmish
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.PowerPack
open Fable.PowerPack.Fetch
open Fulma
open Fulma.Extensions
open Fulma.FontAwesome
open Thoth.Elmish
open Shared.CreateStudentDirectories
open Authentication

type Student =
    { Id: Guid
      LastName: string
      FirstName: string }

type ClassStudents =
    | NotLoaded
    | Loaded of Student list

type Model =
    { ClassList: string list list
      SelectedClass: (string * ClassStudents) option
      GroupSize: int
      Groups: Student list list }

type Msg =
    | Init
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
    | Init ->
        let model', loadClassListCmd = update LoadClassList model
        model', loadClassListCmd
    | LoadClassList ->
        let cmd =
            Cmd.ofPromise
                (fetchAs<string list> "/api/classes")
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
        let model' = { model with SelectedClass = Some (name, NotLoaded) }
        let cmd =
            Cmd.ofPromise
                (fetchAs<(string * string) list> (sprintf "/api/classes/%s/students" name))
                []
                (Ok >> LoadClassStudentsResponse)
                (Error >> LoadClassStudentsResponse)
        model', cmd
    | LoadClassStudentsResponse (Ok students) ->
        let students' =
            students
            |> List.map (fun (lastName, firstName) ->
                { Id = Guid.NewGuid()
                  LastName = lastName
                  FirstName = firstName })
        let model' =
            match model.SelectedClass with
            | Some (className, _) ->
                { model with
                    SelectedClass = Some (className, Loaded students')
                    Groups = group model.GroupSize students' }
            | None -> model
        model', Cmd.none
    | LoadClassStudentsResponse (Error e) ->
        let cmd =
            Toast.toast "Loading students failed" e.Message
            |> Toast.error
        model, cmd
    | SetGroupSize size ->
        let model' =
            { model with
                GroupSize = size
                Groups =
                    model.Groups
                    |> List.collect id
                    |> group size }
        model', Cmd.none
    | CreateShuffledGroups ->
        let model' =
            { model with
                Groups =
                    model.Groups
                    |> List.collect id
                    |> shuffle
                    |> group model.GroupSize }
        model', Cmd.none
    | RemoveStudent studentId ->
        let model' =
            { model with
                Groups =
                    model.Groups
                    |> List.map (fun group ->
                        group
                        |> List.filter (fun student -> student.Id <> studentId)
                    )
                    |> List.collect id
                    |> group model.GroupSize }
        model', Cmd.none

let init =
    let model =
        { ClassList = []
          SelectedClass = None
          GroupSize = 2
          Groups = [] }
    update Init model

let view model dispatch =
    let classListView =
        Container.container []
            [ for group in model.ClassList ->
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
                                [ Field.div [ Field.IsGrouped; Field.CustomClass Field.Classes.IsGrouped.Multiline ]
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