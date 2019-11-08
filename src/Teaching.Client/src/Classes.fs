namespace global

type LoadableClassList =
    | NotLoadedClassList
    | FailedToLoadClassList
    | LoadedClassList of string list list

module Classes =
    let groupAndSort (classes: string list) =
        classes
        |> List.groupBy (fun v -> v.[0])
        |> List.sortBy fst
        |> List.map (snd >> List.sort)
