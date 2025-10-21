module List

let diff (oldItems, oldItemToKey) (newItems, newItemToKey) =
    let oldItemsSet = oldItems |> List.map oldItemToKey |> Set.ofList
    let newItemsSet = newItems |> List.map newItemToKey |> Set.ofList
    let removed =
        oldItems
        |> List.filter (fun v -> not <| Set.contains (oldItemToKey v) newItemsSet)
    let added =
        newItems
        |> List.filter (fun v -> not <| Set.contains (newItemToKey v) oldItemsSet)
    (added, removed)
