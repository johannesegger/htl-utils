module Result

let apply v fn =
    match fn, v with
    | Ok fn, Ok v -> Ok (fn v)
    | Error e, Ok _
    | Ok _, Error e -> Error e
    | Error e1, Error e2 -> Error (e1 @ e2)

let bindAsync fn = function
    | Ok v -> fn v
    | Error v -> async { return Error v }

let traverseM fn list =
    (list, Ok [])
    ||> Seq.foldBack (fun item state ->
        match state with
        | Ok vs ->
            match fn item with
            | Ok v -> Ok (v :: vs)
            | Error e -> Error e
        | Error e -> Error e
    )

let sequenceA list =
    (list, Ok [])
    ||> Seq.foldBack (fun item state ->
        match item, state with
        | Ok v, Ok vs -> Ok (v :: vs)
        | Error e, Ok _ -> Error [ e ]
        | Ok _, Error es -> Error es
        | Error e, Error es -> Error (e :: es)
    )
