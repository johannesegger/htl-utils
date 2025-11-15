module List

open System

let intersperse separator list =
    let folder item = function
        | [] -> [ item ]
        | x -> item :: separator :: x
    List.foldBack folder list []

let shuffle<'a> =
    let rand = Random()

    let swap (a: _[]) x y =
        let tmp = a.[x]
        a.[x] <- a.[y]
        a.[y] <- tmp

    fun (l: 'a list) ->
        let a = Array.ofList l
        Array.iteri (fun i _ -> swap a i (rand.Next(i, Array.length a))) a
        Array.toList a

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
