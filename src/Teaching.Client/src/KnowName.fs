module KnowName

open Fable.Core
open Fable.Core.JsInterop
open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Fable.Reaction
open Fetch.Types
open FSharp.Control
open Fulma
open Shared.KnowName
open System
open Thoth.Fetch
open Thoth.Json
open Thoth.Elmish

module Group =
    let toString = function
        | Teachers -> "Teachers"
        | Students className -> className

type GroupLoadStatus =
    | GroupLoadStatusNotLoaded
    | GroupLoadStatusLoaded of Person list

type LoadableGroup = {
    Group: Group
    LoadStatus: GroupLoadStatus
}

type PersonWithImage = {
    DisplayName: string
    ImageUrl: string
}
module PersonWithImage =
    let tryFromPerson (person: Person) =
        match person.ImageUrl with
        | Some imageUrl -> Some { DisplayName = person.DisplayName; ImageUrl = imageUrl }
        | None -> None
    let toPerson person =
        { Person.DisplayName = person.DisplayName; ImageUrl = Some person.ImageUrl }

module Tuple =
    let traverseSnd fn (a, b) =
        match fn b with
        | Some b -> Some (a, b)
        | None -> None

type Suggestions = {
    Items: (Group * Person) list
    Highlighted: (Group * PersonWithImage) option
}

module Suggestions =
    let tryIndexOfHighlighted model =
        model.Highlighted
        |> Option.bind (fun (group, person) ->
            let item = group, PersonWithImage.toPerson person
            List.tryFindIndex ((=) item) model.Items
        )
    type HighlightModifier = Previous | Next
    let changeHighlightedItem model modifier =
        let items =
            List.indexed model.Items
            |> List.choose (Tuple.traverseSnd (Tuple.traverseSnd PersonWithImage.tryFromPerson))
        let newItem =
            match tryIndexOfHighlighted model, modifier with
            | Some index, Previous ->
                let indexInItems = List.findIndex (fst >> (=) index) items
                List.tryItem (indexInItems - 1) items
                |> Option.orElse (List.tryLast items)
            | Some index, Next ->
                let indexInItems = List.findIndex (fst >> (=) index) items
                List.tryItem (indexInItems + 1) items
                |> Option.orElse (List.tryHead items)
            | None, Previous -> items |> List.tryLast
            | None, Next -> items |> List.tryHead
        { model with Highlighted = Option.map snd newItem }

type PlayModel = {
    AllPersons: (Group * Person) list
    RemainingPersons: (Group * PersonWithImage) list
    CurrentGuess: string
    Suggestions: Suggestions
}

module PlayModel =
    let currentPerson model = List.tryHead model.RemainingPersons
    let choosePersonsWithImage =
        List.choose (fun (group, person: Person) ->
            match PersonWithImage.tryFromPerson person with
            | Some person -> Some (group, person)
            | None -> None
        )
    let updateSuggestions model =
        let suggestions =
            model.AllPersons
            |> List.filter (snd >> fun p -> p.DisplayName.ToUpper().Contains(model.CurrentGuess.ToUpper())) // TODO use StringComparison?
        let suggestionsWithImage = choosePersonsWithImage suggestions
        { model with
            Suggestions =
                { model.Suggestions with
                    Items = suggestions
                    Highlighted =
                        match model.Suggestions.Highlighted with
                        | Some highlightedPerson when List.contains highlightedPerson suggestionsWithImage -> Some highlightedPerson
                        | Some _
                        | None -> List.tryHead suggestionsWithImage
                }
        }
    let addGroup (group, persons) model =
        { model with
            AllPersons =
                persons
                |> List.map (fun person -> group, person)
                |> List.append model.AllPersons
                |> List.sortBy (fun (group, person: Person) -> group, person.DisplayName)
            RemainingPersons =
                let newPersons =
                    persons
                    |> List.map (fun person -> group, person)
                    |> choosePersonsWithImage
                match model.RemainingPersons with
                | [] -> List.shuffle newPersons
                | x :: xs -> x :: (xs @ newPersons |> List.shuffle)
        }
        |> updateSuggestions
    let removeGroup group model =
        { model with
            AllPersons = model.AllPersons |> List.filter (fst >> ((<>) group))
            RemainingPersons = model.RemainingPersons |> List.filter (fst >> ((<>) group))
        }
        |> updateSuggestions
    let updateGuess filterText model =
        { model with CurrentGuess = filterText }
        |> updateSuggestions
    let nextPerson model =
        let remainingPersons =
            match model.RemainingPersons with
            | _ :: xs -> xs
            | [] ->
                model.AllPersons
                |> choosePersonsWithImage
                |> List.shuffle
        { model with
            RemainingPersons = remainingPersons
            Suggestions = { model.Suggestions with Highlighted = None }
        }
        |> updateGuess ""

type GroupsLoadedModel = {
    Groups: LoadableGroup list
    SelectedGroups: Group list
    GroupSelectionVisible: bool
    PlayState: PlayModel
    Score: int
}

type GroupsLoadState =
    | GroupsLoading
    | GroupsLoadError
    | GroupsLoaded of GroupsLoadedModel

type Model = GroupsLoadState

type Msg =
    | LoadGroups
    | LoadGroupsResult of Result<Group list, exn>
    | ShowGroupSelection
    | SelectGroup of LoadableGroup
    | DeselectGroup of Group
    | LoadGroupResult of Result<Group * Person list, Group * exn>
    | CloseGroupSelection
    | UpdateGuess of string
    | SubmitGuess of (Group * PersonWithImage) option
    | SubmittedGuess of Result<Group * PersonWithImage, Group * PersonWithImage>
    | HighlightPreviousSuggestion
    | HighlightNextSuggestion
    | ResetScore

type GuessResult =
    | Correct
    | Incorrect of Person list
    | Skipped

let init = GroupsLoading

let update msg model =
    let mapLoadedGroups fn =
        match model with
        | GroupsLoaded groups -> fn groups |> GroupsLoaded
        | GroupsLoading
        | GroupsLoadError -> model
    match msg with
    | LoadGroups ->
        GroupsLoading
    | LoadGroupsResult (Ok groups) ->
        GroupsLoaded {
            Groups = [ for group in groups -> { Group = group; LoadStatus = GroupLoadStatusNotLoaded } ]
            SelectedGroups = []
            GroupSelectionVisible = false
            PlayState = {
                AllPersons = []
                RemainingPersons = []
                CurrentGuess = ""
                Suggestions = {
                    Items = []
                    Highlighted = None
                }
            }
            Score = 0
        }
    | LoadGroupsResult (Error e) ->
        GroupsLoadError
    | ShowGroupSelection ->
        mapLoadedGroups (fun model -> { model with GroupSelectionVisible = true })
    | SelectGroup loadableGroup ->
        mapLoadedGroups (fun model -> { model with SelectedGroups = model.SelectedGroups @ [ loadableGroup.Group ] })
    | LoadGroupResult (Ok (group, persons)) ->
        mapLoadedGroups (fun model ->
            { model with
                Groups =
                    model.Groups
                    |> List.map (function
                        | { Group = g } as x when g = group -> { x with LoadStatus = GroupLoadStatusLoaded persons }
                        | v -> v
                    )
                PlayState = PlayModel.addGroup (group, persons) model.PlayState
            }
        )
    | LoadGroupResult (Error (group, _)) ->
        mapLoadedGroups (fun model -> { model with SelectedGroups = List.except [ group ] model.SelectedGroups })
    | DeselectGroup group ->
        mapLoadedGroups (fun model ->
            { model with
                SelectedGroups = List.except [ group ] model.SelectedGroups
                PlayState = PlayModel.removeGroup group model.PlayState
            })
    | CloseGroupSelection ->
        mapLoadedGroups (fun model -> { model with GroupSelectionVisible = false })
    | UpdateGuess text ->
        mapLoadedGroups (fun model -> { model with PlayState = PlayModel.updateGuess text model.PlayState })
    | SubmitGuess guess -> model
    | SubmittedGuess (Ok _) ->
        mapLoadedGroups (fun model ->
            { model with
                PlayState = PlayModel.nextPerson model.PlayState
                Score = model.Score + 1
            }
        )
    | SubmittedGuess (Error _) ->
        mapLoadedGroups (fun model ->
            { model with
                PlayState = PlayModel.nextPerson model.PlayState
                Score = model.Score - 1
            }
        )
    | HighlightPreviousSuggestion ->
        mapLoadedGroups (fun model ->
            let suggestions = Suggestions.changeHighlightedItem model.PlayState.Suggestions Suggestions.Previous
            { model with PlayState = { model.PlayState with Suggestions = suggestions } }
        )
    | HighlightNextSuggestion ->
        mapLoadedGroups (fun model ->
            let suggestions = Suggestions.changeHighlightedItem model.PlayState.Suggestions Suggestions.Next
            { model with PlayState = { model.PlayState with Suggestions = suggestions } }
        )
    | ResetScore ->
        mapLoadedGroups (fun model -> { model with Score = 0 })

let view model dispatch =
    Container.container [] [
        match model with
        | GroupsLoading ->
            Progress.progress [ Progress.Color IsInfo ] []
        | GroupsLoaded data ->
            Modal.modal [ Modal.IsActive data.GroupSelectionVisible ] [
                Modal.background [ Props [ OnClick (fun _ -> dispatch CloseGroupSelection) ] ] [ ]
                Modal.content [ ] [
                    Box.box' [ ] [
                        h3 [ Class "title" ] [ str "Select groups" ]
                        let groupLists =
                            data.Groups
                            |> List.groupBy (function
                                | { Group = Teachers } -> 0
                                | { Group = Students className } -> Class.level className
                            )
                            |> List.map (snd >> List.sortBy (function | { Group = Teachers } -> "" | { Group = Students className } -> className))
                        for groups in groupLists do
                            Button.list [] [
                                for loadableGroup in groups do
                                    let isSelected = List.contains loadableGroup.Group data.SelectedGroups
                                    Button.button
                                        [
                                            Button.Color (if isSelected then IsLink else NoColor)
                                            if isSelected
                                            then Button.OnClick (fun _ev -> dispatch (DeselectGroup loadableGroup.Group))
                                            else Button.OnClick (fun _ev -> dispatch (SelectGroup loadableGroup))
                                            Button.IsLoading (isSelected && loadableGroup.LoadStatus = GroupLoadStatusNotLoaded)
                                        ]
                                        [
                                            str (Group.toString loadableGroup.Group)
                                        ]
                            ]
                    ]
                ]
                Modal.close
                    [
                        Modal.Close.Size IsLarge
                        Modal.Close.OnClick (fun _ -> dispatch CloseGroupSelection)
                    ]
                    []
            ]
            Container.container [] [
                div [ Style [ Padding "0.75rem" ] ] [
                    Level.level [ Level.Level.Option.Props [ Style [ Width "100%" ] ] ] [
                        Level.item [] [
                            div [] [
                                Level.title [] [
                                    img [ Src "img/know-name-logo.svg"; HTMLAttr.Width "32px"; HTMLAttr.Height "32px" ]
                                    span [ Style [ MarginLeft "10px" ] ] [
                                        str "Know name"
                                    ]
                                ]
                            ]
                        ]
                        Level.item [ Level.Item.HasTextCentered ] [
                            div [] [
                                Level.heading [] [ str "Groups" ]
                                Level.title [ Props [ Style [ TextAlign TextAlignOptions.Left ] ] ] [
                                    Button.button
                                        [
                                            Button.OnClick (fun _ -> dispatch ShowGroupSelection)
                                        ]
                                        [
                                            data.SelectedGroups
                                            |> List.sortBy (function
                                                | Teachers -> (0, "")
                                                | Students className -> (1, className)
                                            )
                                            |> List.map Group.toString
                                            |> function
                                            | [] -> "Click to select"
                                            | x -> String.concat ", " x
                                            |> str
                                        ]
                                ]
                            ]
                        ]
                        Level.item [ Level.Item.HasTextCentered ] [
                            match data.PlayState with
                            | { RemainingPersons = [] } -> ()
                            | playingModel ->
                                div [] [
                                    Level.heading [ Props [ Style [ Opacity 0 ] ] ] [ str "Name" ] // TODO &nbsp; would suffice
                                    form
                                        [
                                            OnSubmit (fun ev ->
                                                ev.preventDefault()
                                                dispatch (SubmitGuess playingModel.Suggestions.Highlighted)
                                            )
                                        ]
                                        [
                                            Input.text
                                                [
                                                    Input.Placeholder "Name"
                                                    Input.OnChange (fun ev -> UpdateGuess !!ev.target?value |> dispatch)
                                                    Input.Value playingModel.CurrentGuess
                                                    Input.Props [ AutoComplete "new-password" ]
                                                ]
                                        ]
                                ]
                        ]
                        Level.item [ Level.Item.HasTextCentered ] [
                            div
                                [
                                    OnDoubleClick (fun _ev -> dispatch ResetScore)
                                    ClassName "is-unselectable"
                                ]
                                [
                                    Level.heading [] [ str "Score" ]
                                    let color = if data.Score >= 0 then "lightgreen" else "red"
                                    Level.title [ Props [ Style [ Color color ] ] ] [
                                        str (string data.Score)
                                    ]
                                ]
                        ]
                    ]
                ]
                match data.PlayState with
                | { AllPersons = [] } -> ()
                | { RemainingPersons = (_, currentPerson) :: _; Suggestions = suggestions } ->
                    Container.container [ Container.Props [ Style [ Display DisplayOptions.Flex; Height "calc(100vh - 180px)" ] ] ]
                        [
                            Tile.ancestor []
                                [
                                    Tile.parent [ Tile.Size Tile.Is8 ]
                                        [
                                            Tile.child []
                                                [
                                                    Box.box' [ Props [ Style [ Height "100%" ] ] ]
                                                        [
                                                            Image.image [ Image.Props [ Style [ Height "100%" ] ] ]
                                                                [
                                                                    img
                                                                        [
                                                                            Src currentPerson.ImageUrl
                                                                            Style [ MaxHeight "100%"; ObjectFit "contain" ]
                                                                        ]
                                                                ]
                                                        ]
                                                ]
                                        ]
                                    Tile.parent [ Tile.Props [ Style [ MinHeight "auto" ] ] ]
                                        [
                                            Tile.child [ Tile.Props [ Style [ Height "100%" ] ] ]
                                                [
                                                    Box.box' [ Props [ Id "suggestions"; Style [ Height "100%"; OverflowY OverflowOptions.Auto ] ] ]
                                                        [
                                                            for suggestion in suggestions.Items do
                                                                let (group, person) = suggestion
                                                                match PersonWithImage.tryFromPerson person with
                                                                | Some person ->
                                                                    Dropdown.Item.a
                                                                        [
                                                                            Dropdown.Item.IsActive (suggestions.Highlighted.IsSome && suggestions.Highlighted = Some (group, person))
                                                                            Dropdown.Item.Props [ OnClick (fun _ev -> SubmitGuess (Some (group, person)) |> dispatch) ]
                                                                        ]
                                                                        [ str (sprintf "%s (%s)" person.DisplayName (Group.toString group)) ]
                                                                | None ->
                                                                    Dropdown.Item.div
                                                                        [
                                                                            Dropdown.Item.Props [
                                                                                Title (sprintf "Kein Foto von \"%s\" gefunden" person.DisplayName)
                                                                                Style [ Color "red" ]
                                                                            ]
                                                                        ]
                                                                        [ str (sprintf "%s (%s)" person.DisplayName (Group.toString group)) ]
                                                        ]
                                                ]
                                        ]
                                ]
                        ]
                | { RemainingPersons = [] } ->
                    Notification.notification [ Notification.Color IsDanger ]
                        [ str "Für keine Person der ausgewählten Gruppen ist ein Foto vorhanden" ]
            ]
        | GroupsLoadError -> Views.errorWithRetryButton "Fehler beim Laden der Daten." (fun () -> dispatch LoadGroups)
    ]

let stream (getAuthRequestHeader, (pageActive: IAsyncObservable<bool>)) (states: IAsyncObservable<Msg option * Model>) (msgs: IAsyncObservable<Msg>) =
    pageActive
    |> AsyncRx.flatMapLatest (function
        | true ->
            [
                msgs

                let loadGroups =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync' (async {
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            return! Fetch.``get``("/api/know-name/groups", Decode.list Group.decoder, requestProperties) |> Async.AwaitPromise
                        })
                        |> AsyncRx.map Ok
                        |> AsyncRx.catch (Error >> AsyncRx.single)
                    )
                msgs
                |> AsyncRx.choose (function
                    | LoadGroups -> Some loadGroups
                    | _ -> None
                )
                |> AsyncRx.startWith [ loadGroups ]
                |> AsyncRx.switchLatest
                |> AsyncRx.showSimpleErrorToast (fun e -> "Loading groups failed", e.Message)
                |> AsyncRx.map LoadGroupsResult

                let loadGroup group =
                    AsyncRx.defer (fun () ->
                        AsyncRx.ofAsync' (async {
                            let url =
                                match group with
                                | Teachers -> "/api/know-name/teachers"
                                | Students className -> sprintf "/api/know-name/students/%s" className
                            let! authHeader = getAuthRequestHeader ()
                            let requestProperties = [ Fetch.requestHeaders [ authHeader ] ]
                            return! Fetch.``get``(url, Decode.list Person.decoder, requestProperties) |> Async.AwaitPromise
                        })
                        |> AsyncRx.map (fun v -> Ok (group, v))
                        |> AsyncRx.catch (fun v -> Error (group, v) |> AsyncRx.single)
                    )

                msgs
                |> AsyncRx.flatMap (function
                    | SelectGroup ({ Group = group; LoadStatus = GroupLoadStatusNotLoaded }) -> loadGroup group
                    | SelectGroup ({ Group = group; LoadStatus = GroupLoadStatusLoaded persons }) -> AsyncRx.single (Ok (group, persons))
                    | _ -> AsyncRx.empty ()
                )
                |> AsyncRx.showSimpleErrorToast (fun (g, e) -> sprintf "Loading group %s failed" (Group.toString g), e.Message)
                |> AsyncRx.map LoadGroupResult

                AsyncRx.ofEvent "keydown"
                |> AsyncRx.choose (fun (e: Browser.Types.KeyboardEvent) ->
                    if e.key = "ArrowUp" then Some HighlightPreviousSuggestion
                    elif e.key = "ArrowDown" then Some HighlightNextSuggestion
                    else None
                )

                let updateView fn =
                    // https://stackoverflow.com/a/34999925/1293659
                    Browser.Dom.window.setTimeout(
                        fun () ->
                            Browser.Dom.window.requestAnimationFrame(fun dt ->
                                fn()
                            )
                            |> ignore
                        , 0
                    )
                    |> ignore

                states
                |> AsyncRx.tapOnNext (function
                    | Some (UpdateGuess _), GroupsLoaded model
                    | Some HighlightPreviousSuggestion, GroupsLoaded model
                    | Some HighlightNextSuggestion, GroupsLoaded model ->
                        match Suggestions.tryIndexOfHighlighted model.PlayState.Suggestions with
                        | Some idx ->
                            updateView (fun () ->
                                let suggestions = Browser.Dom.document.querySelector("#suggestions")
                                let isScrollable = !!suggestions?scrollHeight <> !!suggestions?offsetHeight
                                if isScrollable then
                                    let el = suggestions.querySelectorAll(".dropdown-item").[idx]
                                    el?scrollIntoView(createObj [ "block" ==> "nearest" ]) |> ignore
                            )
                        | None ->
                            updateView (fun () ->
                                Browser.Dom.document.querySelector("#suggestions")?scrollTop <- 0.
                            )
                    | _ -> ()
                )
                |> AsyncRx.flatMap (ignore >> AsyncRx.empty)

                msgs
                |> AsyncRx.choose (function | SubmitGuess guess -> Some guess | _ -> None)
                |> AsyncRx.withLatestFrom (AsyncRx.map snd states)
                |> AsyncRx.choose (fun (guess, model) ->
                    match model with
                    | GroupsLoaded model ->
                        match PlayModel.currentPerson model.PlayState with
                        | None -> None
                        | Some x when Some x = guess -> Some (Ok x)
                        | Some x -> Some (Error x)
                    | GroupsLoading
                    | GroupsLoadError -> None
                )
                |> AsyncRx.showSuccessToast (fun (group, person) ->
                    Toast.create person.DisplayName
                    |> Toast.icon (img [ Src person.ImageUrl; Style [ Height "50px" ] ])
                    |> Toast.timeout (TimeSpan.FromSeconds 5.)
                )
                |> AsyncRx.showErrorToast (fun (group, person) ->
                    Toast.create person.DisplayName
                    |> Toast.icon (img [ Src person.ImageUrl; Style [ Height "50px" ] ])
                    |> Toast.timeout (TimeSpan.FromSeconds 5.)
                )
                |> AsyncRx.map SubmittedGuess
            ]
            |> AsyncRx.mergeSeq
        | false ->
            AsyncRx.empty ()
    )
