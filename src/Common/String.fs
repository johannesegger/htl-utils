module String

open System

let split (separator: string) (text: string) =
    text.Split([| separator |], StringSplitOptions.None)

let trySubstringFrom idx (text: string) =
    if idx <= text.Length
    then text.Substring idx |> Some
    else None

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
