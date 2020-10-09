module String

open System
open System.Text.RegularExpressions

let split (separator: string) (text: string) =
    text.Split([| separator |], StringSplitOptions.None)

let trySubstringFrom idx (text: string) =
    if idx <= text.Length
    then text.Substring idx |> Some
    else None

let trySplitAt (subString: string) (text: string) =
    match text.IndexOf(subString) with
    | -1 -> None
    | idx -> Some (text.Substring(0, idx), text.Substring(idx + 1))

let toLower (text: string) =
    text.ToLower()

let toUpper (text: string) =
    text.ToUpper()

let replace (oldValue: string) (newValue: string) (text: string) =
    text.Replace(oldValue, newValue)

let equalsCaseInsensitive (a: string) (b: string) =
    if isNull a then isNull b
    else a.Equals(b, StringComparison.InvariantCultureIgnoreCase)

let startsWithCaseInsensitive (value: string) (text: string) =
    text.StartsWith(value, StringComparison.InvariantCultureIgnoreCase)

let cut maxLength (text: string) =
    if text.Length > maxLength
    then text.Substring(0, maxLength)
    else text

let ellipsis maxLength (text: string) =
    if text.Length > maxLength
    then sprintf "%s ..." <| text.Substring(0, maxLength - 4)
    else text

let asAlphaNumeric =
    replace "Ä" "Ae"
    >> replace "Ö" "Oe"
    >> replace "Ü" "Ue"
    >> replace "ä" "ae"
    >> replace "ö" "oe"
    >> replace "ü" "ue"
    #if !FABLE_COMPILER
    >> Unidecode.NET.Unidecoder.Unidecode
    #endif
    >> fun s -> Regex.Replace(s, @"[^a-zA-Z0-9._-]", "")
