[<AutoOpen>]
module Utils

let flip fn a b = fn b a

let curry fn a b = fn (a, b)

let uncurry fn (a, b) = fn a b

let tryDo fn arg =
    match fn arg with
    | (true, value) -> Some value
    | (false, _) -> None
