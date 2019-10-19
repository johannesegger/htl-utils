module Result

let apply v fn =
    match fn, v with
    | Ok fn, Ok v -> Ok (fn v)
    | Error e, Ok _
    | Ok _, Error e -> Error e
    | Error e1, Error e2 -> Error (e1 @ e2)
