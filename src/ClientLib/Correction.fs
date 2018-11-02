module ClientLib.Correction

open System

let editorNewLine = "\r\n"

[<CustomEquality>]
[<CustomComparison>]
type TextPosition =
    { Line: int
      Column: int }
    override x.GetHashCode () =
        hash (x.Line, x.Column)
    override x.Equals obj =
        match obj with
        | :? TextPosition as y -> (x.Line, x.Column) = (y.Line, y.Column)
        | _ -> false
    interface IComparable with
        member x.CompareTo obj =
            match obj with
            | :? TextPosition as y ->
                if x = y then 0
                elif x.Line < y.Line || x.Line = y.Line && x.Column < y.Column then -1
                else 1
            | _ -> invalidArg "obj" "Cannot compare values of different types"

type RemoveCharacter =
    | NormalCharacter
    | EndOfLine

type InsertCharacter =
    | NormalCharacter of char
    | EndOfLine

module InsertCharacter =
    let toString = function
        | NormalCharacter ch -> string ch
        | EndOfLine -> editorNewLine

type CorrectionIntentionType =
    | InsertText of string
    | RemoveText of RemoveCharacter list

module CorrectionIntentionType =
    let removeText (text: string) =
        let rec removeText' acc = function
            | [] -> acc
            | '\r' :: '\n' :: xs
            | '\r' :: xs
            | '\n' :: xs ->
                let acc' = RemoveCharacter.EndOfLine :: acc
                removeText' acc' xs
            | _ :: xs ->
                let acc' = RemoveCharacter.NormalCharacter :: acc
                removeText' acc' xs
        removeText' [] (Seq.toList text)
        |> List.rev

type CorrectionIntention =
    { Position: TextPosition
      IntentionType: CorrectionIntentionType }

type AtomicCorrectionIntentionType =
    | InsertCharacter of InsertCharacter
    | RemoveCharacter of RemoveCharacter
    
type AtomicCorrectionIntention =
    { Position: TextPosition
      IntentionType: AtomicCorrectionIntentionType }

module AtomicCorrectionIntention =
    let create ``type`` position : AtomicCorrectionIntention =
        { Position = position
          IntentionType = ``type`` }

module CorrectionIntention =
    let create ``type`` position : CorrectionIntention =
        { Position = position
          IntentionType = ``type`` }
    let split = function
        | { CorrectionIntention.Position = position; IntentionType = InsertText text } ->
            let rec getCorrection position acc = function
                | [] -> acc
                | '\r' :: '\n' :: xs
                | '\r' :: xs
                | '\n' :: xs ->
                    let acc' = AtomicCorrectionIntention.create (InsertCharacter InsertCharacter.EndOfLine) position :: acc
                    let position' = { Line = position.Line + 1; Column = 1 }
                    getCorrection position' acc' xs
                | c :: xs ->
                    let acc' = AtomicCorrectionIntention.create (InsertCharacter (InsertCharacter.NormalCharacter c)) position :: acc
                    let position' = { position with Column = position.Column + 1 }
                    getCorrection position' acc' xs
            getCorrection position [] (Seq.toList text)
        | { Position = position; IntentionType = RemoveText characters } ->
            let rec getCorrection position acc = function
                | [] -> acc
                | RemoveCharacter.EndOfLine :: xs ->
                    let acc' = AtomicCorrectionIntention.create (RemoveCharacter RemoveCharacter.EndOfLine) position :: acc
                    let position' = { Line = position.Line + 1; Column = 1 }
                    getCorrection position' acc' xs
                | RemoveCharacter.NormalCharacter :: xs ->
                    let acc' = AtomicCorrectionIntention.create (RemoveCharacter RemoveCharacter.NormalCharacter) position :: acc
                    let position' = { position with Column = position.Column + 1 }
                    getCorrection position' acc' xs
            getCorrection position [] characters
            |> List.rev

type CorrectionType =
    | Insert of InsertCharacter
    | StrikeThrough of RemoveCharacter
    | Delete of RemoveCharacter

module CorrectionType =
    let getSpan position = function
        | Insert (InsertCharacter.NormalCharacter _)
        | StrikeThrough (RemoveCharacter.NormalCharacter)
        | Delete (RemoveCharacter.NormalCharacter) ->
            position, { position with Column = position.Column + 1 }
        | Insert (InsertCharacter.EndOfLine)
        | StrikeThrough (RemoveCharacter.EndOfLine)
        | Delete (RemoveCharacter.EndOfLine) ->
            position, { Line = position.Line + 1; Column = 1 }

type Correction =
    { Position: TextPosition
      CorrectionType: CorrectionType }

module Correction =
    let create ``type`` position =
        { Position = position
          CorrectionType = ``type`` }

type CorrectionSnapshot =
    { Correction: Correction
      CurrentPosition: TextPosition option }

let getSnapshot laterCorrections correction =
    let rec fixSpan currentPosition = function
        | [] -> Some currentPosition
        | { Position = position; CorrectionType = Insert (InsertCharacter.NormalCharacter _) } :: xs ->
            if position.Line = currentPosition.Line && position <= currentPosition
            then fixSpan { currentPosition with Column = currentPosition.Column + 1 } xs
            else fixSpan currentPosition xs
        | { Position = position; CorrectionType = Insert (InsertCharacter.EndOfLine) } :: xs ->
            if position.Line < currentPosition.Line
            then fixSpan { currentPosition with Line = currentPosition.Line + 1 } xs
            elif position <= currentPosition
            then fixSpan { Line = currentPosition.Line + 1; Column = currentPosition.Column - position.Column + 1 } xs
            else fixSpan currentPosition xs
        | { CorrectionType = StrikeThrough _ } :: xs -> fixSpan currentPosition xs
        | { Position = position; CorrectionType = Delete (RemoveCharacter.NormalCharacter _) } :: xs ->
            if position.Line = currentPosition.Line && position < currentPosition
            then fixSpan { currentPosition with Column = currentPosition.Column - 1 } xs
            elif position = currentPosition
            then None
            else fixSpan currentPosition xs
        | { Position = position; CorrectionType = Delete (RemoveCharacter.EndOfLine) } :: xs ->
            if position.Line + 1 < currentPosition.Line
            then fixSpan { currentPosition with Line = currentPosition.Line - 1 } xs
            elif position.Line + 1 = currentPosition.Line
            then fixSpan { Line = currentPosition.Line - 1; Column = position.Column + currentPosition.Column - 1 } xs
            elif position = currentPosition
            then None
            else fixSpan currentPosition xs

    { Correction = correction
      CurrentPosition = fixSpan correction.Position laterCorrections }

let getSnapshots corrections =
    let getSnapshot' laterCorrectionSnapshots correction =
        let laterCorrections = List.map (fun c -> c.Correction) laterCorrectionSnapshots
        getSnapshot laterCorrections correction :: laterCorrectionSnapshots

    List.fold getSnapshot' [] corrections
    |> List.rev

let mergeCorrectionIntention (correctionIntention: CorrectionIntention) corrections =
    let snapshots = getSnapshots corrections

    CorrectionIntention.split correctionIntention
    |> List.map (fun correctionIntention ->
        let existingCorrectionSnapshot =
            snapshots
            |> List.tryFind (fun snapshot -> snapshot.CurrentPosition = Some correctionIntention.Position)
        match existingCorrectionSnapshot, correctionIntention with
        | Some { Correction = { CorrectionType = Insert _ } }, { IntentionType = RemoveCharacter ch } ->
            Correction.create (Delete ch) correctionIntention.Position
        | _ ->
            match correctionIntention with
            | { AtomicCorrectionIntention.Position = position; IntentionType = InsertCharacter ch } ->
                Correction.create (Insert ch) position
            | { Position = position; IntentionType = RemoveCharacter ch } ->
                Correction.create (StrikeThrough ch) position
    )

let addCorrection correctionIntention corrections =
    mergeCorrectionIntention correctionIntention corrections @ corrections
