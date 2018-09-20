module String

open System

let split (separator: string) (text: string) =
    text.Split(separator, StringSplitOptions.None)

let trySubstringFrom idx (text: string) =
    if idx <= text.Length
    then text.Substring idx |> Some
    else None

let toLower (text: string) =
    text.ToLower()

let equalsCaseInsensitive (a: string) (b: string) =
    a.Equals(b, StringComparison.InvariantCultureIgnoreCase)