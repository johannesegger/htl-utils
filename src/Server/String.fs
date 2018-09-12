module String

open System

let split (separator: string) (text: string) =
    text.Split(separator, StringSplitOptions.None)