module CreateStudentGroups

open System
open Elmish
open Fable.Helpers.React
open Fable.PowerPack
open Fable.PowerPack.Fetch
open Fulma
open Fulma.Extensions
open Fulma.FontAwesome
open Thoth.Elmish
open Shared.CreateStudentDirectories
open Authentication

type ClassStudents =
    | NotLoaded
    | Loaded of (string * string) list

type Model =
    { ClassList: string list list
      SelectedClass: (string * ClassStudents) option
      GroupSize: int
      Groups: (string * string) list list }

type Msg =
    | Init
    | LoadClassList
    | LoadClassListResponse of Result<string list, exn>
    | SelectClass of string
    | LoadClassStudentsResponse of Result<(string * string) list, exn>
    | SetGroupSize of int
    | CreateShuffledGroups

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
        let model' =
            match model.SelectedClass with
            | Some (className, _) ->
                { model with
                    SelectedClass = Some (className, Loaded students)
                    Groups = group model.GroupSize students }
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

    Container.container []
        [ yield classListView
          yield Divider.divider []
          
          yield!
            [ for group in model.Groups ->
                Tag.list []
                    [ for (lastName, firstName) in group ->
                        Tag.tag [] [ str (sprintf "%s %s" (lastName.ToUpper()) firstName) ] ]
            ]

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
            [ str "Shuffle" ] ]