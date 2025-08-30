module Option

let sequence list =
    (list, Some [])
    ||> Seq.foldBack (fun item state ->
        match item, state with
        | Some v, Some vs -> Some (v :: vs)
        | _ -> None
    )

let fromTryPattern (success, value) =
    if success then Some value
    else None
