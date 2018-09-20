module DateTime

open System
open System.Globalization

let tryParseExact (format: string) (text: string) =
    match DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None) with
    | (true, v) -> Some v
    | _ -> None