module CorrectExercise

open System
open System.Text.RegularExpressions
open Elmish
open Fable.Core.JsInterop
open Fable.Helpers.React
open Fable.Helpers.React.Props
open Fable.Import.Browser
open Fable.Import.React
open Fable.PowerPack
open FileSystem
open Fulma
open Fulma.FontAwesome
open Monaco
open Thoth.Elmish
open Thoth.Json
open Monaco.MonacoEditor
open Monaco.MonacoEditor.Editor
open ClientLib.Correction

let uncurry fn (a, b) = fn a b

module TextPosition =
    let fromPosition (p: IPosition) =
        { Line = int p.lineNumber
          Column = int p.column }

    let toPosition p =
        monaco.Position.Create (float p.Line, float p.Column)
        :?> IPosition

module StartPosition =
    let fromRange (range: IRange) =
        { Line = int range.startLineNumber
          Column = int range.startColumn }

module EndPosition =
    let fromRange (range: IRange) =
        { Line = int range.endLineNumber
          Column = int range.endColumn }

module RangeHelper =
    let fromPositions start ``end`` =
        monaco.Range.Create (float start.Line, float start.Column, float ``end``.Line, float ``end``.Column)
        :?> IRange

    let fromPosition position =
        fromPositions position position

type DecorationId = DecorationId of string

module DecorationId =
    let value (DecorationId decorationId) = decorationId

type DocumentId = DocumentId of Guid
type ExerciseId = ExerciseId of Guid

type Document =
    { Id: DocumentId
      FileName: string
      OriginalContent: string
      Editor: IStandaloneCodeEditor option
      EditorContent: string
      EditorDecorations: DecorationId list
      Corrections: Correction list
      PendingReInsertCorrections: int }

type StudentExercise =
    { Id: ExerciseId
      Student: string
      Documents: Document list
      Result: string }

type Settings =
    { HasResult: bool
      ResultTemplate: string }

type Model =
    { Exercises: StudentExercise list option
      Settings: Settings
      ShowCorrections: bool
      CorrectionsData: string }

type Msg =
    | StartCorrection of DataTransferItemList
    | StartCorrectionResult of Result<StudentExercise list, exn>
    | SetEditor of DocumentId * IStandaloneCodeEditor
    | EditText of DocumentId * IModelContentChange list
    | Print
    | PrintResult of Result<unit, exn>
    | SetHasResultTemplate of bool
    | SetResultTemplate of string
    | SetResult of ExerciseId * string
    | ShowCorrectionsData
    | HideCorrectionsData
    | EditCorrectionsData of string
    | LoadCorrectionsData
    | LoadCorrectionsDataSuccess of StudentExercise list
    | PostLoadCorrectionsDataSuccess
    | ResetDocument of DocumentId
    | Close

exception private DomException of DOMException
exception private DomError of DOMError

let private getFileEntries (entry: FileSystemDirectoryEntry) =
    Promise.create (fun succeed fail ->
        let reader = entry.createReader()
        reader.readEntries(succeed, DomException >> fail)
    )

let rec private getAllFileEntries (parent: FileSystemEntry) = promise {
    if parent.isDirectory
    then
        let dir = parent :?> FileSystemDirectoryEntry
        let! entries = getFileEntries dir
        let! children =
            entries
            |> Seq.map getAllFileEntries
            |> Promise.Parallel
            |> Promise.map (Seq.collect id >> Seq.toList)
        return children
    else
        return [ parent :?> FileSystemFileEntry ]
}

let private readFile (entry: FileSystemFileEntry) = promise {
    let! file = Promise.create (fun succeed fail -> entry.file succeed)
    return!
        Promise.create (fun succeed fail ->
            let reader = Fable.Import.Browser.FileReader.Create()
            reader.onload <- fun ev ->
                succeed (file, reader.result :?> string)
            reader.onerror <- fun ev ->
                fail (DomError reader.error)
            reader.readAsText(file)
        )
}

let private createExercises (items: DataTransferItemList) =
    items
    :?> Fable.Import.JS.ArrayLike<Fable.Import.Browser.DataTransferItem>
    |> Fable.Import.JS.Array.from
    |> Seq.filter (fun item -> item.kind = "file")
    |> Seq.map (fun item -> promise {
        let entry: FileSystemEntry = item?webkitGetAsEntry()
        let student = entry.name
        let! files =
            getAllFileEntries entry
            |> Promise.bind (List.map readFile >> Promise.Parallel)
        let documents =
            files
            |> Seq.map (fun (file, content) ->
                { Id = Guid.NewGuid() |> DocumentId
                  FileName = file.name
                  OriginalContent = content
                  Editor = None
                  EditorContent = content
                  EditorDecorations = []
                  Corrections = []
                  PendingReInsertCorrections = 0
                }
            )
            |> Seq.sortBy (fun f -> f.FileName)
            |> Seq.toList
        return
            { Id = Guid.NewGuid() |> ExerciseId
              Student = student
              Documents = documents
              Result = "" }
    })
    |> Promise.Parallel
    |> Promise.map (Seq.sortBy (fun p -> p.Student) >> Seq.toList)

let private updateExercise model exerciseId update =
    { model with
        Exercises =
            model.Exercises
            |> FSharp.Core.Option.map (fun exercises ->
                exercises
                |> List.map (fun e -> if e.Id = exerciseId then update e else e)
            )
    }

let private updateDocument model documentId update =
    { model with
        Exercises =
            match model.Exercises with
            | Some exercises ->
                Some
                    [ for exercise in exercises ->
                        { exercise with
                            Documents =
                                exercise.Documents
                                |> List.map (fun (d: Document) -> if d.Id = documentId then update d else d) }
                    ]
            | None -> None
    }

let private correctionToDecorations = function
    | { Correction = { CorrectionType = Insert insertCharacter as correctionType}; CurrentPosition = Some position } ->
        let decoration = createEmpty<IModelDeltaDecoration>
        decoration.options <- createEmpty<IModelDecorationOptions>
        match insertCharacter with
        | InsertCharacter.NormalCharacter _ ->
            decoration.range <- CorrectionType.getSpan position correctionType |> uncurry RangeHelper.fromPositions
            decoration.options.inlineClassName <- Some "added-text"
        | InsertCharacter.EndOfLine ->
            decoration.range <- 
                CorrectionType.getSpan position correctionType
                |> fun (s, e) -> { Line = s.Line + 1; Column = 1 }, e
                |> uncurry RangeHelper.fromPositions
            decoration.options.linesDecorationsClassName <- Some "added-line"
            decoration.options.isWholeLine <- Some true
        decoration.options.stickiness <- Some TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges
        [ decoration ]
    | { Correction = { CorrectionType = Insert _ }; CurrentPosition = None } -> []
    | { Correction = { CorrectionType = StrikeThrough removeCharacter as correctionType }; CurrentPosition = Some position } ->
        let decoration = createEmpty<IModelDeltaDecoration>
        decoration.options <- createEmpty<IModelDecorationOptions>
        match removeCharacter with
        | RemoveCharacter.NormalCharacter _ ->
            decoration.range <- CorrectionType.getSpan position correctionType |> uncurry RangeHelper.fromPositions
            decoration.options.inlineClassName <- Some "removed-text"
        | RemoveCharacter.EndOfLine ->
            decoration.range <- 
                CorrectionType.getSpan position correctionType
                |> fun (s, e) -> { Line = s.Line + 1; Column = 1 }, e
                |> uncurry RangeHelper.fromPositions
            decoration.options.linesDecorationsClassName <- Some "removed-line"
            decoration.options.isWholeLine <- Some true
        decoration.options.stickiness <- Some TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges
        [ decoration ]
    | { Correction = { CorrectionType = StrikeThrough _ }; CurrentPosition = None } -> []
    | { Correction = { CorrectionType = Delete _ } } -> []

let applyDecorations doc =
    let decorationIds =
        match doc.Corrections, doc.Editor with
        | _ :: _, Some editor ->
            let oldDecorationIds = List.map DecorationId.value doc.EditorDecorations
            let newDecorations =
                getSnapshots doc.Corrections
                |> List.collect correctionToDecorations

            editor.deltaDecorations(ResizeArray<_>(oldDecorationIds), ResizeArray<_>(newDecorations))
            |> Seq.map DecorationId
            |> Seq.toList
        | _ -> []
    { doc with EditorDecorations = decorationIds }

let setEditorContent doc =
    { doc with
        EditorContent =
            doc.Editor
            |> FSharp.Core.Option.map (fun e -> e.getModel().getValue())
            |> FSharp.Core.Option.defaultValue doc.OriginalContent }

let applyRemove range doc =
    // Delete and Insert corrections can be merged in directly,
    // StrikeThrough corrections must be re-inserted first
    let text =
        MonacoEditor.editor
            .createModel(doc.EditorContent)
            .getValueInRange(range)
    let correctionIntention =
        { CorrectionIntention.Position = StartPosition.fromRange range
          IntentionType = RemoveText (CorrectionIntentionType.removeText text) }

    let newCorrections = mergeCorrectionIntention correctionIntention doc.Corrections
    let (otherCorrections, reInsertCorrections) =
        newCorrections
        |> List.partition (fun c ->
            match c.CorrectionType with
            | Delete _ | Insert _ -> true
            | StrikeThrough _ -> false)
    
    // Recalculate the position of the re-insert corrections because the other
    // corrections now happened afterwards
    let reInsertCorrections' =
        getSnapshots (otherCorrections @ reInsertCorrections)
        |> List.filter (fun c ->
            match c.Correction.CorrectionType with
            | Delete _ | Insert _ -> false
            | StrikeThrough _ -> true)
        |> List.map (fun c -> { c.Correction with Position = c.CurrentPosition.Value })
        |> List.sortBy (fun c -> c.Position)

    match doc.Editor, reInsertCorrections with
    | Some editor, _ :: _ ->
        editor.trigger("re-insert", "undo", None)
        { doc with
            PendingReInsertCorrections = doc.PendingReInsertCorrections + 1
            Corrections = reInsertCorrections' @ otherCorrections @ doc.Corrections }
    | _ ->
        { doc with
            Corrections = otherCorrections @ doc.Corrections }

let encodeExercises exercises =
    let encodeCorrectionType = function
        | Insert (InsertCharacter.NormalCharacter ch) ->
            Encode.object
                [ "insert-character", Encode.string (string ch) ]
        | Insert InsertCharacter.EndOfLine ->
            Encode.object
                [ "insert-eol", Encode.object [] ]
        | StrikeThrough RemoveCharacter.NormalCharacter ->
            Encode.object
                [ "strike-through-character", Encode.object [] ]
        | StrikeThrough RemoveCharacter.EndOfLine ->
            Encode.object
                [ "strike-through-eol", Encode.object [] ]
        | Delete RemoveCharacter.NormalCharacter ->
            Encode.object
                [ "delete-character", Encode.object [] ]
        | Delete RemoveCharacter.EndOfLine ->
            Encode.object
                [ "delete-eol", Encode.object [] ]

    let encodePosition position =
        Encode.object
            [ "line", Encode.int position.Line
              "column", Encode.int position.Column ]

    let encodeCorrection correction =
        Encode.object
            [ "correctionType", encodeCorrectionType correction.CorrectionType
              "position", encodePosition correction.Position ]

    let encodeDocument (document: Document) =
        let (DocumentId documentId) = document.Id
        Encode.object
            [ "id", Encode.guid documentId
              "fileName", Encode.string document.FileName
              "originalContent", Encode.string document.OriginalContent
              "corrections", List.map encodeCorrection document.Corrections |> Encode.list
            ]

    let encodeExercise exercise =
        let (ExerciseId exerciseId) = exercise.Id
        Encode.object
            [ "id", Encode.guid exerciseId
              "student", Encode.string exercise.Student
              "documents", List.map encodeDocument exercise.Documents |> Encode.list
              "result", Encode.string exercise.Result
            ]

    List.map encodeExercise exercises
    |> Encode.list

let decodeExercises =
    let decodeInsertCharacter =
        Decode.field "insert-character" Decode.string
        |> Decode.andThen (fun value ->
            if value.Length = 1
            then Insert (InsertCharacter.NormalCharacter value.[0]) |> Decode.succeed
            else sprintf "Expected single insert character, got %s" value |> Decode.fail)

    let decodeInsertEol =
        Decode.field "insert-eol" (Decode.succeed (Insert InsertCharacter.EndOfLine))

    let decodeStrikeThroughCharacter =
        Decode.field "strike-through-character" (Decode.succeed (StrikeThrough RemoveCharacter.NormalCharacter))

    let decodeStrikeThroughEol =
        Decode.field "strike-through-eol" (Decode.succeed (StrikeThrough RemoveCharacter.EndOfLine))

    let decodeDeleteCharacter =
        Decode.field "delete-character" (Decode.succeed (StrikeThrough RemoveCharacter.NormalCharacter))

    let decodeDeleteEol =
        Decode.field "delete-eol" (Decode.succeed (StrikeThrough RemoveCharacter.EndOfLine))

    let decodeCorrectionType =
        Decode.oneOf
            [ decodeInsertCharacter
              decodeInsertEol
              decodeStrikeThroughCharacter
              decodeStrikeThroughEol
              decodeDeleteCharacter
              decodeDeleteEol ]

    let decodePosition =
        Decode.map2
            (fun line column -> { Line = line; Column = column })
            (Decode.field "line" Decode.int)
            (Decode.field "column" Decode.int)

    let decodeCorrection =
        Decode.map2
            (fun correctionType position ->
                { CorrectionType = correctionType
                  Position = position })
            (Decode.field "correctionType" decodeCorrectionType)
            (Decode.field "position" decodePosition)

    let decodeCorrections =
        Decode.list decodeCorrection

    let decodeDocument =
        Decode.map4
            (fun documentId fileName originalContent corrections ->
                { Id = DocumentId documentId
                  FileName = fileName
                  OriginalContent = originalContent
                  Editor = None
                  EditorContent = originalContent
                  EditorDecorations = []
                  Corrections = corrections
                  PendingReInsertCorrections = 0 })
            (Decode.field "id" Decode.guid)
            (Decode.field "fileName" Decode.string)
            (Decode.field "originalContent" Decode.string)
            (Decode.field "corrections" decodeCorrections)

    let decodeDocuments =
        Decode.list decodeDocument

    let decodeExercise =
        Decode.map4
            (fun exerciseId student documents result ->
                { Id = ExerciseId exerciseId
                  Student = student
                  Documents = documents
                  Result = result
                })
            (Decode.field "id" Decode.guid)
            (Decode.field "student" Decode.string)
            (Decode.field "documents" decodeDocuments)
            (Decode.field "result" Decode.string)
            
    Decode.list decodeExercise

let init =
    let model =
        { Exercises = None
          Settings =
            { HasResult = false
              ResultTemplate = "" }
          ShowCorrections = false
          CorrectionsData = "" }
    model, Cmd.none

let update msg model =
    match msg with
    | StartCorrection items ->
        let cmd =
            Cmd.ofPromise
                createExercises
                items
                (Ok >> StartCorrectionResult)
                (Error >> StartCorrectionResult)
        model, cmd
    | StartCorrectionResult (Ok documents) ->
        let model' = { model with Exercises = Some documents }
        model', Cmd.none
    | StartCorrectionResult (Error e) ->
        let cmd =
            Toast.toast "Exercise correction" "Failed to load exercise documents"
            |> Toast.error
        let model' = { model with Exercises = None }
        model', cmd
    | SetEditor (documentId, editor) ->
        let model' = updateDocument model documentId (fun d -> { d with Editor = Some editor })
        model', Cmd.none
    | EditText (documentId, changes) ->
        let applyChange doc (change: IModelContentChange) =
            // Monaco by default groups several edit operations to a single undo operation,
            // but we want every change to be a single undo operation
            doc.Editor
            |> FSharp.Core.Option.iter (fun editor -> editor.getModel().pushStackElement())

            if change.text = "" // Remove
            then
                applyRemove change.range doc
                |> applyDecorations
                |> setEditorContent
            else // Insert or replace
                let correctionIntention =
                    { CorrectionIntention.Position = EndPosition.fromRange change.range
                      IntentionType = InsertText change.text }
                { doc with Corrections = addCorrection correctionIntention doc.Corrections }
                |> applyRemove change.range
                |> applyDecorations
                |> setEditorContent
        let updateDoc doc =
            if doc.PendingReInsertCorrections > 0
            then
                { doc with
                    PendingReInsertCorrections = doc.PendingReInsertCorrections - List.length changes }
            else
                List.fold applyChange doc changes
        let model' = updateDocument model documentId updateDoc
        model', Cmd.none
    | Print ->
        let cmd =
            Cmd.ofPromise
                (fun () -> promise {
                    let editors =
                        model.Exercises
                        |> FSharp.Core.Option.toList
                        |> List.collect (fun exercises ->
                            exercises
                            |> List.collect (fun p -> p.Documents)
                            |> List.collect (fun doc -> FSharp.Core.Option.toList doc.Editor))
                    do!
                        editors
                        |> List.map (fun editor -> promise {
                            let action = editor.getAction("editor.unfoldAll")
                            do! action.run() :?> Fable.Import.JS.Promise<_>
                            
                            let lineHeight = 19.
                            let height = editor.getModel().getLineCount() * lineHeight
                            
                            let container = editor.getDomNode().parentElement
                            container.style.height <- sprintf "%fpx" height
                            editor.layout()
                        })
                        |> Promise.Parallel
                        |> Promise.map ignore
                    Fable.Import.Browser.window.print()
                })
                ()
                (Ok >> PrintResult)
                (Error >> PrintResult)
        model, cmd
    | PrintResult (Ok ()) ->
        model, Cmd.none
    | PrintResult (Error e) ->
        let cmd =
            Toast.toast "Print" e.Message
            |> Toast.error
        model, cmd
    | SetHasResultTemplate value ->
        let model' = { model with Settings = { model.Settings with HasResult = value } }
        model', Cmd.none
    | SetResultTemplate value ->
        let model' = { model with Settings = { model.Settings with ResultTemplate = value } }
        model', Cmd.none
    | SetResult (exerciseId, value) ->
        let model' = updateExercise model exerciseId (fun e -> { e with Result = value })
        model', Cmd.none
    | ShowCorrectionsData ->
        let model' =
            { model with
                ShowCorrections = true
                CorrectionsData =
                    model.Exercises
                    |> FSharp.Core.Option.map (encodeExercises >> Encode.toString 4)
                    |> FSharp.Core.Option.defaultValue "" }
        model', Cmd.none
    | HideCorrectionsData ->
        let model' = { model with ShowCorrections = false }
        model', Cmd.none
    | EditCorrectionsData value ->
        let model' = { model with CorrectionsData = value }
        model', Cmd.none
    | LoadCorrectionsData ->
        match Decode.fromString decodeExercises model.CorrectionsData with
        | Ok exercises ->
            let model' =
                { model with
                    ShowCorrections = false }
            let cmd =
                Cmd.ofPromise
                    (fun () -> Promise.sleep 100)
                    ()
                    (fun () -> LoadCorrectionsDataSuccess exercises)
                    (failwithf "Sleep failed: %O")
            model', Cmd.batch [ Cmd.ofMsg Close; cmd ]
        | Error e ->
            let cmd =
                Toast.toast "Loading exercises failed" e
                |> Toast.error
            model, cmd
    | LoadCorrectionsDataSuccess exercises ->
        let model' =
            { model with
                Exercises = Some exercises }
        let cmd =
            Cmd.ofPromise
                (fun () -> Promise.sleep 100)
                ()
                (fun () -> PostLoadCorrectionsDataSuccess)
                (failwithf "Sleep failed: %O")
        model', cmd
    | PostLoadCorrectionsDataSuccess ->
        let model' =
            { model with
                Exercises =
                    model.Exercises
                    |> FSharp.Core.Option.map (List.map (fun e ->
                        { e with Documents = List.map applyDecorations e.Documents }
                    ))
            }
        model', Cmd.none
    | ResetDocument documentId ->
        let updateDoc doc =
            { doc with
                Corrections = []
                EditorContent = doc.OriginalContent
                EditorDecorations = []
                PendingReInsertCorrections = 0 }
        let model' = updateDocument model documentId updateDoc
        model', Cmd.none
    | Close -> init

let private getCorrectedDocumentContent document =
    let folder correction (content: string) =
        match correction with
        | { Correction.Position = position; CorrectionType = Insert text } ->
            content.Split([| editorNewLine |], StringSplitOptions.None)
            |> Seq.mapi (fun idx line ->
                if idx + 1 = position.Line
                then
                    let index = position.Column - 1
                    let prefix = line.Substring(0, index)
                    let postfix = line.Substring(index)
                    prefix + (InsertCharacter.toString text) + postfix
                else line)
            |> String.concat editorNewLine
        | { CorrectionType = StrikeThrough _ } -> content
        | { CorrectionType = Delete _ } ->
            let startPosition, endPosition = CorrectionType.getSpan correction.Position correction.CorrectionType
            content.Split([| editorNewLine |], StringSplitOptions.None)
            |> Seq.indexed
            |> fun l ->
                Seq.foldBack
                    (fun (idx, line: string) lines ->
                        let startIndex = startPosition.Column - 1
                        let endIndex = endPosition.Column - 1
                        match idx + 1, lines with
                        | lineNumber, lines when lineNumber = startPosition.Line && lineNumber = endPosition.Line ->
                            let prefix = line.Substring (0, startIndex)
                            let postfix = line.Substring endIndex
                            sprintf "%s%s" prefix postfix :: lines
                        | lineNumber, lines when lineNumber = endPosition.Line ->
                            let segment = line.Substring endIndex
                            segment :: lines
                        | lineNumber, x :: xs when lineNumber = startPosition.Line ->
                            let segment = line.Substring (0, startIndex)
                            sprintf "%s%s" segment x :: xs
                        | lineNumber, lines when lineNumber > startPosition.Line && lineNumber < endPosition.Line ->
                            lines
                        | _, lines -> line :: lines
                    )
                    l
                    []
            |> String.concat editorNewLine
    List.foldBack folder document.Corrections document.OriginalContent

let view model dispatch =
    let editorView document =
        let editorOptions = createEmpty<IEditorConstructionOptions>
        editorOptions.scrollBeyondLastLine <- Some false
#if DEBUG
        // Some debug package (maybe remotedev?) is super-slow if minimap is turned on
        let minimap = createEmpty<IEditorMinimapOptions>
        minimap.enabled <- Some false
        editorOptions.minimap <- Some minimap
#endif
        ReactMonacoEditor.monacoEditor
            [ yield ReactMonacoEditor.Language "csharp"
              yield ReactMonacoEditor.Height (!^"500px")
              yield ReactMonacoEditor.Value (getCorrectedDocumentContent document)
              yield ReactMonacoEditor.Options editorOptions
              yield ReactMonacoEditor.OnChange (fun newValue ev -> dispatch (EditText (document.Id, Seq.toList ev.changes)))
              yield ReactMonacoEditor.EditorDidMount (fun editor monaco -> dispatch (SetEditor(document.Id, editor))) ] []
    let exercisesView =
        match model.Exercises with
        | Some exercises ->
            Container.container []
                [ Level.level [ Level.Level.CustomClass "no-print" ]
                    [ Level.left []
                        [ Level.item []
                            [ Field.div [ Field.HasAddons ]
                                [ Control.div []
                                    [ Input.text
                                        [ Input.Placeholder "Result template"
                                          Input.Value model.Settings.ResultTemplate
                                          Input.Disabled (not model.Settings.HasResult)
                                          Input.OnChange (fun ev -> dispatch (SetResultTemplate ev.Value)) ] ]
                                  Control.div []
                                    [ Button.a
                                        [ Button.Color IsLight
                                          Button.OnClick (fun _ev -> dispatch (SetHasResultTemplate (not model.Settings.HasResult))) ]
                                        [ str (if model.Settings.HasResult then "Disable result" else "Enable result") ] ] ] ] ]
                      Level.right []
                        [ Level.item []
                            [ Button.button
                                [ Button.Color IsSuccess
                                  Button.OnClick (fun _ev -> dispatch Print) ]
                                [ str "Print" ] ]
                          Level.item []
                            [ Button.button
                                [ Button.Color IsWarning
                                  Button.OnClick (fun _ev -> dispatch ShowCorrectionsData) ]
                                [ str "Show/Edit corrections" ] ]
                          Level.item []
                            [ Button.button
                                [ Button.Color IsDanger
                                  Button.OnClick (fun _ev -> dispatch Close) ]
                                [ str "Close" ] ] ] ]
                  Container.container [ Container.Props [ Id "exercise-correction" ] ]
                    [ for exercise in exercises ->
                        Box.box' []
                            [ Level.level []
                                [ Level.left []
                                    [ Level.item []
                                        [ Heading.h4 [ Heading.IsSubtitle ]
                                            [ str exercise.Student ] ] ]
                                  Level.right []
                                    [ if model.Settings.HasResult then
                                        yield
                                            Level.item []
                                                [ Tag.tag
                                                    [ Tag.Color IsPrimary
                                                      Tag.Size IsLarge ]
                                                    [ ReactContentEditable.contentEditable
                                                        [ ReactContentEditable.Html (if exercise.Result <> "" then exercise.Result else model.Settings.ResultTemplate)
                                                          ReactContentEditable.OnChange (fun ev -> dispatch (SetResult (exercise.Id, ev.Value))) ] ] ] ] ]
                              Content.content []
                                (
                                    [ for document in exercise.Documents ->
                                        Card.card []
                                            [ Card.header []
                                                [ Card.Header.title [] [ str document.FileName ] ]
                                              Card.content [ ]
                                                [ Content.content [ ]
                                                    [ editorView document ] ]
                                              Card.footer [ CustomClass "no-print" ]
                                                [ Card.Footer.a [ Props [ OnClick (fun _ev -> dispatch (ResetDocument document.Id)) ] ]
                                                    [ str "Reset" ] ] ]
                                    ]
                                    |> List.intersperse (hr [])
                                ) ] ] ]
        | None ->
            Container.container []
                [ div
                    [ Style [ Border "10px dashed #ccc"; Height "300px"; Margin "20px auto"; Padding "50px" ]
                      OnDrop (fun ev -> ev.preventDefault(); dispatch (StartCorrection ev.dataTransfer.items))
                      OnDragOver (fun ev -> ev.preventDefault()) ]
                    [ Heading.h2 []
                        [ str "Drop file or folder or "
                          Button.button
                            [ Button.Color IsWarning
                              Button.OnClick (fun _ev -> dispatch ShowCorrectionsData)
                              Button.Size IsLarge ]
                            [ str "resume where you left off" ] ] ] ]

    let correctionsDataView =
        let editorOptions = createEmpty<IEditorConstructionOptions>
        editorOptions.scrollBeyondLastLine <- Some false
        editorOptions.wordWrap <- Some "on"
#if DEBUG
        // Some debug package (maybe remotedev?) is super-slow if minimap is turned on
        let minimap = createEmpty<IEditorMinimapOptions>
        minimap.enabled <- Some false
        editorOptions.minimap <- Some minimap
#endif
        Modal.modal [ Modal.IsActive true ]
            [ Modal.background [ Props [ OnClick (fun _ev -> dispatch HideCorrectionsData) ] ] []
              Modal.content []
                [ Card.card []
                    [ Card.header []
                        [ Card.Header.title [] [ str "Corrections data" ] ]
                      Card.content [ ]
                        [ Content.content [ ]
                            [ ReactMonacoEditor.monacoEditor
                                [ yield ReactMonacoEditor.Language "json"
                                  yield ReactMonacoEditor.Height (!^"500px")
                                  yield ReactMonacoEditor.Value model.CorrectionsData
                                  yield ReactMonacoEditor.Options editorOptions
                                  yield ReactMonacoEditor.OnChange (fun newValue ev -> dispatch (EditCorrectionsData newValue)) ] [] ] ]
                      Card.footer [ ]
                        [ Card.Footer.a [ Props [ OnClick (fun _ev -> dispatch LoadCorrectionsData) ] ]
                            [ str "Ok" ] ] ] ]
              Modal.close
                [ Modal.Close.Size IsLarge
                  Modal.Close.OnClick (fun _ev -> dispatch HideCorrectionsData) ] [] ]

    div []
        [ yield exercisesView
          if model.ShowCorrections then yield correctionsDataView
        ]