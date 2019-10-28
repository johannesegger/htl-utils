[<AutoOpen>]
module Utils

let flip fn a b = fn b a

let curry fn a b = fn (a, b)

let trimEMailAddressDomain (address: string) =
    match address.IndexOf '@' with
    | -1 -> address
    | i -> address.Substring(0, i)
