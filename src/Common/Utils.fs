[<AutoOpen>]
module Utils

let flip fn a b = fn b a

let trimEMailAddressDomain (address: string) =
    match address.IndexOf '@' with
    | -1 -> address
    | i -> address.Substring(0, i)
