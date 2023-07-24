[<AutoOpen>]
module Utils

open System

let uncurry3 fn (a, b, c) =
    fn a b c

module String =
    let toLower (v: string) =
        if isNull v then v
        else v.ToLower()

let printWarning v =
    Console.ForegroundColor <- ConsoleColor.Yellow
    printfn "%s" v
    Console.ResetColor()
